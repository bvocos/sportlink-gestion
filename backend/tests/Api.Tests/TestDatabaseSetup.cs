using System.Runtime.CompilerServices;
using Api.Shared.Database;

namespace Api.Tests;

internal static class TestDatabaseSetup
{
    // Igual que Program: DateTime se guarda como timestamp sin zona horaria (semántica de SQL Server).
    [ModuleInitializer]
    internal static void Initialize() => DatabaseConnection.UseLegacyTimestampBehavior();
}
