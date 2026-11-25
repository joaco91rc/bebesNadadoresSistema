using System.Globalization;
using BebesNadadores.Application.Cuotas;
using BebesNadadores.Application.Cuotas.Dtos;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Pagos;

public partial class GeneracionCuotas
{
    // =========================================================
    // SERVICIOS
    // =========================================================

    [Inject]
    protected IGeneradorCuotasService
        GeneradorCuotasService { get; set; } = null!;


    [Inject]
    protected NavigationManager
        Navigation { get; set; } = null!;


    // =========================================================
    // ESTADO
    // =========================================================

    protected DateOnly _periodo = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    protected int _cantidad = 1;

    protected bool _generando;


    protected string? _error;


    protected string? _mensaje;


    protected GeneracionCuotasResultadoDto?
        _resultado;


    // =========================================================
    // GENERAR
    // =========================================================

    protected async Task GenerarAsync()
    {
        if (_generando)
            return;


        _error =
            null;


        _mensaje =
            null;


        _resultado =
            null;


        // -----------------------------------------------------
        // PARSEAR YYYY-MM
        // -----------------------------------------------------

        var periodo = new DateOnly(_periodo.Year, _periodo.Month, 1);

        try
        {
            _generando =
                true;


            _resultado =
                await GeneradorCuotasService
                    .GenerarPeriodosAsync(periodo, _cantidad);


            _mensaje =
                _resultado.CuotasGeneradas > 0

                    ? $"Se generaron " +
                      $"{_resultado.CuotasGeneradas} cuota(s) " +
                      $"para el período " +
                      $"{periodo:MM/yyyy} a {periodo.AddMonths(_cantidad - 1):MM/yyyy}."

                    : $"El período " +
                      $"{periodo:MM/yyyy} a {periodo.AddMonths(_cantidad - 1):MM/yyyy} ya se encontraba procesado.";
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _generando =
                false;
        }
    }


// =========================================================
// CAMBIAR PERÍODO
// =========================================================

    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo(
            "/pagos");
    }
}