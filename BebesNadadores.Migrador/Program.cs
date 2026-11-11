using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Migrador;
using Microsoft.EntityFrameworkCore;

var opt = MigrationOptions.Parse(args);
if (opt.ShowHelp)
{
    MigrationOptions.PrintHelp();
    return;
}

Directory.CreateDirectory(opt.ReportDirectory);

var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(opt.PostgresConnection)
    .EnableDetailedErrors()
    .Options;

await using var db = new AppDbContext(dbOptions);
var legacy = new LegacyRepository(opt.MySqlConnection);
var report = new MigrationReport(opt.ReportDirectory);
// Corrector de turnera para la base test YA importada. No ejecuta migración general.
if (args.Any(a => a.Equals("--corregir-turnera-test", StringComparison.OrdinalIgnoreCase)))
{
    var apply = args.Any(a => a.Equals("--aplicar-turnera-test", StringComparison.OrdinalIgnoreCase));
    if (apply && opt.DryRun) throw new ArgumentException("No combinar --aplicar-turnera-test con --dry-run.");
    try
    {
        await TurneraTestFix.RunAsync(db, legacy, opt.ReportDirectory, apply, opt.Cutoff!.Value, opt.ConfirmProduction);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
        Environment.ExitCode = 1;
    }
    return;
}

// V4.4: conciliacion conservadora de cuotas de alumnos activos YA migrados.
if (args.Any(a => a.Equals("--conciliar-finanzas-test", StringComparison.OrdinalIgnoreCase)))
{
    var apply = args.Any(a => a.Equals("--aplicar-finanzas-test", StringComparison.OrdinalIgnoreCase));
    if (apply && opt.DryRun) throw new ArgumentException("No combinar --aplicar-finanzas-test y --dry-run.");
    try
    {
        await FinanzasActivosTest.RunAsync(db, legacy, opt, apply);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
        Environment.ExitCode = 1;
    }
    return;
}

var migrator = new LegacyMigrationService(db, legacy, report, opt);

try
{
    await migrator.RunAsync();
    Console.WriteLine($"\nOK. Reportes: {Path.GetFullPath(opt.ReportDirectory)}");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}
