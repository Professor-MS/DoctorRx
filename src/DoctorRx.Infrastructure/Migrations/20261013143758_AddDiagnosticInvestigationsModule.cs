using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorRx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosticInvestigationsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvestigationCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Subcategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CommonAbbreviation = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SampleTypeOrModality = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    DefaultInstructions = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsCustom = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ClinicalNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DoctorSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    PatientSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, defaultValue: "Active"),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestigationRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    InvestigationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Subcategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BodyPartOrSite = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ViewOrProtocol = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    SpecificInstructions = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestigationRequestItems_InvestigationCatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "InvestigationCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvestigationRequestItems_InvestigationRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "InvestigationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationCatalogItems_Category",
                table: "InvestigationCatalogItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationCatalogItems_CommonAbbreviation",
                table: "InvestigationCatalogItems",
                column: "CommonAbbreviation");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationCatalogItems_IsActive",
                table: "InvestigationCatalogItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationCatalogItems_Name",
                table: "InvestigationCatalogItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationCatalogItems_NormalizedName",
                table: "InvestigationCatalogItems",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationCatalogItems_UsageCount",
                table: "InvestigationCatalogItems",
                column: "UsageCount");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequestItems_CatalogItemId",
                table: "InvestigationRequestItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequestItems_RequestId",
                table: "InvestigationRequestItems",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequests_CreatedAtUtc",
                table: "InvestigationRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequests_PatientId",
                table: "InvestigationRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequests_RequestDate",
                table: "InvestigationRequests",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequests_RequestNumber",
                table: "InvestigationRequests",
                column: "RequestNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestigationRequestItems");

            migrationBuilder.DropTable(
                name: "InvestigationCatalogItems");

            migrationBuilder.DropTable(
                name: "InvestigationRequests");
        }
    }
}
