using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAircraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Registration = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SeatCapacity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.Id);
                    table.CheckConstraint("CK_Aircraft_SeatCapacity", "\"SeatCapacity\" >= 1 AND \"SeatCapacity\" <= 500");
                });

            migrationBuilder.InsertData(
                table: "Aircraft",
                columns: new[] { "Id", "Registration", "Model", "SeatCapacity" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222201"), "RA-0001", "Резервное судно", 500 });

            migrationBuilder.AddColumn<Guid>(
                name: "AircraftId",
                table: "Flights",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("22222222-2222-2222-2222-222222222201"));

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights",
                column: "AircraftId");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_Registration",
                table: "Aircraft",
                column: "Registration",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Aircraft_AircraftId",
                table: "Flights",
                column: "AircraftId",
                principalTable: "Aircraft",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Aircraft_AircraftId",
                table: "Flights");

            migrationBuilder.DropTable(
                name: "Aircraft");

            migrationBuilder.DropIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "AircraftId",
                table: "Flights");
        }
    }
}
