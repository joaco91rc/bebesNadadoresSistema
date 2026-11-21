using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;

namespace BebesNadadores.Web.Services.Facturas;

public sealed record DatosFactura(string Emisor, int Tipo, int Punto, long Numero, string Receptor, DateOnly Fecha, decimal Importe, string Cae);
public sealed class FacturaFila
{
    public Guid Id {get;set;}
    public string Emisor {get;set;} = "";
    public int Tipo {get;set;}
    public int Punto {get;set;}
    public long Numero {get;set;}
    public string Receptor {get;set;} = "";
    public DateOnly Fecha {get;set;}
    public decimal Importe {get;set;}
    public string Cae {get;set;} = "";
    public string Estado {get;set;} = "";
    public Guid? ResponsableId {get;set;}
    public Guid? IdentidadBebeId {get;set;}
    public Guid? BebeId {get;set;}
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public List<FacturaAlumnoVinculo> Alumnos {get;set;}=[];
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public bool Vinculada => !string.IsNullOrWhiteSpace(Destinatario) && (ResponsableId!=null || IdentidadBebeId!=null);
    public string? Destinatario {get;set;}
    public string? ResponsableNombre {get;set;}
    public DateTimeOffset? EnviadoUtc {get;set;}
    public bool Contrastada {get;set;}
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string Comprobante => $"{Tipo:000} {Punto:00000}-{Numero:00000000}";
}
public sealed record AlumnoFiscal(Guid Id,string Nombre,bool Activo);
public sealed record ResumenFiscalAlumno(Guid Id,string Nombre,bool Activo,List<IdentidadBebeFiscal> Identidades);
public sealed class FacturaAlumnoVinculo
{
    public Guid FacturaId {get;set;}
    public Guid BebeId {get;set;}
    public string Nombre {get;set;}="";
}
public sealed class IdentidadBebeFiscal
{
    public Guid Id {get;set;}
    public Guid BebeId {get;set;}
    public string Documento {get;set;}="";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string TipoDocumento {get;set;}="";
    public string Titular {get;set;}="";
    public string Email {get;set;}="";
    public bool Principal {get;set;}
    public bool Autorizado {get;set;}
    public Guid? ResponsableId {get;set;}
    public bool DestinoAutorizado {get;set;}
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool ListoParaFacturar => DestinoAutorizado && MailAddress.TryCreate(Email,out var direccion) && string.Equals(direccion.Address,Email.Trim(),StringComparison.OrdinalIgnoreCase);
}
public sealed record ResponsableFiscalLegajo(Guid Id,string Nombre,string Email,string Relacion,bool Activo,bool AutorizaComunicaciones,bool Principal);
public sealed record ContactoFiscal(Guid Id,string Nombre,string Documento,string Email);
public sealed record FilaArca(DateOnly Fecha,string Emisor,string Receptor,string Numero,decimal Importe);
public sealed record TrabajoFactura(Guid Id,string Destinatario,string Asunto,string Archivo,string PdfBase64);
public sealed class FacturasService(IDbContextFactory<AppDbContext> factory,IConfiguration config,ConfiguracionFiscalService configuracionFiscal)
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");
    public bool Habilitado => config.GetValue("Automatizaciones:Habilitadas",false) && config.GetValue("Facturas:HabilitarEnvios",false);
    public async Task<bool> EnviosHabilitadosAsync()
    {
        if(!Habilitado)return false;
        await using var db=await factory.CreateDbContextAsync();
        return await EnviosHabilitadosAsync(db);
    }
    private async Task<bool> EnviosHabilitadosAsync(AppDbContext db)
    {
        if(!Habilitado)return false;
        return await db.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS(SELECT 1 FROM "ConfiguracionComunicaciones" WHERE "Tipo"='facturas' AND "Habilitada") AS "Value"
            """).SingleAsync();
    }
    static string Digitos(string? s) => Regex.Replace(s ?? "", @"\D", "");
    static string Valor(string texto,string patron)
    {
        var m=Regex.Match(texto,patron,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(!m.Success) throw new InvalidDataException("No se reconocieron todos los datos fiscales del PDF.");
        return m.Groups[1].Value;
    }
    static decimal Importe(string s) => decimal.Parse(s.Trim().Replace("$", "").Trim(), NumberStyles.Number,Ar);
    static string? DniPersonal(string doc)
    {
        if(doc.Length!=11 || !new[]{"20","23","24","27"}.Contains(doc[..2]))return null;
        int[] pesos=[5,4,3,2,7,6,5,4,3,2];
        int resto=11-Enumerable.Range(0,10).Sum(i=>(doc[i]-'0')*pesos[i])%11;
        if(resto==11)resto=0;
        if(resto!=doc[10]-'0')return null;
        return doc.Substring(2,8).TrimStart('0');
    }
    public static DatosFactura LeerPdf(byte[] bytes)
    {
        using var doc=PdfDocument.Open(bytes);
        if(doc.NumberOfPages<1 || doc.NumberOfPages>60) throw new InvalidDataException("Cantidad de páginas no admitida.");
        DatosFactura? resultado=null;
        foreach(var page in doc.GetPages())
        {
            // Reconstruir filas por coordenadas, no por orden interno de los objetos PDF.
            var palabras=page.GetWords().OrderByDescending(w=>w.BoundingBox.Bottom).ToArray();
            var filas=new List<List<UglyToad.PdfPig.Content.Word>>();
            foreach(var w in palabras)
            {
                var fila=filas.LastOrDefault();
                if(fila==null || Math.Abs(fila[0].BoundingBox.Bottom-w.BoundingBox.Bottom)>3) {fila=[];filas.Add(fila);}
                fila.Add(w);
            }
            var texto=string.Join("\n",filas.Select(f=>string.Join(" ",f.OrderBy(w=>w.BoundingBox.Left).Select(w=>w.Text))));
            if(!Regex.IsMatch(texto,@"\bFACTURA\b",RegexOptions.IgnoreCase)) throw new InvalidDataException("Solo se admiten facturas; revisar otros comprobantes manualmente.");
            var tipo=int.Parse(Valor(texto,@"COD\.?\s*0*(\d+)"));
            if(tipo is not (1 or 6 or 11)) throw new InvalidDataException("Tipo de factura no admitido.");
            var docs=Regex.Matches(texto,@"(?:CUIT|CUIL|DNI)\s*:\s*([\d-]{7,13})",RegexOptions.IgnoreCase).Select(m=>Digitos(m.Groups[1].Value)).ToArray();
            if(docs.Length!=2 || docs[0].Length!=11 || docs[1].Length is not (7 or 8 or 11)) throw new InvalidDataException("No se identificó inequívocamente el documento receptor.");
            var datos=new DatosFactura(docs[0],tipo,int.Parse(Valor(texto,@"Punto\s+de\s+Venta\s*:\s*(\d+)")),long.Parse(Valor(texto,@"Comp\.\s*Nro\s*:\s*(\d+)")),docs[1],DateOnly.ParseExact(Valor(texto,@"Fecha\s+de\s+Emisi[oó]n\s*:\s*(\d{2}/\d{2}/\d{4})"),"dd/MM/yyyy",Ar),Importe(Valor(texto,@"Importe\s+Total\s*:\s*\$?\s*([\d.,]+)")),Valor(texto,@"CAE\s*N[°ºo]\s*:\s*(\d{14})"));
            if(resultado!=null && resultado!=datos) throw new InvalidDataException("El PDF contiene páginas de comprobantes distintos o no coincidentes.");
            resultado=datos;
        }
        return resultado!;
    }
    static string Normal(string s)=>new string(s.Normalize(System.Text.NormalizationForm.FormD).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    public static List<FilaArca> LeerListado(byte[] bytes,string nombre)
    {
        var filas=new List<string[]>();
        if(nombre.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase))
        {
            using var libro=new XLWorkbook(new MemoryStream(bytes));
            foreach(var r in libro.Worksheet(1).RowsUsed().Take(10001)) filas.Add(r.Cells(1,Math.Min(100,libro.Worksheet(1).LastColumnUsed()?.ColumnNumber()??1)).Select(c=>c.TryGetValue<DateTime>(out var d)&&c.DataType==XLDataType.DateTime?d.ToString("dd/MM/yyyy",Ar):c.GetFormattedString(Ar)).ToArray());
        }
        else
        {
            var contenido=System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            var cab=contenido.Split('\n')[0];
            using var parser=new TextFieldParser(new StringReader(contenido));
            parser.SetDelimiters(cab.Contains(';')?";":","); parser.HasFieldsEnclosedInQuotes=true;
            while(!parser.EndOfData && filas.Count<10001) filas.Add(parser.ReadFields()??[]);
        }
        for(int h=0;h<Math.Min(15,filas.Count);h++)
        {
            var cols=filas[h].Select(Normal).ToArray();
            int Col(params string[] candidatos)=>Array.FindIndex(cols,x=>candidatos.Contains(x));
            int fecha=Col("fecha","fechaemision"), emisor=Col("cuitemisor","cuitdelemisor"), receptor=Col("cuilreceptor","cuitreceptor","cuitcuilreceptor","nrodocreceptor","nrodocumentoreceptor","numerodocumentoreceptor","documentoreceptor"), numero=Col("nrocomprobante","numerocomprobante","comprobante","nrodecomprobante","nrocomprobantedesde","numerocomprobantedesde"), importe=Col("importe","importetotal","total");
            if(new[]{fecha,emisor,receptor,numero,importe}.Any(x=>x<0))continue;
            var result=new List<FilaArca>();
            foreach(var r in filas.Skip(h+1).Where(r=>r.Any(x=>!string.IsNullOrWhiteSpace(x))))
            {
                if(r.Length<=new[]{fecha,emisor,receptor,numero,importe}.Max())throw new InvalidDataException("Fila incompleta en el listado.");
                result.Add(new(DateOnly.Parse(r[fecha],Ar),Digitos(r[emisor]),Digitos(r[receptor]),r[numero].Trim(),Importe(r[importe])));
            }
            return result;
        }
        throw new InvalidDataException("No se reconocen las columnas del listado. Se necesitan Fecha, Nro comprobante, CUIT emisor, CUIL receptor e Importe.");
    }
    public async Task<List<ContactoFiscal>> ContactosAsync()
    {
        await using var db=await factory.CreateDbContextAsync();
        var filas=await db.Responsables.AsNoTracking().Where(r=>r.Activo && r.Bebes.Any(b=>b.EsContactoPrincipal && b.AutorizaComunicaciones)).Select(r=>new {r.Id,r.Nombre,r.Apellido,r.Dni,r.Email}).ToListAsync();
        return filas.Where(r=>CorreoValido(r.Email)).Select(r=>new ContactoFiscal(r.Id,r.Apellido+", "+r.Nombre,Digitos(r.Dni),r.Email.Trim())).OrderBy(r=>r.Nombre).ToList();
    }
    static bool CorreoValido(string? email)=>MailAddress.TryCreate(email,out var a)&&string.Equals(a.Address,email?.Trim(),StringComparison.OrdinalIgnoreCase);
    public async Task<List<IdentidadBebeFiscal>> IdentidadesAsync(Guid bebe)
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Database.SqlQuery<IdentidadBebeFiscal>($"SELECT * FROM \"BebesIdentidadesFiscalesVigentes\" WHERE \"BebeId\"={bebe}").ToListAsync();
    }
    public async Task<string?> NombreBebeAsync(Guid bebe)
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Bebes.Where(b=>b.Id==bebe).Select(b=>b.Nombre+" "+b.Apellido).SingleOrDefaultAsync();
    }
    public async Task<List<ResumenFiscalAlumno>> ResumenAlumnosAsync()
    {
        await using var db=await factory.CreateDbContextAsync();
        var alumnos=await db.Bebes.AsNoTracking().OrderBy(b=>b.Apellido).ThenBy(b=>b.Nombre)
            .Select(b=>new AlumnoFiscal(b.Id,b.Apellido+", "+b.Nombre,b.Estado==BebesNadadores.Domain.Enums.EstadoBebe.Activo)).ToListAsync();
        var identidades=(await db.Database.SqlQueryRaw<IdentidadBebeFiscal>("SELECT * FROM \"BebesIdentidadesFiscalesVigentes\"").ToListAsync()).ToLookup(x=>x.BebeId);
        return alumnos.Select(a=>new ResumenFiscalAlumno(a.Id,a.Nombre,a.Activo,identidades[a.Id].OrderByDescending(x=>x.Principal).ThenBy(x=>x.Titular).ToList())).ToList();
    }
    public async Task<List<ResponsableFiscalLegajo>> ResponsablesLegajoAsync(Guid bebe)
    {
        await using var db=await factory.CreateDbContextAsync();
        var filas=await db.BebesResponsables.AsNoTracking().Where(x=>x.BebeId==bebe)
            .Select(x=>new {x.ResponsableId,x.Responsable.Nombre,x.Responsable.Apellido,x.Responsable.Email,x.Relacion,x.Responsable.Activo,x.AutorizaComunicaciones,x.EsContactoPrincipal}).ToListAsync();
        return filas.OrderByDescending(x=>x.EsContactoPrincipal).ThenBy(x=>x.Apellido).Select(x=>new ResponsableFiscalLegajo(x.ResponsableId,(x.Nombre+" "+x.Apellido).Trim(),x.Email??"",x.Relacion.ToString(),x.Activo,x.AutorizaComunicaciones,x.EsContactoPrincipal)).ToList();
    }
    public async Task<List<AlumnoFiscal>> HermanosAsync(Guid bebe,Guid? responsable=null)
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Bebes.AsNoTracking().Where(b=>b.Id!=bebe && b.Responsables.Any(r=>(responsable==null||r.ResponsableId==responsable) && db.BebesResponsables.Any(o=>o.BebeId==bebe&&o.ResponsableId==r.ResponsableId)))
            .OrderBy(b=>b.Apellido).ThenBy(b=>b.Nombre).Select(b=>new AlumnoFiscal(b.Id,b.Apellido+", "+b.Nombre,b.Estado==BebesNadadores.Domain.Enums.EstadoBebe.Activo)).ToListAsync();
    }
    public async Task GuardarIdentidadAsync(IdentidadBebeFiscal x,IEnumerable<Guid>? copiarA=null)
    {
        var doc=DocumentoFiscalReceptor.SoloDigitos(x.Documento); var titular=x.Titular.Trim(); var email=x.Email.Trim();
        // Cada identidad fiscal es independiente: un auxiliar puede pertenecer a otro titular.
        // El tipo de documento se detecta por su longitud si no se indicó explícitamente.
        var tipo=string.IsNullOrWhiteSpace(x.TipoDocumento)?DocumentoFiscalReceptor.Tipo(doc):x.TipoDocumento;
        if(!DocumentoFiscalReceptor.Valido(doc,tipo))
            throw new InvalidOperationException(tipo=="DNI"
                ? "Ingresá un DNI válido de 7 u 8 dígitos."
                : "Ingresá un CUIL válido de 11 dígitos (con dígito verificador).");
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        // El titular del documento NO es necesariamente quien recibe el email.
        // El único destinatario de cada bebé es su contacto principal del legajo.
        if(x.ResponsableId is Guid responsable)
        {
            var fiscal=await db.BebesResponsables.AsNoTracking()
                .Where(r=>r.BebeId==x.BebeId && r.ResponsableId==responsable)
                .Select(r=>new {r.Responsable.Nombre,r.Responsable.Apellido,r.Responsable.Activo}).SingleOrDefaultAsync();
            if(fiscal==null && (x.Autorizado || x.Id==Guid.Empty ||
                !await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM \"BebesIdentidadesFiscales\" WHERE \"Id\"={x.Id} AND \"BebeId\"={x.BebeId} AND \"ResponsableId\"={responsable}) AS \"Value\"").SingleAsync()))
                throw new InvalidOperationException("El titular fiscal seleccionado ya no pertenece al legajo.");
            if(x.Autorizado && fiscal is { Activo: false })
                throw new InvalidOperationException("El titular fiscal debe estar activo en el legajo para autorizar documentos.");
            titular=""; // El nombre del titular fiscal se consulta desde el legajo.
        }
        else if(titular.Length==0||titular.Length>200)
            throw new InvalidOperationException("Ingresá el nombre del titular fiscal externo.");

        // El correo fiscal almacenado deja de utilizarse como destino.
        email="";
        if(x.Autorizado)
            await ValidarContactoPrincipalAsync(db,x.BebeId);
        if(x.Principal)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BebesIdentidadesFiscales\" SET \"Principal\"=false WHERE \"BebeId\"={x.BebeId}");
        if(x.Id==Guid.Empty)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BebesIdentidadesFiscales\" (\"Id\",\"BebeId\",\"Documento\",\"Titular\",\"Email\",\"Principal\",\"Autorizado\",\"ResponsableId\") VALUES ({Guid.NewGuid()},{x.BebeId},{doc},{titular},{email},{x.Principal},{x.Autorizado},{x.ResponsableId})");
        else if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BebesIdentidadesFiscales\" SET \"Titular\"={titular},\"Email\"={email},\"Principal\"={x.Principal},\"Autorizado\"={x.Autorizado},\"ResponsableId\"={x.ResponsableId} WHERE \"Id\"={x.Id} AND \"BebeId\"={x.BebeId} AND \"Documento\"={doc}")!=1)throw new InvalidOperationException("El documento no se puede cambiar; desautorizalo y agregá otro.");
        foreach(var hermano in (copiarA??Array.Empty<Guid>()).Distinct().Where(id=>id!=x.BebeId))
        {
            if(!await db.BebesResponsables.AnyAsync(r=>r.BebeId==hermano && (x.ResponsableId==null||r.ResponsableId==x.ResponsableId) && db.BebesResponsables.Any(o=>o.BebeId==x.BebeId&&o.ResponsableId==r.ResponsableId)))
                throw new InvalidOperationException("El alumno seleccionado no comparte el responsable requerido con este legajo.");
            if(x.Autorizado)
                await ValidarContactoPrincipalAsync(db,hermano);
            // No sobrescribir datos ni autorizaciones preexistentes en el hermano.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "BebesIdentidadesFiscales" ("Id","BebeId","Documento","Titular","Email","Principal","Autorizado","ResponsableId")
                VALUES ({Guid.NewGuid()},{hermano},{doc},{titular},{email},
                    {x.Principal} AND NOT EXISTS (SELECT 1 FROM "BebesIdentidadesFiscales" WHERE "BebeId"={hermano} AND "Principal"),{x.Autorizado},{x.ResponsableId})
                ON CONFLICT ("BebeId","Documento") DO NOTHING
                """);
        }
        await tx.CommitAsync();
    }
    private static async Task<string> ValidarContactoPrincipalAsync(AppDbContext db,Guid bebe)
    {
        var principales=await db.BebesResponsables.AsNoTracking()
            .Where(r=>r.BebeId==bebe && r.EsContactoPrincipal)
            .Select(r=>new {r.Responsable.Email,r.Responsable.Activo,r.AutorizaComunicaciones})
            .ToListAsync();
        if(principales.Count!=1)
            throw new InvalidOperationException("El alumno debe tener exactamente un contacto principal en su legajo.");
        var p=principales[0];
        if(!p.Activo || !p.AutorizaComunicaciones || !CorreoValido(p.Email))
            throw new InvalidOperationException("El contacto principal del alumno debe estar activo, autorizar comunicaciones y tener un correo válido. Completá su ficha antes de autorizar el documento fiscal.");
        return p.Email.Trim();
    }
    private static Task<List<IdentidadBebeFiscal>> CoincidenciasBebeAsync(AppDbContext db,string doc)
        => db.Database.SqlQuery<IdentidadBebeFiscal>($"SELECT * FROM \"BebesIdentidadesFiscalesVigentes\" WHERE \"Documento\"={doc}").ToListAsync();
    private static bool DestinosCoincidentes(List<IdentidadBebeFiscal> matches) => matches.Count>0 && matches.All(x=>x.ListoParaFacturar) && matches.Select(x=>x.Email.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1;
    private async Task<List<AlumnoFiscal>> CandidatosAsync(AppDbContext db,string receptor,Guid? identidad,Guid? responsable)
    {
        if(identidad!=null)
        {
            var coincidencias=await db.Database.SqlQuery<IdentidadBebeFiscal>($"SELECT * FROM \"BebesIdentidadesFiscalesVigentes\" WHERE \"Documento\"={receptor}").ToListAsync();
            var ids=coincidencias.Select(x=>x.BebeId).Distinct().ToArray();
            return await db.Bebes.AsNoTracking().Where(b=>ids.Contains(b.Id)).OrderBy(b=>b.Apellido).ThenBy(b=>b.Nombre)
                .Select(b=>new AlumnoFiscal(b.Id,b.Apellido+", "+b.Nombre,b.Estado==BebesNadadores.Domain.Enums.EstadoBebe.Activo)).ToListAsync();
        }
        if(responsable==null)return [];
        return await db.Bebes.AsNoTracking().Where(b=>b.Responsables.Any(r=>r.ResponsableId==responsable && r.EsContactoPrincipal && r.AutorizaComunicaciones))
            .OrderBy(b=>b.Apellido).ThenBy(b=>b.Nombre).Select(b=>new AlumnoFiscal(b.Id,b.Apellido+", "+b.Nombre,b.Estado==BebesNadadores.Domain.Enums.EstadoBebe.Activo)).ToListAsync();
    }
    public async Task<List<AlumnoFiscal>> CandidatosFacturaAsync(Guid id)
    {
        await using var db=await factory.CreateDbContextAsync();
        var f=await LeerFacturaAsync(db,id)??throw new InvalidOperationException("Factura no encontrada.");
        return await CandidatosAsync(db,f.Receptor,f.IdentidadBebeId,f.ResponsableId);
    }
    private static async Task ReemplazarAlumnosAsync(AppDbContext db,Guid factura,IEnumerable<Guid> alumnos)
    {
        var ids=alumnos.Distinct().ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"FacturasFiscalesBebes\" WHERE \"FacturaId\"={factura}");
        foreach(var id in ids)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"FacturasFiscalesBebes\" (\"FacturaId\",\"BebeId\") VALUES ({factura},{id})");
        Guid? unico=ids.Length==1?ids[0]:null;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"BebeId\"={unico} WHERE \"Id\"={factura}");
    }
    // El pago conserva el importe efectivamente cobrado (incluido cualquier descuento).
    // No adivinar por tarifa vigente, orden de importación ni cantidad de hermanos.
    private async Task<bool> AsociarAutomaticamenteAsync(AppDbContext db,Guid factura,string receptor,
        Guid? identidad,Guid? responsable,DateOnly fecha,decimal importe,ConfiguracionFiscalDatos? ajustes=null)
    {
        ajustes ??= await configuracionFiscal.ObtenerAsync(db);
        if(!ajustes.AsociacionAutomatica)return false;
        var opciones=await CandidatosAsync(db,receptor,identidad,responsable);
        if(opciones.Count==1)
        {
            await ReemplazarAlumnosAsync(db,factura,new[]{opciones[0].Id});
            return true;
        }
        if(opciones.Count<2 || importe<=0 || !ajustes.ConciliarHermanos)return false;
        var ids=opciones.Select(x=>x.Id).ToArray();
        // Día de emisión y días anteriores permitidos, en Argentina; no usar la fecha UTC directamente.
        var inicio=new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(-3)).ToUniversalTime();
        var fin=inicio.AddDays(1);
        inicio=inicio.AddDays(-ajustes.DiasTolerancia);
        var pagos=await db.Pagos.AsNoTracking()
            .Where(p=>ids.Contains(p.BebeId) && p.AnuladoUtc==null && p.Importe>0 && p.FechaUtc>=inicio && p.FechaUtc<fin)
            .OrderBy(p=>p.Id).Select(p=>new PagoFiscalCandidato(p.Id,p.BebeId,p.Importe)).Take(19).ToListAsync();
        var coincidencia=ConciliadorFacturas.BuscarUnica(pagos,importe);
        if(coincidencia==null)return false;
        var pagosIds=coincidencia.Select(p=>p.Id).ToArray();
        // No usar una segunda factura para el mismo pago; no eliminar pagos ya usados
        // de la búsqueda porque convertiría una ambigüedad en una falsa coincidencia.
        var usados=await db.Database.SqlQuery<Guid>($"""
            SELECT "PagoId" AS "Value" FROM "FacturasFiscalesPagos"
            WHERE "PagoId"=ANY({pagosIds}) AND "FacturaId"<>{factura}
            """).ToListAsync();
        if(usados.Count>0)return false;
        await ReemplazarAlumnosAsync(db,factura,coincidencia.Select(p=>p.BebeId));
        foreach(var pago in coincidencia)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "FacturasFiscalesPagos" ("FacturaId","PagoId") VALUES ({factura},{pago.Id})
                ON CONFLICT ("PagoId") DO NOTHING
                """);
        // Una importación concurrente puede haber reservado un pago de otro CUIT.
        // Si ocurrió, revertimos toda la asociación de esta factura.
        var reservados=await db.Database.SqlQuery<Guid>($"""
            SELECT "PagoId" AS "Value" FROM "FacturasFiscalesPagos" WHERE "FacturaId"={factura}
            """).ToListAsync();
        if(pagosIds.Any(id=>!reservados.Contains(id)))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"FacturasFiscalesPagos\" WHERE \"FacturaId\"={factura}");
            await ReemplazarAlumnosAsync(db,factura,Array.Empty<Guid>());
            return false;
        }
        return true;
    }
    public async Task GuardarAlumnosFacturaAsync(Guid factura,IEnumerable<Guid> alumnos)
    {
        var ids=alumnos.Distinct().ToArray();
        if(ids.Length==0)throw new InvalidOperationException("Seleccioná al menos un alumno incluido en este comprobante.");
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync();
        var f=await LeerFacturaAsync(db,factura,true)??throw new InvalidOperationException("Factura no encontrada.");
        if(f.Estado is not ("Pendiente" or "Enviado" or "Error") || !f.Vinculada)
            throw new InvalidOperationException("La factura no tiene un destinatario identificado o está en proceso de envío.");
        var opciones=await CandidatosAsync(db,f.Receptor,f.IdentidadBebeId,f.ResponsableId);
        if(ids.Any(id=>!opciones.Any(a=>a.Id==id)))throw new InvalidOperationException("Hay alumnos que no corresponden al titular fiscal de esta factura.");
        if(!ids.ToHashSet().SetEquals(f.Alumnos.Select(a=>a.BebeId)))
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"FacturasFiscalesPagos\" WHERE \"FacturaId\"={factura}");
        await ReemplazarAlumnosAsync(db,factura,ids);
        await tx.CommitAsync();
    }
    private async Task<bool> DestinoVigenteAsync(AppDbContext db,FacturaFila f)
    {
        if(f.Alumnos.Count==0)return false;
        var candidatos=await CandidatosAsync(db,f.Receptor,f.IdentidadBebeId,f.ResponsableId);
        if(f.Alumnos.Any(a=>!candidatos.Any(c=>c.Id==a.BebeId)))return false;
        var evidencia=await db.Database.SqlQuery<Guid>($"""
            SELECT "PagoId" AS "Value" FROM "FacturasFiscalesPagos" WHERE "FacturaId"={f.Id}
            """).ToListAsync();
        if(evidencia.Count>0)
        {
            var pagos=await db.Pagos.AsNoTracking().Where(p=>evidencia.Contains(p.Id) && p.AnuladoUtc==null)
                .Select(p=>new {p.BebeId,p.Importe}).ToListAsync();
            if(pagos.Count!=evidencia.Count || pagos.Sum(p=>p.Importe)!=f.Importe ||
                !pagos.Select(p=>p.BebeId).ToHashSet().SetEquals(f.Alumnos.Select(a=>a.BebeId)))return false;
        }
        return await ActualizarDestinoVigenteAsync(db,f);
    }
    // Sólo modifica la proyección en memoria. Se persiste al encolar o entregar a n8n,
    // nunca se sobreescribe el destinatario histórico de una factura enviada.
    private static async Task<bool> ActualizarDestinoVigenteAsync(AppDbContext db,FacturaFila f)
    {
        var matches=await CoincidenciasBebeAsync(db,f.Receptor);
        if(f.IdentidadBebeId!=null)
        {
            var titular=matches.SingleOrDefault(x=>x.Id==f.IdentidadBebeId);
            if(titular==null||!DestinosCoincidentes(matches))return false;
            if(f.Alumnos.Any(a=>!matches.Any(x=>x.BebeId==a.BebeId&&x.DestinoAutorizado)))return false;
            f.Destinatario=titular.Email.Trim(); f.ResponsableNombre=titular.Titular;
            return true;
        }
        if(matches.Count!=0||f.ResponsableId==null)return false;
        // Compatibilidad con asociaciones manuales antiguas: ningún comprobante
        // puede salir al correo de un titular fiscal auxiliar.
        var contacto=await db.Responsables.AsNoTracking()
            .Where(r=>r.Id==f.ResponsableId && r.Activo)
            .Select(r=>new {r.Email,r.Nombre,r.Apellido}).SingleOrDefaultAsync();
        if(contacto==null || !CorreoValido(contacto.Email))return false;
        foreach(var alumno in f.Alumnos)
        {
            var correo=await db.BebesResponsables.AsNoTracking()
                .Where(r=>r.BebeId==alumno.BebeId && r.EsContactoPrincipal &&
                          r.ResponsableId==f.ResponsableId && r.AutorizaComunicaciones && r.Responsable.Activo)
                .Select(r=>r.Responsable.Email).ToListAsync();
            if(correo.Count!=1 || !CorreoValido(correo[0]) ||
                !string.Equals(correo[0].Trim(),contacto.Email.Trim(),StringComparison.OrdinalIgnoreCase))return false;
        }
        f.Destinatario=contacto.Email.Trim();
        f.ResponsableNombre=(contacto.Nombre+" "+contacto.Apellido).Trim();
        return true;
    }
    public async Task<string> ImportarAsync(byte[] pdf,List<FilaArca>? listado)
    {
        var d=LeerPdf(pdf);
        var ajustes=await configuracionFiscal.ObtenerAsync();
        var emisorPermitido=ajustes.CuitEmisor;
        if(!ConfiguracionFiscalService.CuitValido(emisorPermitido))throw new InvalidDataException("Configurá un CUIT emisor válido en Configuración > Facturación fiscal.");
        if(d.Emisor!=emisorPermitido)throw new InvalidDataException("El CUIT emisor del PDF no coincide con el configurado.");
        var contrastada=false;
        if(listado!=null)
        {
            var coincidencias=listado.Where(x=>x.Emisor==d.Emisor && x.Receptor==d.Receptor && x.Fecha==d.Fecha && x.Importe==d.Importe && (long.TryParse(x.Numero,out var n)&&n==d.Numero || Digitos(x.Numero)==$"{d.Punto:00000}{d.Numero:00000000}")).ToList();
            if(coincidencias.Count!=1)throw new InvalidDataException("El PDF no tiene una coincidencia única en el listado ARCA.");
            contrastada=true;
        }
        await using var db=await factory.CreateDbContextAsync();
        var contactos=await ContactosAsync();
        var ids=await db.Database.SqlQuery<Guid>($"SELECT \"ResponsableId\" AS \"Value\" FROM \"IdentidadesFiscales\" WHERE \"Documento\"={d.Receptor}").ToListAsync();
        var dni=DniPersonal(d.Receptor);
        var todos=await db.Responsables.AsNoTracking().Select(r=>new {r.Id,r.Dni}).ToListAsync();
        var candidatos=todos.Where(r=>Digitos(r.Dni)==d.Receptor || ids.Contains(r.Id) || (dni!=null && (Digitos(r.Dni).Length is 7 or 8) && Digitos(r.Dni).TrimStart('0')==dni)).Select(r=>r.Id).Distinct().ToList();
        var c=candidatos.Count==1?contactos.SingleOrDefault(c=>c.Id==candidatos[0]):null;
        var fiscales=await CoincidenciasBebeAsync(db,d.Receptor);
        var fiscal=DestinosCoincidentes(fiscales) ? fiscales.OrderBy(x=>x.Id).First() : null;
        if(fiscales.Count>0)c=null;
        Guid? bebeFiscal=ajustes.AsociacionAutomatica && fiscal!=null && fiscales.Select(x=>x.BebeId).Distinct().Count()==1 ? fiscal.BebeId : null;
        var id=Guid.NewGuid(); var hash=Convert.ToHexString(SHA256.HashData(pdf));
        await using var tx=await db.Database.BeginTransactionAsync();
        var nfilas=await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "FacturasFiscales" ("Id","Emisor","Tipo","Punto","Numero","Receptor","Fecha","Importe","Cae","Pdf","Hash","ResponsableId","Destinatario","ResponsableNombre","Contrastada","IdentidadBebeId","BebeId")
            VALUES ({id},{d.Emisor},{d.Tipo},{d.Punto},{d.Numero},{d.Receptor},{d.Fecha},{d.Importe},{d.Cae},{pdf},{hash},{c?.Id},{fiscal?.Email??c?.Email},{fiscal?.Titular??c?.Nombre},{contrastada},{fiscal?.Id},{bebeFiscal}) ON CONFLICT ("Emisor","Tipo","Punto","Numero") DO NOTHING
            """);
        if(nfilas==1)
        {
            var asociada=await AsociarAutomaticamenteAsync(db,id,d.Receptor,fiscal?.Id,c?.Id,d.Fecha,d.Importe,ajustes);
            // Sólo las importaciones contrastadas con Excel/CSV pueden entrar automáticamente.
            // Sin habilitación, quedan pendientes (no se libera un lote histórico al activar).
            bool encolada=asociada && contrastada && d.Importe>0 && Habilitado && await EncolarUnaAsync(db,id);
            await tx.CommitAsync();
            if(encolada)return "Importada, asociada y en cola de envío";
            return asociada ? "Importada y asociada automáticamente (pendiente de envío)" : "Importada: requiere revisión de datos fiscales o pagos";
        }
        await tx.CommitAsync();
        var hashes=await db.Database.SqlQuery<string>($"SELECT \"Hash\" AS \"Value\" FROM \"FacturasFiscales\" WHERE \"Emisor\"={d.Emisor} AND \"Tipo\"={d.Tipo} AND \"Punto\"={d.Punto} AND \"Numero\"={d.Numero}").ToListAsync();
        return hashes.Count==1&&hashes[0]==hash?"Ya importada (mismo archivo)":"Comprobante ya existente con PDF diferente: revisar. No se reemplazó.";
    }
    private const string ColumnasFactura="""
        SELECT "Id","Emisor","Tipo","Punto","Numero","Receptor","Fecha","Importe","Cae","Estado","ResponsableId","Destinatario","ResponsableNombre","EnviadoUtc","Contrastada","IdentidadBebeId","BebeId" FROM "FacturasFiscales"
        """;
    private static async Task CargarAlumnosAsync(AppDbContext db,List<FacturaFila> filas)
    {
        if(filas.Count==0)return;
        var ids=filas.Select(f=>f.Id).ToArray();
        var alumnos=await db.Database.SqlQuery<FacturaAlumnoVinculo>($"""
            SELECT v."FacturaId",v."BebeId",b."Apellido" || ', ' || b."Nombre" AS "Nombre"
            FROM "FacturasFiscalesBebes" v JOIN "Bebes" b ON b."Id"=v."BebeId"
            WHERE v."FacturaId"=ANY({ids}) ORDER BY b."Apellido",b."Nombre"
            """).ToListAsync();
        var lookup=alumnos.ToLookup(a=>a.FacturaId);
        foreach(var f in filas)f.Alumnos=lookup[f.Id].ToList();
    }
    private static async Task<FacturaFila?> LeerFacturaAsync(AppDbContext db,Guid id,bool bloquear=false)
    {
        var filas= bloquear
            ? await db.Database.SqlQueryRaw<FacturaFila>(ColumnasFactura+" WHERE \"Id\"={0} FOR UPDATE",id).ToListAsync()
            : await db.Database.SqlQueryRaw<FacturaFila>(ColumnasFactura+" WHERE \"Id\"={0}",id).ToListAsync();
        await CargarAlumnosAsync(db,filas);
        return filas.SingleOrDefault();
    }
    public async Task<List<FacturaFila>> ListarAsync()
    {
        await using var db=await factory.CreateDbContextAsync();
        var filas=await db.Database.SqlQueryRaw<FacturaFila>(ColumnasFactura+" ORDER BY \"Fecha\" DESC,\"Numero\" DESC LIMIT 2000").ToListAsync();
        await CargarAlumnosAsync(db,filas);
        var pendientes=filas.Where(f=>f.Estado is "Pendiente" or "EnCola").Where(f=>f.IdentidadBebeId!=null).ToList();
        if(pendientes.Count>0)
        {
            var docs=pendientes.Select(f=>f.Receptor).Distinct().ToArray();
            var lookup=(await db.Database.SqlQuery<IdentidadBebeFiscal>($"SELECT * FROM \"BebesIdentidadesFiscalesVigentes\" WHERE \"Documento\"=ANY({docs})").ToListAsync()).ToLookup(x=>x.Documento);
            foreach(var f in pendientes)
            {
                var matches=lookup[f.Receptor].ToList(); var titular=matches.SingleOrDefault(x=>x.Id==f.IdentidadBebeId);
                if(titular!=null&&DestinosCoincidentes(matches)){f.Destinatario=titular.Email.Trim();f.ResponsableNombre=titular.Titular;}
                else{f.Destinatario=null;f.ResponsableNombre=null;}
            }
        }
        return filas;
    }
    public async Task<List<FacturaFila>> FacturasAlumnoAsync(Guid bebe)
    {
        await using var db=await factory.CreateDbContextAsync();
        var filas=await db.Database.SqlQueryRaw<FacturaFila>(ColumnasFactura+" WHERE \"Id\" IN (SELECT \"FacturaId\" FROM \"FacturasFiscalesBebes\" WHERE \"BebeId\"={0}) ORDER BY \"Fecha\" DESC,\"Numero\" DESC",bebe).ToListAsync();
        await CargarAlumnosAsync(db,filas);
        return filas;
    }
    public async Task VincularAsync(Guid factura,Guid responsable,bool recordar)
    {
        var c=(await ContactosAsync()).SingleOrDefault(x=>x.Id==responsable)??throw new InvalidOperationException("Contacto sin correo autorizado.");
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync();
        var docs=await db.Database.SqlQuery<string>($"SELECT \"Receptor\" AS \"Value\" FROM \"FacturasFiscales\" WHERE \"Id\"={factura} AND \"Estado\"='Pendiente' FOR UPDATE").ToListAsync();
        if(docs.Count!=1)throw new InvalidOperationException("La factura no está pendiente.");
        if((await CoincidenciasBebeAsync(db,docs[0])).Count>0)throw new InvalidOperationException("Este DNI/CUIL está registrado en Datos fiscales. Revisá los titulares y usá Buscar DNI/CUIL.");
        if(recordar)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"IdentidadesFiscales\" (\"Documento\",\"ResponsableId\") VALUES ({docs[0]},{responsable}) ON CONFLICT DO NOTHING");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"IdentidadBebeId\"=NULL,\"BebeId\"=NULL,\"ResponsableId\"={responsable},\"Destinatario\"={c.Email},\"ResponsableNombre\"={c.Nombre} WHERE \"Id\"={factura} AND \"Estado\"='Pendiente'");
        var f=await LeerFacturaAsync(db,factura);
        if(f!=null)await RevalidarAlumnosAsync(db,f,null,responsable);
        await tx.CommitAsync();
    }
    public async Task ReidentificarAsync(Guid id)
    {
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync();
        var f=await LeerFacturaAsync(db,id,true);
        if(f==null||f.Estado!="Pendiente")throw new InvalidOperationException("Factura no pendiente.");
        var m=await CoincidenciasBebeAsync(db,f.Receptor);
        if(!DestinosCoincidentes(m))throw new InvalidOperationException("No hay una coincidencia fiscal autorizada e inequívoca.");
        var x=m.OrderBy(x=>x.Id).First(); Guid? bebe=m.Select(x=>x.BebeId).Distinct().Count()==1?x.BebeId:null;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"IdentidadBebeId\"={x.Id},\"BebeId\"={bebe},\"ResponsableId\"=NULL,\"Destinatario\"={x.Email},\"ResponsableNombre\"={x.Titular} WHERE \"Id\"={id} AND \"Estado\"='Pendiente'");
        await RevalidarAlumnosAsync(db,f,x.Id,null);
        await tx.CommitAsync();
    }
    private async Task RevalidarAlumnosAsync(AppDbContext db,FacturaFila f,Guid? identidad,Guid? responsable)
    {
        var candidatos=await CandidatosAsync(db,f.Receptor,identidad,responsable);
        if(f.Alumnos.Count>0 && f.Alumnos.All(a=>candidatos.Any(c=>c.Id==a.BebeId)))
        {
            await ReemplazarAlumnosAsync(db,f.Id,f.Alumnos.Select(a=>a.BebeId));
            return;
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"FacturasFiscalesPagos\" WHERE \"FacturaId\"={f.Id}");
        await ReemplazarAlumnosAsync(db,f.Id,Array.Empty<Guid>());
        await AsociarAutomaticamenteAsync(db,f.Id,f.Receptor,identidad,responsable,f.Fecha,f.Importe);
    }
    public async Task<int> EncolarAsync(IEnumerable<Guid> ids)
    {
        if(!await EnviosHabilitadosAsync())return 0;
        int total=0;
        foreach(var id in ids.Distinct())if(await EncolarUnaAsync(id))total++;
        return total;
    }
    private async Task<bool> EncolarUnaAsync(Guid id)
    {
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync();
        var resultado=await EncolarUnaAsync(db,id);
        await tx.CommitAsync();
        return resultado;
    }
    private async Task<bool> EncolarUnaAsync(AppDbContext db,Guid id)
    {
        if(!await EnviosHabilitadosAsync(db))return false;
        var f=await LeerFacturaAsync(db,id,true);
        if(f==null || f.Estado!="Pendiente" || !await DestinoVigenteAsync(db,f))return false;
        return (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"Estado\"='EnCola',\"Destinatario\"={f.Destinatario},\"ResponsableNombre\"={f.ResponsableNombre} WHERE \"Id\"={id} AND \"Estado\"='Pendiente' AND \"ResponsableId\" IS NOT DISTINCT FROM {f.ResponsableId} AND \"IdentidadBebeId\" IS NOT DISTINCT FROM {f.IdentidadBebeId} AND EXISTS (SELECT 1 FROM \"FacturasFiscalesBebes\" WHERE \"FacturaId\"={id}) AND EXISTS (SELECT 1 FROM \"ConfiguracionComunicaciones\" WHERE \"Tipo\"='facturas' AND \"Habilitada\")"))==1;
    }
    public async Task<byte[]?> PdfAsync(Guid id)
    {
        await using var db=await factory.CreateDbContextAsync();
        return (await db.Database.SqlQuery<byte[]>($"SELECT \"Pdf\" AS \"Value\" FROM \"FacturasFiscales\" WHERE \"Id\"={id}").ToListAsync()).SingleOrDefault();
    }
    public async Task<TrabajoFactura?> TomarAsync()
    {
        if(!Habilitado)return null;
        await using var db=await factory.CreateDbContextAsync();
        var ids=await db.Database.SqlQueryRaw<Guid>("""
            UPDATE "FacturasFiscales" SET "Estado"='EnProceso' WHERE "Id"=(SELECT "Id" FROM "FacturasFiscales" WHERE "Estado"='EnCola' AND EXISTS (SELECT 1 FROM "ConfiguracionComunicaciones" WHERE "Tipo"='facturas' AND "Habilitada") ORDER BY "Fecha","Numero" LIMIT 1 FOR UPDATE SKIP LOCKED) RETURNING "Id" AS "Value"
            """).ToListAsync();
        if(ids.Count==0)return null;
        var id=ids[0]; var f=await LeerFacturaAsync(db,id);
        if(f==null || !await DestinoVigenteAsync(db,f))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"Estado\"='Pendiente' WHERE \"Id\"={id} AND \"Estado\"='EnProceso'");
            return null;
        }
        var pdf=await PdfAsync(id); if(pdf==null){await ResultadoAsync(id,false);return null;}
        if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"Destinatario\"={f.Destinatario},\"ResponsableNombre\"={f.ResponsableNombre} WHERE \"Id\"={id} AND \"Estado\"='EnProceso'")!=1)return null;
        return new(id,f.Destinatario!, $"Bebés Nadadores - Factura {f.Comprobante}", $"Factura_{f.Punto:00000}_{f.Numero:00000000}.pdf",Convert.ToBase64String(pdf));
    }
    public async Task ResultadoAsync(Guid id,bool enviado)
    {
        await using var db=await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"Estado\"={(enviado?"Enviado":"Error")},\"EnviadoUtc\"=CASE WHEN {enviado} THEN CURRENT_TIMESTAMP ELSE NULL END WHERE \"Id\"={id} AND \"Estado\"='EnProceso'");
    }
}
