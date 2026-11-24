using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Klinara.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicIdToPatientToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientTokens_Clinics_ClinicId",
                table: "PatientTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTokens_Patients_PatientId",
                table: "PatientTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeSlots_Clinics_ClinicId",
                table: "TimeSlots");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTokens_Clinics_ClinicId",
                table: "PatientTokens",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTokens_Patients_PatientId",
                table: "PatientTokens",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeSlots_Clinics_ClinicId",
                table: "TimeSlots",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientTokens_Clinics_ClinicId",
                table: "PatientTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTokens_Patients_PatientId",
                table: "PatientTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeSlots_Clinics_ClinicId",
                table: "TimeSlots");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTokens_Clinics_ClinicId",
                table: "PatientTokens",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTokens_Patients_PatientId",
                table: "PatientTokens",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeSlots_Clinics_ClinicId",
                table: "TimeSlots",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
