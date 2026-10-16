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

    protected string _periodoTexto =
        DateTime.Today.ToString(
            "yyyy-MM",
            CultureInfo.InvariantCulture);


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

        if (!DateOnly.TryParseExact(
                $"{_periodoTexto}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodo))
        {
            _error =
                "Debe seleccionar un período válido.";

            return;
        }


        try
        {
            _generando =
                true;


            _resultado =
                await GeneradorCuotasService
                    .GenerarAsync(
                        periodo);


            _mensaje =
                _resultado.CuotasGeneradas > 0

                    ? $"Se generaron " +
                      $"{_resultado.CuotasGeneradas} cuota(s) " +
                      $"para el período " +
                      $"{periodo:MM/yyyy}."

                    : $"El período " +
                      $"{periodo:MM/yyyy} ya se encontraba " +
                      $"procesado.";
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

protected void CambiarPeriodo(
    ChangeEventArgs e)
{
    _periodoTexto =
        e.Value?.ToString()
        ?? string.Empty;


    _error =
        null;


    _mensaje =
        null;


    _resultado =
        null;
}

    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo(
            "/pagos");
    }
}