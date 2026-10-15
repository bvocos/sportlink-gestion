namespace Api.Tests;

internal static class SqlIntegrationEnvironment
{
    public static string? ConnectionOrSkip()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(connection)) return connection;

        var message = "SQL integration test skipped locally: TEST_SQL_CONNECTION is not defined.";
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "CI=true requires TEST_SQL_CONNECTION; SQL integration tests cannot be skipped in CI.");

        Console.WriteLine(message);
        return null;
    }
}
