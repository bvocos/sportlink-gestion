// Copia los datos de la base SQL Server (Azure) a PostgreSQL (Railway).
//
// Requisitos: la base PostgreSQL destino ya debe tener el esquema creado (arrancar la API una vez contra
// ella aplica la migración InicialPostgres). La herramienta vacía las tablas destino y copia todo.
//
// Uso:
//   dotnet run -- --origen "<cadena SQL Server>" --destino "<cadena o URL PostgreSQL>"              (simulación)
//   dotnet run -- --origen "<cadena SQL Server>" --destino "<cadena o URL PostgreSQL>" --confirmar  (copia real)
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Npgsql;

var origen = Arg("--origen");
var destino = Arg("--destino");
var confirmar = args.Contains("--confirmar");
if (origen is null || destino is null)
{
    Console.Error.WriteLine("Uso: dotnet run -- --origen \"<SQL Server>\" --destino \"<PostgreSQL>\" [--confirmar]");
    return 2;
}
destino = ToNpgsql(destino);

await using var sql = new SqlConnection(origen);
// Azure SQL serverless se pausa sin uso: el primer intento la reanuda (error 40613) y tarda ~1 minuto.
for (var intento = 1; ; intento++)
{
    try { await sql.OpenAsync(); break; }
    catch (SqlException ex) when (intento < 12 && ex.Number is 40613 or 40197 or 40501 or 49918 or 4060 or -2)
    {
        Console.WriteLine($"Base de origen no disponible todavía (error {ex.Number}), reintentando en 15 s...");
        SqlConnection.ClearPool(sql);
        await Task.Delay(TimeSpan.FromSeconds(15));
    }
}
await using var pg = new NpgsqlConnection(destino);
await pg.OpenAsync();

var targetTables = await TargetTables(pg);
var sourceTables = await SourceTables(sql);
var problems = new List<string>();
var plan = new List<(string Table, List<(string Name, string Type)> Columns, long Rows)>();

Console.WriteLine($"Origen : {sql.DataSource}/{sql.Database}");
Console.WriteLine($"Destino: {pg.Host}:{pg.Port}/{pg.Database}");
Console.WriteLine();
foreach (var (table, columns) in targetTables)
{
    if (!sourceTables.TryGetValue(table, out var sourceColumns))
    {
        Console.WriteLine($"  {table,-28} (no existe en origen, queda vacía)");
        plan.Add((table, columns, -1));
        continue;
    }
    var missing = columns.Select(c => c.Name).Where(c => !sourceColumns.Contains(c)).ToList();
    if (missing.Count > 0) problems.Add($"{table}: faltan en origen las columnas {string.Join(", ", missing)}");
    var rows = await Count(sql, $"SELECT COUNT_BIG(*) FROM [dbo].[{table}]");
    var ignored = sourceColumns.Where(c => columns.All(t => t.Name != c)).ToList();
    Console.WriteLine($"  {table,-28} {rows,8} filas" + (ignored.Count > 0 ? $"   (columnas antiguas ignoradas: {string.Join(", ", ignored)})" : ""));
    plan.Add((table, columns, rows));
}
foreach (var extra in sourceTables.Keys.Where(t => !targetTables.ContainsKey(t) && t != "__EFMigrationsHistory"))
    Console.WriteLine($"  {extra,-28} (tabla solo en origen, no se copia)");

if (problems.Count > 0)
{
    Console.Error.WriteLine("\nNo se puede migrar:");
    problems.ForEach(p => Console.Error.WriteLine("  - " + p));
    return 1;
}
if (!confirmar)
{
    Console.WriteLine("\nSimulación correcta. Repetí con --confirmar para vaciar el destino y copiar los datos.");
    return 0;
}

