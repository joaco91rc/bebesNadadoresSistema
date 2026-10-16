using BebesNadadores.Application.Proveedores;
using BebesNadadores.Application.Proveedores.Dtos;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Proveedores;

public partial class Proveedores
    : ComponentBase
{
    [Inject]
    protected IProveedorService ProveedorService { get; set; }
        = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; }
        = null!;


    protected List<ProveedorDto> _proveedores =
        [];

    protected GuardarProveedorDto _form =
        new();


    protected Guid? _editandoId;

    protected bool _mostrarModal;

    protected bool _mostrarInactivos;

    protected bool _cargando;

    protected bool _guardando;

    protected string _buscar =
        string.Empty;

    protected string? _error;

    protected string? _errorModal;


    protected List<ProveedorDto>
        ProveedoresFiltrados
    {
        get
        {
            IEnumerable<ProveedorDto> query =
                _proveedores;


            if (!string.IsNullOrWhiteSpace(
                _buscar))
            {
                var texto =
                    _buscar.Trim();


                query =
                    query.Where(x =>
                        Contiene(
                            x.RazonSocial,
                            texto)

                        ||

                        Contiene(
                            x.NombreFantasia,
                            texto)

                        ||

                        Contiene(
                            x.Cuit,
                            texto)

                        ||

                        Contiene(
                            x.Email,
                            texto)

                        ||

                        Contiene(
                            x.Telefono,
                            texto));
            }


            return query
                .OrderByDescending(x =>
                    x.Activo)

                .ThenBy(x =>
                    x.RazonSocial)

                .ToList();
        }
    }


    protected override async Task
        OnInitializedAsync()
    {
        await CargarAsync();
    }


    private async Task CargarAsync()
    {
        try
        {
            _cargando =
                true;

            _error =
                null;


            var datos =
                await ProveedorService
                    .ListarAsync(
                        _mostrarInactivos);


            _proveedores =
                datos.ToList();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _cargando =
                false;
        }
    }


    protected async Task RecargarAsync()
    {
        await CargarAsync();
    }


    protected void NuevoProveedor()
    {
        _editandoId =
            null;

        _form =
            new GuardarProveedorDto
            {
                Activo =
                    true
            };

        _errorModal =
            null;

        _mostrarModal =
            true;
    }


    protected async Task EditarProveedorAsync(
        Guid id)
    {
        try
        {
            _error =
                null;


            var proveedor =
                await ProveedorService
                    .ObtenerAsync(id);


            if (proveedor is null)
            {
                _error =
                    "El proveedor indicado no existe.";

                return;
            }


            _editandoId =
                proveedor.Id;


            _form =
                new GuardarProveedorDto
                {
                    RazonSocial =
                        proveedor.RazonSocial,

                    NombreFantasia =
                        proveedor.NombreFantasia,

                    Cuit =
                        proveedor.Cuit,

                    Telefono =
                        proveedor.Telefono,

                    Email =
                        proveedor.Email,

                    Direccion =
                        proveedor.Direccion,

                    Observaciones =
                        proveedor.Observaciones,

                    Activo =
                        proveedor.Activo
                };


            _errorModal =
                null;

            _mostrarModal =
                true;
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    protected async Task GuardarAsync()
    {
        try
        {
            _guardando =
                true;

            _errorModal =
                null;


            if (_editandoId.HasValue)
            {
                await ProveedorService
                    .ActualizarAsync(
                        _editandoId.Value,
                        _form);
            }
            else
            {
                await ProveedorService
                    .CrearAsync(
                        _form);
            }


            _mostrarModal =
                false;


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _errorModal =
                Errores.Mensaje(ex);
        }
        finally
        {
            _guardando =
                false;
        }
    }


    protected async Task CambiarEstadoAsync(
        ProveedorDto proveedor)
    {
        try
        {
            _error =
                null;


            await ProveedorService
                .CambiarEstadoAsync(
                    proveedor.Id,
                    !proveedor.Activo);


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    protected void VerPagos(
        Guid proveedorId)
    {
        Navigation.NavigateTo(
            $"/proveedores/{proveedorId}/pagos");
    }


    protected void CerrarModal()
    {
        if (_guardando)
            return;


        _mostrarModal =
            false;

        _errorModal =
            null;
    }


    protected static string ObtenerIniciales(
        ProveedorDto proveedor)
    {
        var texto =
            proveedor.NombreFantasia
            ??
            proveedor.RazonSocial;


        var partes =
            texto
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries);


        if (partes.Length == 0)
            return "PR";


        if (partes.Length == 1)
        {
            return partes[0]
                .Substring(
                    0,
                    Math.Min(
                        2,
                        partes[0].Length))
                .ToUpperInvariant();
        }


        return
            $"{partes[0][0]}{partes[1][0]}"
                .ToUpperInvariant();
    }


    private static bool Contiene(
        string? valor,
        string buscar)
    {
        return !string.IsNullOrWhiteSpace(valor)
               &&
               valor.Contains(
                   buscar,
                   StringComparison.OrdinalIgnoreCase);
    }
}