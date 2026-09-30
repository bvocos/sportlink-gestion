using Api.Features.Auth;
using Api.Shared.Database;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class AuthPermissionsTests
{
    [Fact]
    public void ReadPermissions_ReadsLegacyArray()
    {
        var user = User("[\"ventas\",\"clientes\"]");

        var permissions = AuthEndpoints.ReadPermissions(user, NullLogger.Instance);

        Assert.Equal(["ventas", "clientes"], permissions);
    }

    [Fact]
    public void ReadPermissions_ConvertsPermissionObjectToVisibleModules()
    {
        var user = User("""
            {
              "ventas": { "ver": true, "crear": false },
              "clientes": { "ver": false },
              "dashboard": true
            }
            """);

        var permissions = AuthEndpoints.ReadPermissions(user, NullLogger.Instance);

        Assert.Equal(["ventas", "dashboard"], permissions);
    }

    [Fact]
    public void ReadPermissions_ReturnsEmptyForInvalidJson()
    {
        var user = User("not-json");

        var permissions = AuthEndpoints.ReadPermissions(user, NullLogger.Instance);

        Assert.Empty(permissions);
    }

    private static Usuario User(string permissionsJson) => new()
    {
        Id = Guid.NewGuid(),
        PermisosJson = permissionsJson
    };
}
