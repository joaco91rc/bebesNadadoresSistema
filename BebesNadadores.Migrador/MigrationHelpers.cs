using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Migrador;

public static class MigrationHelpers
{
    private static readonly Guid NamespaceId = Guid.Parse("3dc8f522-cdf7-4d2a-8af1-8a1cf057a5f1");

    public static Guid StableGuid(string key)
    {
        var ns = NamespaceId.ToByteArray();
        var data = Encoding.UTF8.GetBytes(key);
        var all = new byte[ns.Length + data.Length]; Buffer.BlockCopy(ns,0,all,0,ns.Length); Buffer.BlockCopy(data,0,all,ns.Length,data.Length);
        var hash = SHA256.HashData(all);
        var b = hash[..16].ToArray();
        b[6] = (byte)((b[6] & 0x0F) | 0x50); b[8] = (byte)((b[8] & 0x3F) | 0x80);
        return new Guid(b);
    }

    public static string FixText(string? value)
    {
        var s=(value??"").Trim();
        if (s.Contains('Ã') || s.Contains('Â'))
        {
            try { s = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(s)); } catch { }
        }
        return string.Join(' ',s.Split(' ',StringSplitOptions.RemoveEmptyEntries));
    }

    public static (string Nombre,string Apellido,bool Dudoso) SplitLegacyName(string raw)
    {
        var s=FixText(raw);
        if(string.IsNullOrWhiteSpace(s)) return ("Sin nombre","Legacy",true);
        var p=s.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(p.Length==1) return (ToTitle(p[0]),"Legacy",true);
        // El sistema viejo usa mayoritariamente APELLIDO NOMBRE(S).
        // Apellidos compuestos frecuentes. Conservar marca de revisión cuando haya ambigüedad.
        var upper = p.Select(x => Strip(x)).ToArray();
        var prefix = upper.Length >= 3 && (upper[0] is "DE" or "DEL" or "DI" or "DA" or "DOS" or "VAN" or "VON" or "SAN" or "SANTA") ? 2 : 1;
        if (upper.Length >= 4 && upper[0] == "DE" && upper[1] == "LA") prefix = 3;
        var apellido=ToTitle(string.Join(' ',p.Take(prefix)));
        var nombre=ToTitle(string.Join(' ',p.Skip(prefix)));
        return (nombre,apellido,p.Length>=4 || prefix>1);
    }

    public static string ToTitle(string s)=>CultureInfo.GetCultureInfo("es-AR").TextInfo.ToTitleCase(s.ToLowerInvariant());
    public static string Digits(string? s)=>new((s??"").Where(char.IsDigit).ToArray());

    public static int Frequency(IEnumerable<LegacyClase> rows)
    {
        var weekly=rows.GroupBy(x=>ISOWeek.GetWeekOfYear(x.Fecha.ToDateTime(TimeOnly.MinValue)))
                       .Select(g=>g.Select(x=>NormalizeDay(x.Dia)).Where(x=>x!=0).Distinct().Count())
                       .Where(x=>x>0).ToList();
        if(weekly.Count==0) return 1;
        var grouped=weekly.GroupBy(x=>Math.Clamp(x,1,3)).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).First();
        return grouped.Key;
    }

    public static DiaSemana NormalizeDay(string day)
    {
        var s=Strip(day);
        if(s.Contains("LUNES")) return DiaSemana.Lunes;
        if(s.Contains("MARTES")) return DiaSemana.Martes;
        if(s.Contains("MIERCOLES")) return DiaSemana.Miercoles;
        if(s.Contains("JUEVES")) return DiaSemana.Jueves;
        if(s.Contains("VIERNES")) return DiaSemana.Viernes;
        if(s.Contains("SABADO")) return DiaSemana.Sabado;
        if(s.Contains("DOMINGO")) return DiaSemana.Domingo;
        return 0;
    }

    public static TimeOnly ParseTime(string raw)=>TimeOnly.TryParse(raw,out var t)?t:new TimeOnly(0,0);

    public static Nivel? ResolveLevel(string legacy, DateOnly date, DateOnly? birth, IReadOnlyCollection<Nivel> levels)
    {
        var s=Strip(legacy);
        string? target =
            s.Contains("3 A 12") ? "Bebés 3 a 12 meses" :
            s.Contains("12 A 24") ? "Bebés 12 a 24 meses" :
            s.Contains("2 A 3") ? "2 años" :
            s.Contains("3 A 4") ? "3 y 4 años" :
            s.Contains("MIXTO") ? "3 a 6 años" :
            s.Contains("5 A 6") ? "5 y 6 años" : null;
        if(target is not null) return levels.FirstOrDefault(x=>x.Nombre.Equals(target,StringComparison.OrdinalIgnoreCase));
        if(birth is null) return null;
        var months=(date.Year-birth.Value.Year)*12+date.Month-birth.Value.Month-(date.Day<birth.Value.Day?1:0);
        return levels.Where(x=>months>=x.EdadMinMeses && months<=x.EdadMaxMeses).OrderBy(x=>x.Orden).FirstOrDefault();
    }

    public static EstadoAsistencia Attendance(string code,out bool recupero,out bool known)
    {
        recupero=false; known=true;
        switch((code??"").Trim().ToUpperInvariant())
        {
            case "P": return EstadoAsistencia.Presente;
            case "A": return EstadoAsistencia.Ausente;
            // R no está confirmado como asistencia presente; se audita.
            default: known=false; return EstadoAsistencia.Pendiente;
        }
    }

    public static string Strip(string? s)
    {
        var n=FixText(s).Normalize(NormalizationForm.FormD);
        return new string(n.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
    }
}
