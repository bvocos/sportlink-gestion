using Api.Shared.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202609080001_MigratePermissionsMatrix")]
public sealed class MigratePermissionsMatrix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
