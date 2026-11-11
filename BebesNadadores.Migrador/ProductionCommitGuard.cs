using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Migrador;

internal static class ProductionCommitGuard
{
    public static bool Check(AppDbContext db, bool authorized)
    {
        var current = db.Database.GetDbConnection().Database;
        if (string.Equals(current, "BebesNadadores_MigracionTest", StringComparison.OrdinalIgnoreCase))
            return false;
        var expected = Environment.GetEnvironmentVariable("BEBES_MIGRACION_PROD_TARGET");
        if (!authorized || string.IsNullOrWhiteSpace(expected) ||
            !string.Equals(expected, current, StringComparison.Ordinal))
            throw new InvalidOperationException("COMMIT bloqueado: nombre de la base de producción no autorizado expresamente.");
        if (current.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Destino TEST inesperado en autorización productiva.");
        return true;
    }
}
