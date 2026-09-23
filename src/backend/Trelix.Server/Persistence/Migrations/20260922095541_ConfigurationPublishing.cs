using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trelix.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurationPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Releases_Releases_ConfigFileId_SourceVersion",
                table: "Releases");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "Environments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddForeignKey(
                name: "FK_Releases_Releases_ConfigFileId_SourceVersion",
                table: "Releases",
                columns: new[] { "ConfigFileId", "SourceVersion" },
                principalTable: "Releases",
                principalColumns: new[] { "ConfigFileId", "Version" });

            // 已有资源使用稳定且非空的 ID 初始化并发基准，后续写入会刷新为新标记。
            migrationBuilder.Sql("UPDATE Projects SET ConcurrencyStamp = Id;");
            migrationBuilder.Sql("UPDATE Environments SET ConcurrencyStamp = Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Releases_Releases_ConfigFileId_SourceVersion",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Environments");

            migrationBuilder.AddForeignKey(
                name: "FK_Releases_Releases_ConfigFileId_SourceVersion",
                table: "Releases",
                columns: new[] { "ConfigFileId", "SourceVersion" },
                principalTable: "Releases",
                principalColumns: new[] { "ConfigFileId", "Version" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
