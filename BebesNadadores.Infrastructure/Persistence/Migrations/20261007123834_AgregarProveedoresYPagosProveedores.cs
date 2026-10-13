using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProveedoresYPagosProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PagoProveedorId",
                table: "MovimientosCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PagoProveedorId",
                table: "MovimientosBancarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RazonSocial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NombreFantasia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Cuit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PagosProveedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProveedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MedioPago = table.Column<int>(type: "integer", nullable: false),
                    Concepto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NumeroComprobante = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenciaExterna = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsuarioRegistroId = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CuentaBancariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosProveedores", x => x.Id);
                    table.CheckConstraint("CK_PagosProveedores_Importe", "\"Importe\" > 0");
                    table.ForeignKey(
                        name: "FK_PagosProveedores_AspNetUsers_UsuarioRegistroId",
                        column: x => x.UsuarioRegistroId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosProveedores_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosProveedores_CuentasBancarias_CuentaBancariaId",
                        column: x => x.CuentaBancariaId,
                        principalTable: "CuentasBancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosProveedores_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_PagoProveedorId",
                table: "MovimientosCaja",
                column: "PagoProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosBancarios_PagoProveedorId",
                table: "MovimientosBancarios",
                column: "PagoProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosProveedores_CajaId_FechaUtc",
                table: "PagosProveedores",
                columns: new[] { "CajaId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PagosProveedores_CuentaBancariaId_FechaUtc",
                table: "PagosProveedores",
                columns: new[] { "CuentaBancariaId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PagosProveedores_ProveedorId_FechaUtc",
                table: "PagosProveedores",
                columns: new[] { "ProveedorId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PagosProveedores_UsuarioRegistroId",
                table: "PagosProveedores",
                column: "UsuarioRegistroId");

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Activo_RazonSocial",
                table: "Proveedores",
                columns: new[] { "Activo", "RazonSocial" });

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Cuit",
                table: "Proveedores",
                column: "Cuit",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosBancarios_PagosProveedores_PagoProveedorId",
                table: "MovimientosBancarios",
                column: "PagoProveedorId",
                principalTable: "PagosProveedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_PagosProveedores_PagoProveedorId",
                table: "MovimientosCaja",
                column: "PagoProveedorId",
                principalTable: "PagosProveedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosBancarios_PagosProveedores_PagoProveedorId",
                table: "MovimientosBancarios");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_PagosProveedores_PagoProveedorId",
                table: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "PagosProveedores");

            migrationBuilder.DropTable(
                name: "Proveedores");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_PagoProveedorId",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosBancarios_PagoProveedorId",
                table: "MovimientosBancarios");

            migrationBuilder.DropColumn(
                name: "PagoProveedorId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "PagoProveedorId",
                table: "MovimientosBancarios");
        }
    }
}
