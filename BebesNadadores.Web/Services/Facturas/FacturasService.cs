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
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public bool Vinculada => !string.IsNullOrWhiteSpace(Destinatario) && (ResponsableId!=null || IdentidadBebeId!=null);
    public string? Destinatario {get;set;}
    public string? ResponsableNombre {get;set;}
    public DateTimeOffset? EnviadoUtc {get;set;}
    public bool Contrastada {get;set;}
    [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string Comprobante => $"{Tipo:000} {Punto:00000}-{Numero:00000000}";
}
public sealed class IdentidadBebeFiscal
{
    public Guid Id {get;set;}
    public Guid BebeId {get;set;}
    public string Documento {get;set;}="";
    public string Titular {get;set;}="";
    public string Email {get;set;}="";
    public bool Principal {get;set;}
    public bool Autorizado {get;set;}
}
public sealed record ContactoFiscal(Guid Id,string Nombre,string Documento,string Email);
public sealed record FilaArca(DateOnly Fecha,string Emisor,string Receptor,string Numero,decimal Importe);
public sealed record TrabajoFactura(Guid Id,string Destinatario,string Asunto,string Archivo,string PdfBase64);
public sealed class FacturasService(IDbContextFactory<AppDbContext> factory,IConfiguration config)
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");
    public bool Habilitado => config.GetValue("Facturas:HabilitarEnvios",false);
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
        var filas=await db.Responsables.AsNoTracking().Where(r=>r.Activo && r.Bebes.Any(b=>b.AutorizaComunicaciones)).Select(r=>new {r.Id,r.Nombre,r.Apellido,r.Dni,r.Email}).ToListAsync();
        return filas.Where(r=>CorreoValido(r.Email)).Select(r=>new ContactoFiscal(r.Id,r.Apellido+", "+r.Nombre,Digitos(r.Dni),r.Email.Trim())).OrderBy(r=>r.Nombre).ToList();
    }
    static bool CorreoValido(string? email)=>MailAddress.TryCreate(email,out var a)&&string.Equals(a.Address,email?.Trim(),StringComparison.OrdinalIgnoreCase);
    public async Task<List<IdentidadBebeFiscal>> IdentidadesAsync(Guid bebe)
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Database.SqlQuery<IdentidadBebeFiscal>($"SELECT * FROM \"BebesIdentidadesFiscales\" WHERE \"BebeId\"={bebe}").ToListAsync();
    }
    public async Task<string?> NombreBebeAsync(Guid bebe)
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Bebes.Where(b=>b.Id==bebe).Select(b=>b.Nombre+" "+b.Apellido).SingleOrDefaultAsync();
    }
    static bool CuitValido(string doc)
    {
        if(doc.Length!=11 || doc.Any(c=>c<'0'||c>'9'))return false;
        int[] pesos=[5,4,3,2,7,6,5,4,3,2];
        var d=11-Enumerable.Range(0,10).Sum(i=>(doc[i]-'0')*pesos[i])%11;
        return (d==11?0:d)==doc[10]-'0';
    }
    public async Task GuardarIdentidadAsync(IdentidadBebeFiscal x)
    {
        var doc=Digitos(x.Documento); var titular=x.Titular.Trim(); var email=x.Email.Trim();
        if(!CuitValido(doc)||titular.Length==0||titular.Length>200)throw new InvalidOperationException("Ingresá un CUIT/CUIL válido y el nombre del titular.");
        if((email.Length>0&&!CorreoValido(email))||(x.Autorizado&&email.Length==0))throw new InvalidOperationException("Ingresá un correo válido antes de autorizar envíos.");
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if(x.Principal)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BebesIdentidadesFiscales\" SET \"Principal\"=false WHERE \"BebeId\"={x.BebeId}");
        if(x.Id==Guid.Empty)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BebesIdentidadesFiscales\" (\"Id\",\"BebeId\",\"Documento\",\"Titular\",\"Email\",\"Principal\",\"Autorizado\") VALUES ({Guid.NewGuid()},{x.BebeId},{doc},{titular},{email},{x.Principal},{x.Autorizado})");
        else if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BebesIdentidadesFiscales\" SET \"Titular\"={titular},\"Email\"={email},\"Principal\"={x.Principal},\"Autorizado\"={x.Autorizado} WHERE \"Id\"={x.Id} AND \"BebeId\"={x.BebeId} AND \"Documento\"={doc}")!=1)throw new InvalidOperationException("El documento no se puede cambiar; desautorizalo y agregá otro.");
        await tx.CommitAsync();
    }
    private async Task<List<IdentidadBebeFiscal>> CoincidenciasBebeAsync(string doc)
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Database.SqlQuery<IdentidadBebeFiscal>($"SELECT * FROM \"BebesIdentidadesFiscales\" WHERE \"Documento\"={doc}").ToListAsync();
    }
    private async Task<bool> DestinoVigenteAsync(FacturaFila f)
    {
        if(f.IdentidadBebeId!=null)
        {
            var matches=await CoincidenciasBebeAsync(f.Receptor);
            return matches.Any(x=>x.Id==f.IdentidadBebeId && x.Autorizado && CorreoValido(x.Email) && x.Email==f.Destinatario)
                && matches.All(x=>x.Autorizado && CorreoValido(x.Email) && string.Equals(x.Email,f.Destinatario,StringComparison.OrdinalIgnoreCase));
        }
        return !(await CoincidenciasBebeAsync(f.Receptor)).Any() && (await ContactosAsync()).Any(c=>c.Id==f.ResponsableId && c.Email==f.Destinatario);
    }
    public async Task<string> ImportarAsync(byte[] pdf,List<FilaArca>? listado)
    {
        var d=LeerPdf(pdf);
        var emisorPermitido=Digitos(config["Facturas:CuitEmisor"]);
        if(emisorPermitido.Length!=11 || d.Emisor!=emisorPermitido)throw new InvalidDataException("CUIT emisor no habilitado. Configurá Facturas:CuitEmisor.");
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
        var fiscales=await CoincidenciasBebeAsync(d.Receptor);
        var fiscal=fiscales.Count>0 && fiscales.All(x=>x.Autorizado&&CorreoValido(x.Email)) && fiscales.Select(x=>x.Email.ToUpperInvariant()).Distinct().Count()==1 ? fiscales.OrderBy(x=>x.Id).First() : null;
        if(fiscales.Count>0)c=null;
        Guid? bebeFiscal=fiscal!=null && fiscales.Select(x=>x.BebeId).Distinct().Count()==1 ? fiscal.BebeId : null;
        var id=Guid.NewGuid(); var hash=Convert.ToHexString(SHA256.HashData(pdf));
        var nfilas=await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "FacturasFiscales" ("Id","Emisor","Tipo","Punto","Numero","Receptor","Fecha","Importe","Cae","Pdf","Hash","ResponsableId","Destinatario","ResponsableNombre","Contrastada","IdentidadBebeId","BebeId")
            VALUES ({id},{d.Emisor},{d.Tipo},{d.Punto},{d.Numero},{d.Receptor},{d.Fecha},{d.Importe},{d.Cae},{pdf},{hash},{c?.Id},{fiscal?.Email??c?.Email},{fiscal?.Titular??c?.Nombre},{contrastada},{fiscal?.Id},{bebeFiscal}) ON CONFLICT ("Emisor","Tipo","Punto","Numero") DO NOTHING
            """);
        if(nfilas==1)return "Importada";
        var hashes=await db.Database.SqlQuery<string>($"SELECT \"Hash\" AS \"Value\" FROM \"FacturasFiscales\" WHERE \"Emisor\"={d.Emisor} AND \"Tipo\"={d.Tipo} AND \"Punto\"={d.Punto} AND \"Numero\"={d.Numero}").ToListAsync();
        return hashes.Count==1&&hashes[0]==hash?"Ya importada (mismo archivo)":"Comprobante ya existente con PDF diferente: revisar. No se reemplazó.";
    }
    public async Task<List<FacturaFila>> ListarAsync()
    {
        await using var db=await factory.CreateDbContextAsync();
        return await db.Database.SqlQueryRaw<FacturaFila>("""SELECT "Id","Emisor","Tipo","Punto","Numero","Receptor","Fecha","Importe","Cae","Estado","ResponsableId","Destinatario","ResponsableNombre","EnviadoUtc","Contrastada","IdentidadBebeId","BebeId" FROM "FacturasFiscales" ORDER BY "Fecha" DESC,"Numero" DESC LIMIT 2000""").ToListAsync();
    }
    public async Task VincularAsync(Guid factura,Guid responsable,bool recordar)
    {
        var c=(await ContactosAsync()).SingleOrDefault(x=>x.Id==responsable)??throw new InvalidOperationException("Contacto sin correo autorizado.");
        await using var db=await factory.CreateDbContextAsync();
        await using var tx=await db.Database.BeginTransactionAsync();
        var docs=await db.Database.SqlQuery<string>($"SELECT \"Receptor\" AS \"Value\" FROM \"FacturasFiscales\" WHERE \"Id\"={factura} AND \"Estado\"='Pendiente'").ToListAsync();
        if(docs.Count!=1)throw new InvalidOperationException("La factura no está pendiente.");
        if((await CoincidenciasBebeAsync(docs[0])).Count>0)throw new InvalidOperationException("Este CUIT/CUIL está registrado en Datos fiscales. Revisá los titulares y usá Buscar CUIT/CUIL.");
        if(recordar)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"IdentidadesFiscales\" (\"Documento\",\"ResponsableId\") VALUES ({docs[0]},{responsable}) ON CONFLICT DO NOTHING");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"IdentidadBebeId\"=NULL,\"BebeId\"=NULL,\"ResponsableId\"={responsable},\"Destinatario\"={c.Email},\"ResponsableNombre\"={c.Nombre} WHERE \"Id\"={factura} AND \"Estado\"='Pendiente'");
        await tx.CommitAsync();
    }
    public async Task ReidentificarAsync(Guid id)
    {
        await using var db=await factory.CreateDbContextAsync();
        var f=(await ListarAsync()).SingleOrDefault(x=>x.Id==id && x.Estado=="Pendiente")??throw new InvalidOperationException("Factura no pendiente.");
        var m=await CoincidenciasBebeAsync(f.Receptor);
        if(m.Count==0 || m.Any(x=>!x.Autorizado||!CorreoValido(x.Email)) || m.Select(x=>x.Email.ToUpperInvariant()).Distinct().Count()!=1)throw new InvalidOperationException("No hay una coincidencia fiscal autorizada e inequívoca.");
        var x=m.OrderBy(x=>x.Id).First(); Guid? bebe=m.Select(x=>x.BebeId).Distinct().Count()==1?x.BebeId:null;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"IdentidadBebeId\"={x.Id},\"BebeId\"={bebe},\"ResponsableId\"=NULL,\"Destinatario\"={x.Email},\"ResponsableNombre\"={x.Titular} WHERE \"Id\"={id} AND \"Estado\"='Pendiente'");
    }
    public async Task<int> EncolarAsync(IEnumerable<Guid> ids)
    {
        if(!Habilitado)return 0;
        var facturas=await ListarAsync(); int total=0;
        await using var db=await factory.CreateDbContextAsync();
        foreach(var id in ids.Distinct())
        {
            var f=facturas.FirstOrDefault(x=>x.Id==id);
            if(f==null || !await DestinoVigenteAsync(f))continue;
            total+=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"Estado\"='EnCola' WHERE \"Id\"={id} AND \"Estado\"='Pendiente' AND \"ResponsableId\" IS NOT DISTINCT FROM {f.ResponsableId} AND \"IdentidadBebeId\" IS NOT DISTINCT FROM {f.IdentidadBebeId} AND \"Destinatario\"={f.Destinatario}");
        }
        return total;
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
            UPDATE "FacturasFiscales" SET "Estado"='EnProceso' WHERE "Id"=(SELECT "Id" FROM "FacturasFiscales" WHERE "Estado"='EnCola' ORDER BY "Fecha","Numero" LIMIT 1 FOR UPDATE SKIP LOCKED) RETURNING "Id" AS "Value"
            """).ToListAsync();
        if(ids.Count==0)return null;
        var id=ids[0]; var f=(await ListarAsync()).FirstOrDefault(x=>x.Id==id);
        if(f==null || !await DestinoVigenteAsync(f)) {await ResultadoAsync(id,false);return null;}
        var pdf=await PdfAsync(id); if(pdf==null){await ResultadoAsync(id,false);return null;}
        return new(id,f.Destinatario!, $"Bebés Nadadores - Factura {f.Comprobante}", $"Factura_{f.Punto:00000}_{f.Numero:00000000}.pdf",Convert.ToBase64String(pdf));
    }
    public async Task ResultadoAsync(Guid id,bool enviado)
    {
        await using var db=await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FacturasFiscales\" SET \"Estado\"={(enviado?"Enviado":"Error")},\"EnviadoUtc\"=CASE WHEN {enviado} THEN CURRENT_TIMESTAMP ELSE NULL END WHERE \"Id\"={id} AND \"Estado\"='EnProceso'");
    }
}
