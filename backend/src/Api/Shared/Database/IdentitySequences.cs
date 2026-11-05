using Microsoft.EntityFrameworkCore;

namespace Api.Shared.Database;

/// <summary>
/// Alinea los contadores automáticos (columnas identity/serial) con el mayor valor existente.
/// Hace falta cuando se cargan filas con su Id explícito (por ejemplo, la copia desde Azure SQL o una
/// restauración): el contador de PostgreSQL no avanza solo y los siguientes INSERT chocarían con Ids
/// ya usados. Solo adelanta contadores, nunca los retrocede, y no modifica filas.
/// </summary>
public static class IdentitySequences
{
    internal const string AlignSql = """
        DO $$
        DECLARE r record; maxv bigint; lastv bigint; called boolean;
        BEGIN
          FOR r IN
            SELECT c.table_name, c.column_name,
                   pg_get_serial_sequence(format('%I.%I', c.table_schema, c.table_name), c.column_name) AS seq
            FROM information_schema.columns c
            WHERE c.table_schema = current_schema()
              AND (c.is_identity = 'YES' OR c.column_default LIKE 'nextval%')
          LOOP
            CONTINUE WHEN r.seq IS NULL;
            EXECUTE format('SELECT max(%I) FROM %I', r.column_name, r.table_name) INTO maxv;
            CONTINUE WHEN maxv IS NULL;
            EXECUTE format('SELECT last_value, is_called FROM %s', r.seq) INTO lastv, called;
            IF maxv > lastv OR (maxv = lastv AND NOT called) THEN
              PERFORM setval(r.seq, maxv, true);
            END IF;
          END LOOP;
        END $$;
        """;

    public static Task AlignAsync(AppDbContext db, CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(AlignSql, ct);
}
