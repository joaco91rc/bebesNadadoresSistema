namespace BebesNadadores.Migrador;

public sealed record MigrationOptions(
    string MySqlConnection,
    string PostgresConnection,
    bool DryRun,
    int HistoricalFromYear,
    int FullYear,
    DateOnly? Cutoff,
    string ReportDirectory,
    bool ShowHelp,
    bool ConfirmProduction)
{
    public static MigrationOptions Parse(string[] args)
    {
        string? Get(string name)
        {
            var i = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        bool Has(string name) => args.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (Has("--help") || Has("-h"))
            return new("", "", true, 2019, 2026, null, "migration-report", true, false);

        var mysql = Get("--mysql") ?? Environment.GetEnvironmentVariable("BEBES_LEGACY_MYSQL") ?? "";
        var pg = Get("--postgres") ?? Environment.GetEnvironmentVariable("BEBES_POSTGRES") ?? "";
        if (string.IsNullOrWhiteSpace(mysql) || string.IsNullOrWhiteSpace(pg))
            throw new ArgumentException("Faltan --mysql y/o --postgres. Use --help.");

        var from = int.TryParse(Get("--desde"), out var fy) ? fy : 2019;
        var full = int.TryParse(Get("--anio-completo"), out var cy) ? cy : 2026;
        DateOnly? cutoff = DateOnly.TryParse(Get("--corte"), out var d) ? d : null;
        if(cutoff is null) throw new ArgumentException("Debe indicar --corte AAAA-MM-DD para evitar importar clases futuras o movimientos fuera del backup.");
        if (full <= from) throw new ArgumentException("--anio-completo debe ser posterior a --desde.");
        var report = Get("--reportes") ?? "migration-report";

        return new(mysql, pg, Has("--dry-run"), from, full, cutoff, report, false, Has("--autorizar-produccion"));
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
BebesNadadores.Migrador

Uso:
  dotnet run --project BebesNadadores.Migrador -- \
    --mysql "Server=localhost;Database=bebes_legacy;User ID=root;Password=..." \
    --postgres "Host=localhost;Database=BebesNadadores;Username=postgres;Password=..." \
    --dry-run

Opciones:
  --desde 2019          primer año de histórico resumido
  --anio-completo 2026  año que se migra con detalle
  --corte 2026-11-02    no tomar movimientos posteriores a esa fecha
  --reportes ./reportes carpeta de CSV/diagnóstico
  --dry-run             ejecuta todo y hace ROLLBACK al final

La fuente debe ser una base MySQL restaurada desde el Backup Base.sql.
""");
    }
}
