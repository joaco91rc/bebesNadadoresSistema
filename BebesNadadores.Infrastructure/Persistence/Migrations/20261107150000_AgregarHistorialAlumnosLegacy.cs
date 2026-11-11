using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace BebesNadadores.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261107150000_AgregarHistorialAlumnosLegacy")]
public sealed class AgregarHistorialAlumnosLegacy : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name:"HistorialAlumnosLegacy", columns:t=>new
        {
            Id=t.Column<Guid>(type:"uuid",nullable:false),
            LegacyPersonaId=t.Column<long>(type:"bigint",nullable:false),
            BebeId=t.Column<Guid>(type:"uuid",nullable:true),
            NombreOriginal=t.Column<string>(type:"character varying(250)",maxLength:250,nullable:false),
            Nombre=t.Column<string>(type:"character varying(120)",maxLength:120,nullable:false),
            Apellido=t.Column<string>(type:"character varying(120)",maxLength:120,nullable:false),
            FechaNacimiento=t.Column<DateOnly>(type:"date",nullable:true),
            FechaPrimeraInscripcion=t.Column<DateOnly>(type:"date",nullable:true)
        },constraints:t=>
        {
            t.PrimaryKey("PK_HistorialAlumnosLegacy",x=>x.Id);
            t.ForeignKey("FK_HistorialAlumnosLegacy_Bebes_BebeId",x=>x.BebeId,"Bebes","Id",onDelete:ReferentialAction.SetNull);
        });
        m.CreateTable(name:"HistorialAlumnosAniosLegacy",columns:t=>new
        {
            Id=t.Column<Guid>(type:"uuid",nullable:false),
            HistorialAlumnoLegacyId=t.Column<Guid>(type:"uuid",nullable:false),
            Anio=t.Column<int>(type:"integer",nullable:false),
            PrimeraClase=t.Column<DateOnly>(type:"date",nullable:false),
            UltimaClase=t.Column<DateOnly>(type:"date",nullable:false),
            CantidadRegistros=t.Column<int>(type:"integer",nullable:false),
            Niveles=t.Column<string>(type:"character varying(500)",maxLength:500,nullable:true),
            FrecuenciaEstimada=t.Column<int>(type:"integer",nullable:true)
        },constraints:t=>
        {
            t.PrimaryKey("PK_HistorialAlumnosAniosLegacy",x=>x.Id);
            t.ForeignKey("FK_HistorialAlumnosAniosLegacy_HistorialAlumnosLegacy_HistorialAlumnoLegacyId",x=>x.HistorialAlumnoLegacyId,"HistorialAlumnosLegacy","Id",onDelete:ReferentialAction.Cascade);
        });
        m.CreateIndex(name:"IX_HistorialAlumnosLegacy_LegacyPersonaId",table:"HistorialAlumnosLegacy",column:"LegacyPersonaId",unique:true);
        m.CreateIndex(name:"IX_HistorialAlumnosLegacy_BebeId",table:"HistorialAlumnosLegacy",column:"BebeId");
        m.CreateIndex(name:"IX_HistorialAlumnosLegacy_Apellido_Nombre",table:"HistorialAlumnosLegacy",columns:new[]{"Apellido","Nombre"});
        m.CreateIndex(name:"IX_HistorialAlumnosAniosLegacy_HistorialAlumnoLegacyId_Anio",table:"HistorialAlumnosAniosLegacy",columns:new[]{"HistorialAlumnoLegacyId","Anio"},unique:true);
    }
    protected override void Down(MigrationBuilder m)
    {m.DropTable("HistorialAlumnosAniosLegacy");m.DropTable("HistorialAlumnosLegacy");}
}
