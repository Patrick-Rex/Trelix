using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trelix.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Administrators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Administrators", x => x.Id);
                    table.CheckConstraint("CK_Administrators_Singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "ApplicationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RevokedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationTokens", x => x.Id);
                    table.CheckConstraint("CK_ApplicationTokens_Expiry", "ExpiresAt > CreatedAt");
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdministratorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AdministratorId = table.Column<int>(type: "INTEGER", nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdministratorSessions_Administrators_AdministratorId",
                        column: x => x.AdministratorId,
                        principalTable: "Administrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Environments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Environments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokenScopes",
                columns: table => new
                {
                    ApplicationTokenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenScopes", x => new { x.ApplicationTokenId, x.EnvironmentId });
                    table.ForeignKey(
                        name: "FK_TokenScopes_ApplicationTokens_ApplicationTokenId",
                        column: x => x.ApplicationTokenId,
                        principalTable: "ApplicationTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TokenScopes_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfigFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DraftJson = table.Column<string>(type: "TEXT", nullable: true),
                    DraftRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentReleaseVersion = table.Column<long>(type: "INTEGER", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigFiles", x => x.Id);
                    table.CheckConstraint("CK_ConfigFiles_Draft", "(DraftJson IS NULL AND DraftRevision = 0) OR (DraftJson IS NOT NULL AND json_valid(DraftJson) AND DraftRevision > 0)");
                    table.ForeignKey(
                        name: "FK_ConfigFiles_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Releases",
                columns: table => new
                {
                    ConfigFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DraftRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceVersion = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => new { x.ConfigFileId, x.Version });
                    table.CheckConstraint("CK_Releases_Json", "json_valid(Json)");
                    table.CheckConstraint("CK_Releases_Source", "SourceVersion IS NULL OR SourceVersion < Version");
                    table.CheckConstraint("CK_Releases_Version", "Version > 0 AND DraftRevision > 0");
                    table.ForeignKey(
                        name: "FK_Releases_ConfigFiles_ConfigFileId",
                        column: x => x.ConfigFileId,
                        principalTable: "ConfigFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Releases_Releases_ConfigFileId_SourceVersion",
                        columns: x => new { x.ConfigFileId, x.SourceVersion },
                        principalTable: "Releases",
                        principalColumns: new[] { "ConfigFileId", "Version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorSessions_AdministratorId",
                table: "AdministratorSessions",
                column: "AdministratorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorSessions_ExpiresAt",
                table: "AdministratorSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationTokens_ExpiresAt",
                table: "ApplicationTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationTokens_SecretHash",
                table: "ApplicationTokens",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigFiles_EnvironmentId_Name",
                table: "ConfigFiles",
                columns: new[] { "EnvironmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigFiles_Id_CurrentReleaseVersion",
                table: "ConfigFiles",
                columns: new[] { "Id", "CurrentReleaseVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Environments_ProjectId_Key",
                table: "Environments",
                columns: new[] { "ProjectId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ConfigFileId_SourceVersion",
                table: "Releases",
                columns: new[] { "ConfigFileId", "SourceVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Releases_PublishedAt",
                table: "Releases",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TokenScopes_EnvironmentId",
                table: "TokenScopes",
                column: "EnvironmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConfigFiles_Releases_Id_CurrentReleaseVersion",
                table: "ConfigFiles",
                columns: new[] { "Id", "CurrentReleaseVersion" },
                principalTable: "Releases",
                principalColumns: new[] { "ConfigFileId", "Version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConfigFiles_Environments_EnvironmentId",
                table: "ConfigFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ConfigFiles_Releases_Id_CurrentReleaseVersion",
                table: "ConfigFiles");

            migrationBuilder.DropTable(
                name: "AdministratorSessions");

            migrationBuilder.DropTable(
                name: "TokenScopes");

            migrationBuilder.DropTable(
                name: "Administrators");

            migrationBuilder.DropTable(
                name: "ApplicationTokens");

            migrationBuilder.DropTable(
                name: "Environments");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Releases");

            migrationBuilder.DropTable(
                name: "ConfigFiles");
        }
    }
}