Console.WriteLine("\nCopiando...");
await using (var tx = await pg.BeginTransactionAsync())
{
    // Desactiva las FK durante la carga (Depositos y Sucursales se referencian mutuamente);
    // la integridad se verifica explícitamente al final.
    await Exec(pg, "SET LOCAL session_replication_role = replica", tx);
    await Exec(pg, "TRUNCATE " + string.Join(", ", targetTables.Keys.Select(Q)) + " CASCADE", tx);
    foreach (var (table, columns, rows) in plan.Where(p => p.Rows > 0))
    {
        var copied = await CopyTable(sql, pg, table, columns);
        Console.WriteLine($"  {table,-28} {copied,8} filas copiadas");
    }
    await tx.CommitAsync();
}

Console.WriteLine("\nVerificando...");
var ok = true;
foreach (var (table, columns, rows) in plan.Where(p => p.Rows >= 0))
{
    var target = await Count(pg, $"SELECT COUNT(*) FROM {Q(table)}");
    var source = await Count(sql, $"SELECT COUNT_BIG(*) FROM [dbo].[{table}]");
    var numeric = columns.Where(c => c.Type == "numeric").Select(c => c.Name).ToList();
    var sumsMatch = true;
    foreach (var column in numeric)
    {
        var s = await Scalar(sql, $"SELECT COALESCE(SUM(CAST([{column}] AS decimal(38,6))),0) FROM [dbo].[{table}]");
        var t = await Scalar(pg, $"SELECT COALESCE(SUM({Q(column)}),0) FROM {Q(table)}");
        if (Convert.ToDecimal(s) != Convert.ToDecimal(t)) { sumsMatch = false; Console.WriteLine($"    suma distinta en {table}.{column}: {s} vs {t}"); }
    }
    var match = source == target && sumsMatch;
    ok &= match;
    Console.WriteLine($"  {(match ? "OK " : "ERR")} {table,-28} origen {source,8} | destino {target,8}");
}
var orphans = await ForeignKeyOrphans(pg);
foreach (var o in orphans) Console.WriteLine($"  ERR referencia rota: {o}");
ok &= orphans.Count == 0;
Console.WriteLine(ok ? "\nMigración completa y verificada." : "\nLa verificación encontró diferencias: revisar antes de usar el destino.");
return ok ? 0 : 1;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

static string ToNpgsql(string value)
{
    if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://")) return value;
    var uri = new Uri(value);
    var user = uri.UserInfo.Split(':', 2);
    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432, Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(user[0]), Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : null,
    }.ConnectionString;
}

static async Task<Dictionary<string, List<(string Name, string Type)>>> TargetTables(NpgsqlConnection pg)
{
    var result = new Dictionary<string, List<(string, string)>>();
    await using var cmd = new NpgsqlCommand("""
        SELECT c.table_name, c.column_name, c.udt_name
        FROM information_schema.columns c
        JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
        WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE' AND c.table_name <> '__EFMigrationsHistory'
          AND c.is_generated = 'NEVER'
        ORDER BY c.table_name, c.ordinal_position
        """, pg);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var table = reader.GetString(0);
        if (!result.TryGetValue(table, out var list)) result[table] = list = [];
        list.Add((reader.GetString(1), reader.GetString(2)));
    }
    return result;
}

static async Task<Dictionary<string, HashSet<string>>> SourceTables(SqlConnection sql)
{
    var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    await using var cmd = new SqlCommand("""
        SELECT t.name, c.name FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id
        WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND c.is_computed = 0
        """, sql);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var table = reader.GetString(0);
        if (!result.TryGetValue(table, out var set)) result[table] = set = new HashSet<string>(StringComparer.Ordinal);
        set.Add(reader.GetString(1));
    }
    return result;
}

