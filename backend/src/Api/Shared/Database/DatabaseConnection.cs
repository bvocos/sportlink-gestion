using Npgsql;

namespace Api.Shared.Database;

public static class DatabaseConnection
{
    /// <summary>
    /// Devuelve la cadena de conexión a PostgreSQL. Usa ConnectionStrings:Default y, si está vacía,
    /// la variable DATABASE_URL (formato postgresql://usuario:clave@host:puerto/base que expone Railway).
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(configured)) return FromUrlIfNeeded(configured);
        var url = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url)) return FromUrlIfNeeded(url);
        throw new InvalidOperationException(
            "Falta la conexión a la base de datos: definí ConnectionStrings__Default o DATABASE_URL.");
    }

    public static string FromUrlIfNeeded(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
        };
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(parts[1], true, out var sslMode))
                builder.SslMode = sslMode;
        }
        return builder.ConnectionString;
    }

    /// <summary>
    /// Mantiene la semántica de fechas de SQL Server: DateTime se guarda como "timestamp without time zone"
    /// sin exigir DateTimeKind.Utc. Debe llamarse antes de crear la primera conexión.
    /// </summary>
    public static void UseLegacyTimestampBehavior() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}
