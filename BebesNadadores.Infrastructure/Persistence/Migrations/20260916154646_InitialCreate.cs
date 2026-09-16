using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Niveles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    EdadMinMeses = table.Column<int>(type: "integer", nullable: false),
                    EdadMaxMeses = table.Column<int>(type: "integer", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    CupoPredeterminado = table.Column<int>(type: "integer", nullable: false),
                    ProfesoresMinimos = table.Column<int>(type: "integer", nullable: false),
                    ProfesoresMaximos = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Niveles", x => x.Id);
                    table.CheckConstraint("CK_Niveles_Cupo", "\"CupoPredeterminado\" > 0");
                    table.CheckConstraint("CK_Niveles_Duracion", "\"DuracionMinutos\" > 0");
                    table.CheckConstraint("CK_Niveles_Edades", "\"EdadMinMeses\" >= 0 AND \"EdadMaxMeses\" >= \"EdadMinMeses\" AND \"EdadMaxMeses\" <= 84");
                });

            migrationBuilder.CreateTable(
                name: "Planes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VecesPorSemana = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planes", x => x.Id);
                    table.CheckConstraint("CK_Planes_VecesPorSemana", "\"VecesPorSemana\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Responsables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Dni = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WhatsApp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Domicilio = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responsables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bebes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    Sexo = table.Column<int>(type: "integer", nullable: false),
                    Dni = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FechaAlta = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaBaja = table.Column<DateOnly>(type: "date", nullable: true),
                    MotivoBaja = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NivelActualId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bebes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bebes_Niveles_NivelActualId",
                        column: x => x.NivelActualId,
                        principalTable: "Niveles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    NivelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cupo = table.Column<int>(type: "integer", nullable: false),
                    Sector = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                    table.CheckConstraint("CK_Turnos_Cupo", "\"Cupo\" > 0");
                    table.CheckConstraint("CK_Turnos_Duracion", "\"DuracionMinutos\" > 0");
                    table.ForeignKey(
                        name: "FK_Turnos_Niveles_NivelId",
                        column: x => x.NivelId,
                        principalTable: "Niveles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TarifasPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrecioMensual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VigenciaDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifasPlan", x => x.Id);
                    table.CheckConstraint("CK_TarifasPlan_Precio", "\"PrecioMensual\" >= 0");
                    table.ForeignKey(
                        name: "FK_TarifasPlan_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolesClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolesClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cajas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaAperturaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaCierreUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SaldoInicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoFinalDeclarado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UsuarioAperturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioCierreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cajas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cajas_Usuarios_UsuarioAperturaId",
                        column: x => x.UsuarioAperturaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cajas_Usuarios_UsuarioCierreId",
                        column: x => x.UsuarioCierreId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Profesores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profesores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Profesores_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuariosClaims_Usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UsuariosLogins_Usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UsuariosRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuariosRoles_Usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UsuariosTokens_Usuarios_UserId",
                        column: x => x.UserId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AptosMedicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaRevision = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaHasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Resultado = table.Column<int>(type: "integer", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MedicoUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentoNombreOriginal = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AptosMedicos", x => x.Id);
                    table.CheckConstraint("CK_AptosMedicos_Vigencia", "\"VigenciaHasta\" >= \"FechaRevision\"");
                    table.ForeignKey(
                        name: "FK_AptosMedicos_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AptosMedicos_Usuarios_MedicoUsuarioId",
                        column: x => x.MedicoUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BebesResponsables",
                columns: table => new
                {
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relacion = table.Column<int>(type: "integer", nullable: false),
                    EsContactoPrincipal = table.Column<bool>(type: "boolean", nullable: false),
                    AutorizaComunicaciones = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BebesResponsables", x => new { x.BebeId, x.ResponsableId });
                    table.ForeignKey(
                        name: "FK_BebesResponsables_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BebesResponsables_Responsables_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Responsables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inscripciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    MedioPagoPreferido = table.Column<int>(type: "integer", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inscripciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreferenciasRecuperacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    FranjaHoraria = table.Column<int>(type: "integer", nullable: false),
                    HoraDesde = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    HoraHasta = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferenciasRecuperacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreferenciasRecuperacion_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SesionesClase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    NivelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cupo = table.Column<int>(type: "integer", nullable: false),
                    Sector = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    MotivoCancelacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesClase", x => x.Id);
                    table.CheckConstraint("CK_SesionesClase_Cupo", "\"Cupo\" > 0");
                    table.CheckConstraint("CK_SesionesClase_Duracion", "\"DuracionMinutos\" > 0");
                    table.ForeignKey(
                        name: "FK_SesionesClase_Niveles_NivelId",
                        column: x => x.NivelId,
                        principalTable: "Niveles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionesClase_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MedioPago = table.Column<int>(type: "integer", nullable: false),
                    ReferenciaExterna = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.CheckConstraint("CK_Pagos_Importe", "\"Importe\" > 0");
                    table.ForeignKey(
                        name: "FK_Pagos_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Responsables_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Responsables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurnosProfesores",
                columns: table => new
                {
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfesorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnosProfesores", x => new { x.TurnoId, x.ProfesorId });
                    table.ForeignKey(
                        name: "FK_TurnosProfesores_Profesores_ProfesorId",
                        column: x => x.ProfesorId,
                        principalTable: "Profesores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnosProfesores_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cuotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuotas", x => x.Id);
                    table.CheckConstraint("CK_Cuotas_Importe", "\"Importe\" >= 0");
                    table.ForeignKey(
                        name: "FK_Cuotas_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InscripcionesTurnos",
                columns: table => new
                {
                    InscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscripcionesTurnos", x => new { x.InscripcionId, x.TurnoId });
                    table.ForeignKey(
                        name: "FK_InscripcionesTurnos_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InscripcionesTurnos_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Asistencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SesionClaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    EsRecupero = table.Column<bool>(type: "boolean", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RegistradaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RegistradaPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistencias_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asistencias_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asistencias_SesionesClase_SesionClaseId",
                        column: x => x.SesionClaseId,
                        principalTable: "SesionesClase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    MedioPago = table.Column<int>(type: "integer", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Concepto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AfectaSaldoEfectivo = table.Column<bool>(type: "boolean", nullable: false),
                    PagoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.CheckConstraint("CK_MovimientosCaja_Importe", "\"Importe\" > 0");
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImputacionesPago",
                columns: table => new
                {
                    PagoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImputacionesPago", x => new { x.PagoId, x.CuotaId });
                    table.CheckConstraint("CK_ImputacionesPago_Importe", "\"Importe\" > 0");
                    table.ForeignKey(
                        name: "FK_ImputacionesPago_Cuotas_CuotaId",
                        column: x => x.CuotaId,
                        principalTable: "Cuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImputacionesPago_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recuperaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SesionOrigenId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsistenciaOrigenId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaGeneracion = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    SesionUsoId = table.Column<Guid>(type: "uuid", nullable: true),
                    UtilizadaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recuperaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recuperaciones_Asistencias_AsistenciaOrigenId",
                        column: x => x.AsistenciaOrigenId,
                        principalTable: "Asistencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recuperaciones_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recuperaciones_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recuperaciones_SesionesClase_SesionOrigenId",
                        column: x => x.SesionOrigenId,
                        principalTable: "SesionesClase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recuperaciones_SesionesClase_SesionUsoId",
                        column: x => x.SesionUsoId,
                        principalTable: "SesionesClase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvitacionesVacante",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SesionClaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecuperacionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    EnviadaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondidaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VenceUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitacionesVacante", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitacionesVacante_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitacionesVacante_Recuperaciones_RecuperacionId",
                        column: x => x.RecuperacionId,
                        principalTable: "Recuperaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitacionesVacante_Responsables_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Responsables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitacionesVacante_SesionesClase_SesionClaseId",
                        column: x => x.SesionClaseId,
                        principalTable: "SesionesClase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListaEspera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    NivelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecuperacionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiaPreferido = table.Column<int>(type: "integer", nullable: true),
                    FranjaHoraria = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaAltaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListaEspera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Niveles_NivelId",
                        column: x => x.NivelId,
                        principalTable: "Niveles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Recuperaciones_RecuperacionId",
                        column: x => x.RecuperacionId,
                        principalTable: "Recuperaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AptosMedicos_BebeId_VigenciaHasta",
                table: "AptosMedicos",
                columns: new[] { "BebeId", "VigenciaHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_AptosMedicos_MedicoUsuarioId",
                table: "AptosMedicos",
                column: "MedicoUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_AptosMedicos_Resultado_VigenciaHasta",
                table: "AptosMedicos",
                columns: new[] { "Resultado", "VigenciaHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_BebeId_Estado",
                table: "Asistencias",
                columns: new[] { "BebeId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_InscripcionId",
                table: "Asistencias",
                column: "InscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_SesionClaseId_BebeId",
                table: "Asistencias",
                columns: new[] { "SesionClaseId", "BebeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bebes_Dni",
                table: "Bebes",
                column: "Dni",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bebes_Estado_Apellido_Nombre",
                table: "Bebes",
                columns: new[] { "Estado", "Apellido", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Bebes_NivelActualId",
                table: "Bebes",
                column: "NivelActualId");

            migrationBuilder.CreateIndex(
                name: "IX_BebesResponsables_ResponsableId",
                table: "BebesResponsables",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_Estado_FechaAperturaUtc",
                table: "Cajas",
                columns: new[] { "Estado", "FechaAperturaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_UsuarioAperturaId",
                table: "Cajas",
                column: "UsuarioAperturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_UsuarioCierreId",
                table: "Cajas",
                column: "UsuarioCierreId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_Estado_FechaVencimiento",
                table: "Cuotas",
                columns: new[] { "Estado", "FechaVencimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_InscripcionId_Periodo",
                table: "Cuotas",
                columns: new[] { "InscripcionId", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImputacionesPago_CuotaId",
                table: "ImputacionesPago",
                column: "CuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_BebeId_Estado",
                table: "Inscripciones",
                columns: new[] { "BebeId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_PlanId_Estado",
                table: "Inscripciones",
                columns: new[] { "PlanId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_InscripcionesTurnos_TurnoId",
                table: "InscripcionesTurnos",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesVacante_BebeId_Estado",
                table: "InvitacionesVacante",
                columns: new[] { "BebeId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesVacante_RecuperacionId",
                table: "InvitacionesVacante",
                column: "RecuperacionId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesVacante_ResponsableId",
                table: "InvitacionesVacante",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesVacante_SesionClaseId_Estado",
                table: "InvitacionesVacante",
                columns: new[] { "SesionClaseId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesVacante_Token",
                table: "InvitacionesVacante",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListaEspera_BebeId_Estado",
                table: "ListaEspera",
                columns: new[] { "BebeId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_ListaEspera_Estado_NivelId_DiaPreferido_FranjaHoraria",
                table: "ListaEspera",
                columns: new[] { "Estado", "NivelId", "DiaPreferido", "FranjaHoraria" });

            migrationBuilder.CreateIndex(
                name: "IX_ListaEspera_NivelId",
                table: "ListaEspera",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_ListaEspera_RecuperacionId",
                table: "ListaEspera",
                column: "RecuperacionId");

            migrationBuilder.CreateIndex(
                name: "IX_ListaEspera_TurnoId",
                table: "ListaEspera",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_CajaId_FechaUtc",
                table: "MovimientosCaja",
                columns: new[] { "CajaId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_PagoId",
                table: "MovimientosCaja",
                column: "PagoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_Tipo_Categoria_MedioPago",
                table: "MovimientosCaja",
                columns: new[] { "Tipo", "Categoria", "MedioPago" });

            migrationBuilder.CreateIndex(
                name: "IX_Niveles_Nombre",
                table: "Niveles",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Niveles_Orden",
                table: "Niveles",
                column: "Orden");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_BebeId_FechaUtc",
                table: "Pagos",
                columns: new[] { "BebeId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CajaId",
                table: "Pagos",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ReferenciaExterna",
                table: "Pagos",
                column: "ReferenciaExterna");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ResponsableId",
                table: "Pagos",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Planes_Nombre",
                table: "Planes",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreferenciasRecuperacion_BebeId_DiaSemana_FranjaHoraria_Act~",
                table: "PreferenciasRecuperacion",
                columns: new[] { "BebeId", "DiaSemana", "FranjaHoraria", "Activa" });

            migrationBuilder.CreateIndex(
                name: "IX_Profesores_Activo_Apellido_Nombre",
                table: "Profesores",
                columns: new[] { "Activo", "Apellido", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Profesores_UsuarioId",
                table: "Profesores",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_AsistenciaOrigenId",
                table: "Recuperaciones",
                column: "AsistenciaOrigenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_BebeId_Estado_FechaVencimiento",
                table: "Recuperaciones",
                columns: new[] { "BebeId", "Estado", "FechaVencimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_InscripcionId",
                table: "Recuperaciones",
                column: "InscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_SesionOrigenId",
                table: "Recuperaciones",
                column: "SesionOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_SesionUsoId",
                table: "Recuperaciones",
                column: "SesionUsoId");

            migrationBuilder.CreateIndex(
                name: "IX_Responsables_Dni",
                table: "Responsables",
                column: "Dni",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Responsables_Email",
                table: "Responsables",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesClaims_RoleId",
                table: "RolesClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClase_Fecha_NivelId_Estado",
                table: "SesionesClase",
                columns: new[] { "Fecha", "NivelId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClase_NivelId",
                table: "SesionesClase",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClase_TurnoId_Fecha",
                table: "SesionesClase",
                columns: new[] { "TurnoId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TarifasPlan_PlanId_VigenciaDesde",
                table: "TarifasPlan",
                columns: new[] { "PlanId", "VigenciaDesde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_DiaSemana_HoraInicio_NivelId_Estado",
                table: "Turnos",
                columns: new[] { "DiaSemana", "HoraInicio", "NivelId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_NivelId",
                table: "Turnos",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnosProfesores_ProfesorId",
                table: "TurnosProfesores",
                column: "ProfesorId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Usuarios",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Usuarios",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosClaims_UserId",
                table: "UsuariosClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosLogins_UserId",
                table: "UsuariosLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosRoles_RoleId",
                table: "UsuariosRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AptosMedicos");

            migrationBuilder.DropTable(
                name: "BebesResponsables");

            migrationBuilder.DropTable(
                name: "ImputacionesPago");

            migrationBuilder.DropTable(
                name: "InscripcionesTurnos");

            migrationBuilder.DropTable(
                name: "InvitacionesVacante");

            migrationBuilder.DropTable(
                name: "ListaEspera");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "PreferenciasRecuperacion");

            migrationBuilder.DropTable(
                name: "RolesClaims");

            migrationBuilder.DropTable(
                name: "TarifasPlan");

            migrationBuilder.DropTable(
                name: "TurnosProfesores");

            migrationBuilder.DropTable(
                name: "UsuariosClaims");

            migrationBuilder.DropTable(
                name: "UsuariosLogins");

            migrationBuilder.DropTable(
                name: "UsuariosRoles");

            migrationBuilder.DropTable(
                name: "UsuariosTokens");

            migrationBuilder.DropTable(
                name: "Cuotas");

            migrationBuilder.DropTable(
                name: "Recuperaciones");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "Profesores");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Asistencias");

            migrationBuilder.DropTable(
                name: "Cajas");

            migrationBuilder.DropTable(
                name: "Responsables");

            migrationBuilder.DropTable(
                name: "Inscripciones");

            migrationBuilder.DropTable(
                name: "SesionesClase");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Bebes");

            migrationBuilder.DropTable(
                name: "Planes");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropTable(
                name: "Niveles");
        }
    }
}