static async Task<long> CopyTable(SqlConnection sql, NpgsqlConnection pg, string table, List<(string Name, string Type)> columns)
{
    await using var select = new SqlCommand(
        $"SELECT {string.Join(", ", columns.Select(c => $"[{c.Name}]"))} FROM [dbo].[{table}]", sql) { CommandTimeout = 0 };
    await using var reader = await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
    await using var writer = await pg.BeginBinaryImportAsync(
        $"COPY {Q(table)} ({string.Join(", ", columns.Select(c => Q(c.Name)))}) FROM STDIN (FORMAT BINARY)");
    long rows = 0;
    while (await reader.ReadAsync())
    {
        await writer.StartRowAsync();
        for (var i = 0; i < columns.Count; i++)
        {
            var value = reader.GetValue(i);
            if (value is DBNull) { await writer.WriteNullAsync(); continue; }
            await writer.WriteAsync(ToTarget(value, columns[i].Type), columns[i].Type);
        }
        rows++;
    }
    await writer.CompleteAsync();
    return rows;
}

static object ToTarget(object value, string pgType) => (value, pgType) switch
{
    (DateTimeOffset dto, "timestamptz") => dto.ToUniversalTime(),
    (DateTime dt, "timestamptz") => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
    (DateTime dt, "timestamp") => DateTime.SpecifyKind(dt, DateTimeKind.Unspecified),
    (DateTimeOffset dto, "timestamp") => DateTime.SpecifyKind(dto.UtcDateTime, DateTimeKind.Unspecified),
    (DateTime dt, "date") => DateOnly.FromDateTime(dt),
    (string s, "bpchar") => s,
    (bool b, "bool") => b,
    (byte b, "int2" or "int4") => (int)b,
    (short s, "int4") => (int)s,
    (int i, "int8") => (long)i,
    _ => value,
};

static async Task<long> Count(System.Data.Common.DbConnection connection, string query)
    => System.Convert.ToInt64(await Scalar(connection, query), CultureInfo.InvariantCulture);

static async Task<object?> Scalar(System.Data.Common.DbConnection connection, string query)
{
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = query;
    cmd.CommandTimeout = 0;
    return await cmd.ExecuteScalarAsync();
}

static async Task Exec(NpgsqlConnection pg, string query, NpgsqlTransaction tx)
{
    await using var cmd = new NpgsqlCommand(query, pg, tx);
    await cmd.ExecuteNonQueryAsync();
}

static async Task<List<string>> ForeignKeyOrphans(NpgsqlConnection pg)
{
    var checks = new List<(string Name, string Query)>();
    await using (var cmd = new NpgsqlCommand("""
        SELECT con.conname, src.relname, tgt.relname,
               array_agg(sa.attname ORDER BY k.ord), array_agg(ta.attname ORDER BY k.ord)
        FROM pg_constraint con
        JOIN pg_class src ON src.oid = con.conrelid
        JOIN pg_class tgt ON tgt.oid = con.confrelid
        JOIN pg_namespace n ON n.oid = src.relnamespace AND n.nspname = 'public'
        CROSS JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS k(src_col, tgt_col, ord)
        JOIN pg_attribute sa ON sa.attrelid = con.conrelid AND sa.attnum = k.src_col
        JOIN pg_attribute ta ON ta.attrelid = con.confrelid AND ta.attnum = k.tgt_col
        WHERE con.contype = 'f'
        GROUP BY con.conname, src.relname, tgt.relname
        """, pg))
    await using (var reader = await cmd.ExecuteReaderAsync())
        while (await reader.ReadAsync())
        {
            var (name, src, tgt) = (reader.GetString(0), reader.GetString(1), reader.GetString(2));
            var srcCols = reader.GetFieldValue<string[]>(3);
            var tgtCols = reader.GetFieldValue<string[]>(4);
            var join = string.Join(" AND ", srcCols.Zip(tgtCols, (s, t) => $"t.{Q(t)} = s.{Q(s)}"));
            var notNull = string.Join(" AND ", srcCols.Select(s => $"s.{Q(s)} IS NOT NULL"));
            checks.Add((name, $"SELECT COUNT(*) FROM {Q(src)} s WHERE {notNull} AND NOT EXISTS (SELECT 1 FROM {Q(tgt)} t WHERE {join})"));
        }
    var broken = new List<string>();
    foreach (var (name, query) in checks)
    {
        var count = await Count(pg, query);
        if (count > 0) broken.Add($"{name}: {count} filas");
    }
    return broken;
}
