using System.Globalization;

using BebesNadadores.Application.Reportes;
using BebesNadadores.Application.Reportes.Dtos;

using ClosedXML.Excel;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BebesNadadores.Infrastructure.Services.Reportes;

public sealed class ReporteExportService(
    IReporteService reporteService)
    : IReporteExportService
{
    private static readonly CultureInfo Cultura =
        CultureInfo.GetCultureInfo(
            "es-AR");

    static ReporteExportService()
    {
        /*
         * QuestPDF requiere indicar el tipo de licencia.
         *
         * Community es válida únicamente cuando la organización
         * cumple las condiciones de esa licencia.
         * Si no corresponde, cambiarla por la licencia contratada.
         */
        QuestPDF.Settings.License =
            LicenseType.Community;
    }


    // =============================================================
    // EXCEL
    // =============================================================

    public async Task<ReporteArchivoDto> ExportarExcelAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var reporte =
            await reporteService
                .ObtenerAsync(
                    desde,
                    hasta,
                    ct);

        using var workbook =
            new XLWorkbook();

        CrearHojaResumen(
            workbook,
            reporte);

        CrearHojaAsistencia(
            workbook,
            reporte);

        CrearHojaOcupacion(
            workbook,
            reporte);

        CrearHojaRecuperos(
            workbook,
            reporte);

        CrearHojaFinanzas(
            workbook,
            reporte);

        CrearHojaCaja(
            workbook,
            reporte);

        CrearHojaBancos(
            workbook,
            reporte);

        CrearHojaMovimientosBancarios(
            workbook,
            reporte);

        CrearHojaSalud(
            workbook,
            reporte);

        using var stream =
            new MemoryStream();

        workbook.SaveAs(
            stream);

        return new ReporteArchivoDto(
            stream.ToArray(),
            $"Reporte_BebesNadadores_{desde:yyyy-MM-dd}_{hasta:yyyy-MM-dd}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }


    // =============================================================
    // PDF
    // =============================================================

    public async Task<ReporteArchivoDto> ExportarPdfAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var reporte =
            await reporteService
                .ObtenerAsync(
                    desde,
                    hasta,
                    ct);

        using var stream =
            new MemoryStream();

        var documento =
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(
                        PageSizes.A4.Landscape());

                    page.Margin(
                        22);

                    page.DefaultTextStyle(
                        x =>
                            x.FontSize(8.5f));

                    page.Header()
                        .Element(c =>
                            ComponerCabeceraPdf(
                                c,
                                reporte));

                    page.Content()
                        .PaddingTop(8)
                        .Column(column =>
                        {
                            ComponerResumenPdf(
                                column,
                                reporte);

                            column.Item()
                                .PageBreak();

                            ComponerAsistenciaPdf(
                                column,
                                reporte);

                            column.Item()
                                .PageBreak();

                            ComponerOcupacionPdf(
                                column,
                                reporte);

                            column.Item()
                                .PaddingTop(10);

                            ComponerRecuperosPdf(
                                column,
                                reporte);

                            column.Item()
                                .PageBreak();

                            ComponerFinanzasPdf(
                                column,
                                reporte);

                            column.Item()
                                .PageBreak();

                            ComponerCajaPdf(
                                column,
                                reporte);

                            column.Item()
                                .PageBreak();

                            ComponerBancosPdf(
                                column,
                                reporte);

                            column.Item()
                                .PageBreak();

                            ComponerSaludPdf(
                                column,
                                reporte);
                        });

                    page.Footer()
                        .PaddingTop(8)
                        .BorderTop(1)
                        .BorderColor(
                            Colors.Grey.Lighten2)
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.DefaultTextStyle(
                                x =>
                                    x.FontSize(7)
                                     .FontColor(
                                         Colors.Grey.Darken1));

                            text.Span(
                                "Bebés Nadadores · Página ");

                            text.CurrentPageNumber();

                            text.Span(
                                " de ");

                            text.TotalPages();
                        });
                });
            });

        documento.GeneratePdf(
            stream);

        return new ReporteArchivoDto(
            stream.ToArray(),
            $"Reporte_BebesNadadores_{desde:yyyy-MM-dd}_{hasta:yyyy-MM-dd}.pdf",
            "application/pdf");
    }


    // =============================================================
    // EXCEL - RESUMEN
    // =============================================================

    private static void CrearHojaResumen(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Resumen");

        PrepararTitulo(
            ws,
            "Reporte general",
            reporte,
            4);

        var fila =
            4;

        EscribirCabecera(
            ws,
            fila,
            "Indicador",
            "Valor",
            "Indicador",
            "Valor");

        fila++;

        EscribirParKpi(
            ws,
            fila++,
            "Bebés activos",
            reporte.Resumen.BebesActivos,
            "Altas del período",
            reporte.Resumen.AltasPeriodo);

        EscribirParKpi(
            ws,
            fila++,
            "Clases dictables",
            reporte.Resumen.ClasesDictables,
            "Clases canceladas",
            reporte.Resumen.ClasesCanceladas);

        EscribirParKpi(
            ws,
            fila++,
            "Asistencia promedio",
            reporte.Resumen.AsistenciaPromedio / 100m,
            "Ocupación promedio",
            reporte.Resumen.OcupacionPromedio / 100m);

        ws.Cell(
                fila - 1,
                2)
            .Style.NumberFormat.Format =
            "0.0%";

        ws.Cell(
                fila - 1,
                4)
            .Style.NumberFormat.Format =
            "0.0%";

        EscribirParKpi(
            ws,
            fila++,
            "Cobrado en el período",
            reporte.Resumen.CobradoPeriodo,
            "Deuda actual",
            reporte.Resumen.DeudaActual);

        FormatoMoneda(
            ws.Cell(
                fila - 1,
                2));

        FormatoMoneda(
            ws.Cell(
                fila - 1,
                4));

        fila +=
            2;

        ws.Cell(
                fila,
                1)
            .Value =
            "Finanzas";

        EstiloSubtitulo(
            ws.Range(
                fila,
                1,
                fila,
                4));

        fila++;

        EscribirParKpi(
            ws,
            fila++,
            "Cuotas del período",
            reporte.Finanzas.CuotasPeriodo,
            "Pagos del período",
            reporte.Finanzas.PagosPeriodo);

        EscribirParKpi(
            ws,
            fila++,
            "Facturado",
            reporte.Finanzas.FacturadoPeriodo,
            "Cobrado",
            reporte.Finanzas.CobradoPeriodo);

        FormatoMoneda(
            ws.Cell(
                fila - 1,
                2));

        FormatoMoneda(
            ws.Cell(
                fila - 1,
                4));

        EscribirParKpi(
            ws,
            fila++,
            "Bebés con deuda",
            reporte.Finanzas.BebesConDeuda,
            "Deuda actual",
            reporte.Finanzas.DeudaActual);

        FormatoMoneda(
            ws.Cell(
                fila - 1,
                4));

        AjustarHoja(
            ws);
    }


    // =============================================================
    // EXCEL - ASISTENCIA
    // =============================================================

    private static void CrearHojaAsistencia(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Asistencia");

        PrepararTitulo(
            ws,
            "Asistencia",
            reporte,
            7);

        var filaCabecera =
            4;

        EscribirCabecera(
            ws,
            filaCabecera,
            "Fecha",
            "Presentes",
            "Ausentes",
            "Con aviso",
            "Pendientes",
            "Total",
            "% asistencia");

        var fila =
            filaCabecera +
            1;

        foreach (var item in
            reporte.AsistenciaPorDia)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Fecha.ToDateTime(
                    TimeOnly.MinValue);

            ws.Cell(
                    fila,
                    1)
                .Style.NumberFormat.Format =
                "dd/MM/yyyy";

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Presentes;

            ws.Cell(
                    fila,
                    3)
                .Value =
                item.Ausentes;

            ws.Cell(
                    fila,
                    4)
                .Value =
                item.ConAviso;

            ws.Cell(
                    fila,
                    5)
                .Value =
                item.Pendientes;

            ws.Cell(
                    fila,
                    6)
                .Value =
                item.Total;

            ws.Cell(
                    fila,
                    7)
                .Value =
                item.PorcentajeAsistencia /
                100m;

            ws.Cell(
                    fila,
                    7)
                .Style.NumberFormat.Format =
                "0.0%";

            fila++;
        }

        FinalizarTabla(
            ws,
            filaCabecera,
            Math.Max(
                fila - 1,
                filaCabecera),
            7);
    }


    // =============================================================
    // EXCEL - OCUPACIÓN
    // =============================================================

    private static void CrearHojaOcupacion(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Ocupación");

        PrepararTitulo(
            ws,
            "Ocupación por nivel",
            reporte,
            6);

        var filaCabecera =
            4;

        EscribirCabecera(
            ws,
            filaCabecera,
            "Nivel",
            "Clases",
            "Cupos",
            "Ocupados",
            "Vacantes",
            "% ocupación");

        var fila =
            filaCabecera +
            1;

        foreach (var item in
            reporte.OcupacionPorNivel)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Nivel;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Clases;

            ws.Cell(
                    fila,
                    3)
                .Value =
                item.CuposTotales;

            ws.Cell(
                    fila,
                    4)
                .Value =
                item.Ocupados;

            ws.Cell(
                    fila,
                    5)
                .Value =
                item.Vacantes;

            ws.Cell(
                    fila,
                    6)
                .Value =
                item.PorcentajeOcupacion /
                100m;

            ws.Cell(
                    fila,
                    6)
                .Style.NumberFormat.Format =
                "0.0%";

            fila++;
        }

        FinalizarTabla(
            ws,
            filaCabecera,
            Math.Max(
                fila - 1,
                filaCabecera),
            6);
    }


    // =============================================================
    // EXCEL - RECUPEROS
    // =============================================================

    private static void CrearHojaRecuperos(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Recuperos");

        PrepararTitulo(
            ws,
            "Recuperos",
            reporte,
            4);

        var fila =
            4;

        EscribirCabecera(
            ws,
            fila,
            "Estado",
            "Cantidad");

        fila++;

        var estados =
            new[]
            {
                ("Generados", reporte.Recuperos.Generados),
                ("Disponibles", reporte.Recuperos.Disponibles),
                ("Reservados", reporte.Recuperos.Reservados),
                ("Utilizados", reporte.Recuperos.Utilizados),
                ("Vencidos", reporte.Recuperos.Vencidos),
                ("Anulados", reporte.Recuperos.Anulados)
            };

        foreach (var estado in
            estados)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                estado.Item1;

            ws.Cell(
                    fila,
                    2)
                .Value =
                estado.Item2;

            fila++;
        }

        fila++;

        ws.Cell(
                fila,
                1)
            .Value =
            "Recuperos por motivo";

        EstiloSubtitulo(
            ws.Range(
                fila,
                1,
                fila,
                2));

        fila++;

        EscribirCabecera(
            ws,
            fila,
            "Motivo",
            "Cantidad");

        fila++;

        foreach (var item in
            reporte.Recuperos.PorTipo)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Tipo;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Cantidad;

            fila++;
        }

        AjustarHoja(
            ws);
    }


    // =============================================================
    // EXCEL - FINANZAS
    // =============================================================

    private static void CrearHojaFinanzas(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Finanzas");

        PrepararTitulo(
            ws,
            "Finanzas",
            reporte,
            5);

        var fila =
            4;

        EscribirCabecera(
            ws,
            fila,
            "Indicador",
            "Valor");

        fila++;

        var resumen =
            new (string Nombre, decimal Valor, bool Moneda)[]
            {
                ("Cuotas del período", reporte.Finanzas.CuotasPeriodo, false),
                ("Facturado", reporte.Finanzas.FacturadoPeriodo, true),
                ("Pagos del período", reporte.Finanzas.PagosPeriodo, false),
                ("Cobrado", reporte.Finanzas.CobradoPeriodo, true),
                ("Deuda actual", reporte.Finanzas.DeudaActual, true),
                ("Bebés con deuda", reporte.Finanzas.BebesConDeuda, false)
            };

        foreach (var item in
            resumen)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Nombre;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Valor;

            if (item.Moneda)
            {
                FormatoMoneda(
                    ws.Cell(
                        fila,
                        2));
            }

            fila++;
        }

        fila +=
            2;

        ws.Cell(
                fila,
                1)
            .Value =
            "Cobros por medio";

        EstiloSubtitulo(
            ws.Range(
                fila,
                1,
                fila,
                3));

        fila++;

        var mediosCabecera =
            fila;

        EscribirCabecera(
            ws,
            fila,
            "Medio",
            "Operaciones",
            "Importe");

        fila++;

        foreach (var item in
            reporte.Finanzas.PorMedio)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Medio;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Cantidad;

            ws.Cell(
                    fila,
                    3)
                .Value =
                item.Importe;

            FormatoMoneda(
                ws.Cell(
                    fila,
                    3));

            fila++;
        }

        fila +=
            2;

        ws.Cell(
                fila,
                1)
            .Value =
            "Mayores deudores";

        EstiloSubtitulo(
            ws.Range(
                fila,
                1,
                fila,
                3));

        fila++;

        EscribirCabecera(
            ws,
            fila,
            "Bebé",
            "Cuotas con saldo",
            "Deuda");

        fila++;

        foreach (var item in
            reporte.Finanzas.MayoresDeudores)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Bebe;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.CuotasPendientes;

            ws.Cell(
                    fila,
                    3)
                .Value =
                item.Deuda;

            FormatoMoneda(
                ws.Cell(
                    fila,
                    3));

            fila++;
        }

        AjustarHoja(
            ws);
    }


    // =============================================================
    // EXCEL - CAJA
    // =============================================================

    private static void CrearHojaCaja(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Caja");

        PrepararTitulo(
            ws,
            "Movimientos de caja",
            reporte,
            5);

        var fila =
            4;

        EscribirCabecera(
            ws,
            fila,
            "Ingresos",
            "Egresos",
            "Neto",
            "Movimientos");

        fila++;

        ws.Cell(
                fila,
                1)
            .Value =
            reporte.Caja.Ingresos;

        ws.Cell(
                fila,
                2)
            .Value =
            reporte.Caja.Egresos;

        ws.Cell(
                fila,
                3)
            .Value =
            reporte.Caja.Neto;

        ws.Cell(
                fila,
                4)
            .Value =
            reporte.Caja.Movimientos;

        for (var columna = 1;
             columna <= 3;
             columna++)
        {
            FormatoMoneda(
                ws.Cell(
                    fila,
                    columna));
        }

        fila +=
            2;

        EscribirCabecera(
            ws,
            fila,
            "Concepto",
            "Movimientos",
            "Ingresos",
            "Egresos",
            "Neto");

        var filaCabecera =
            fila;

        fila++;

        foreach (var item in
            reporte.Caja.PorCategoria)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Categoria;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Cantidad;

            ws.Cell(
                    fila,
                    3)
                .Value =
                item.Ingresos;

            ws.Cell(
                    fila,
                    4)
                .Value =
                item.Egresos;

            ws.Cell(
                    fila,
                    5)
                .Value =
                item.Ingresos -
                item.Egresos;

            for (var columna = 3;
                 columna <= 5;
                 columna++)
            {
                FormatoMoneda(
                    ws.Cell(
                        fila,
                        columna));
            }

            fila++;
        }

        FinalizarTabla(
            ws,
            filaCabecera,
            Math.Max(
                fila - 1,
                filaCabecera),
            5);
    }


    // =============================================================
    // EXCEL - BANCOS RESUMEN
    // =============================================================

    private static void CrearHojaBancos(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Bancos");

        PrepararTitulo(
            ws,
            "Resumen bancario",
            reporte,
            7);

        var fila =
            4;

        EscribirCabecera(
            ws,
            fila,
            "Banco",
            "Cuenta",
            "Movimientos",
            "Ingresos",
            "Egresos",
            "Neto");

        var filaCabecera =
            fila;

        fila++;

        foreach (var cuenta in
            reporte.Bancos.Cuentas)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                cuenta.Banco;

            ws.Cell(
                    fila,
                    2)
                .Value =
                cuenta.Cuenta;

            ws.Cell(
                    fila,
                    3)
                .Value =
                cuenta.Movimientos;

            ws.Cell(
                    fila,
                    4)
                .Value =
                cuenta.Ingresos;

            ws.Cell(
                    fila,
                    5)
                .Value =
                cuenta.Egresos;

            ws.Cell(
                    fila,
                    6)
                .Value =
                cuenta.Neto;

            for (var columna = 4;
                 columna <= 6;
                 columna++)
            {
                FormatoMoneda(
                    ws.Cell(
                        fila,
                        columna));
            }

            fila++;
        }

        FinalizarTabla(
            ws,
            filaCabecera,
            Math.Max(
                fila - 1,
                filaCabecera),
            6);
    }


    // =============================================================
    // EXCEL - MOVIMIENTOS BANCARIOS
    // =============================================================

    private static void CrearHojaMovimientosBancarios(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Movimientos bancarios");

        PrepararTitulo(
            ws,
            "Detalle de movimientos bancarios",
            reporte,
            9);

        var filaCabecera =
            4;

        EscribirCabecera(
            ws,
            filaCabecera,
            "Banco",
            "Cuenta",
            "Fecha",
            "Tipo",
            "Origen",
            "Concepto",
            "Referencia",
            "Observaciones",
            "Importe");

        var fila =
            filaCabecera +
            1;

        foreach (var cuenta in
            reporte.Bancos.Cuentas)
        {
            foreach (var movimiento in
                cuenta.Detalle)
            {
                ws.Cell(
                        fila,
                        1)
                    .Value =
                    cuenta.Banco;

                ws.Cell(
                        fila,
                        2)
                    .Value =
                    cuenta.Cuenta;

                ws.Cell(
                        fila,
                        3)
                    .Value =
                    ObtenerFechaArgentina(
                        movimiento.FechaUtc);

                ws.Cell(
                        fila,
                        3)
                    .Style.NumberFormat.Format =
                    "dd/MM/yyyy HH:mm";

                ws.Cell(
                        fila,
                        4)
                    .Value =
                    movimiento.Tipo;

                ws.Cell(
                        fila,
                        5)
                    .Value =
                    movimiento.Origen;

                ws.Cell(
                        fila,
                        6)
                    .Value =
                    movimiento.Concepto;

                ws.Cell(
                        fila,
                        7)
                    .Value =
                    movimiento.Referencia
                    ?? string.Empty;

                ws.Cell(
                        fila,
                        8)
                    .Value =
                    movimiento.Observaciones
                    ?? string.Empty;

                ws.Cell(
                        fila,
                        9)
                    .Value =
                    movimiento.Tipo ==
                    "Egreso"
                        ? -movimiento.Importe
                        : movimiento.Importe;

                FormatoMoneda(
                    ws.Cell(
                        fila,
                        9));

                fila++;
            }
        }

        FinalizarTabla(
            ws,
            filaCabecera,
            Math.Max(
                fila - 1,
                filaCabecera),
            9);

        ws.Column(
                6)
            .Width =
            36;

        ws.Column(
                8)
            .Width =
            38;

        ws.Column(
                6)
            .Style.Alignment.WrapText =
            true;

        ws.Column(
                8)
            .Style.Alignment.WrapText =
            true;
    }


    // =============================================================
    // EXCEL - SALUD
    // =============================================================

    private static void CrearHojaSalud(
        XLWorkbook workbook,
        ReporteGeneralDto reporte)
    {
        var ws =
            workbook.Worksheets.Add(
                "Salud");

        PrepararTitulo(
            ws,
            "Aptos médicos - estado actual",
            reporte,
            2);

        EscribirCabecera(
            ws,
            4,
            "Situación",
            "Cantidad");

        var items =
            new[]
            {
                ("Vigentes", reporte.Salud.Vigentes),
                ("Por vencer", reporte.Salud.PorVencer),
                ("Vencidos", reporte.Salud.Vencidos),
                ("Sin apto", reporte.Salud.SinApto),
                ("Observados", reporte.Salud.Observados),
                ("No aptos", reporte.Salud.NoAptos)
            };

        var fila =
            5;

        foreach (var item in
            items)
        {
            ws.Cell(
                    fila,
                    1)
                .Value =
                item.Item1;

            ws.Cell(
                    fila,
                    2)
                .Value =
                item.Item2;

            fila++;
        }

        AjustarHoja(
            ws);
    }


    // =============================================================
    // EXCEL - HELPERS
    // =============================================================

    private static void PrepararTitulo(
        IXLWorksheet ws,
        string titulo,
        ReporteGeneralDto reporte,
        int ultimaColumna)
    {
        var rangoTitulo =
            ws.Range(
                1,
                1,
                1,
                ultimaColumna);

        rangoTitulo.Merge();

        rangoTitulo.Value =
            $"Bebés Nadadores · {titulo}";

        rangoTitulo.Style.Font.Bold =
            true;

        rangoTitulo.Style.Font.FontSize =
            18;

        rangoTitulo.Style.Font.FontColor =
            XLColor.White;

        rangoTitulo.Style.Fill.BackgroundColor =
            XLColor.FromArgb(
                15,
                157,
                218);

        rangoTitulo.Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;

        rangoTitulo.Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Left;

        ws.Row(
                1)
            .Height =
            28;

        var rangoPeriodo =
            ws.Range(
                2,
                1,
                2,
                ultimaColumna);

        rangoPeriodo.Merge();

        rangoPeriodo.Value =
            $"Período: {reporte.Desde:dd/MM/yyyy} al {reporte.Hasta:dd/MM/yyyy}";

        rangoPeriodo.Style.Font.FontSize =
            10;

        rangoPeriodo.Style.Font.FontColor =
            XLColor.FromArgb(
                90,
                105,
                120);

        rangoPeriodo.Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;

        ws.Row(
                2)
            .Height =
            20;
    }


    private static void EscribirCabecera(
        IXLWorksheet ws,
        int fila,
        params string[] columnas)
    {
        for (var i = 0;
             i < columnas.Length;
             i++)
        {
            var celda =
                ws.Cell(
                    fila,
                    i + 1);

            celda.Value =
                columnas[i];

            celda.Style.Font.Bold =
                true;

            celda.Style.Font.FontColor =
                XLColor.White;

            celda.Style.Fill.BackgroundColor =
                XLColor.FromArgb(
                    17,
                    71,
                    98);

            celda.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
        }

        ws.Row(
                fila)
            .Height =
            22;
    }


    private static void EscribirParKpi(
        IXLWorksheet ws,
        int fila,
        string nombre1,
        object valor1,
        string nombre2,
        object valor2)
    {
        ws.Cell(
                fila,
                1)
            .Value =
            nombre1;

        AsignarValor(
            ws.Cell(
                fila,
                2),
            valor1);

        ws.Cell(
                fila,
                3)
            .Value =
            nombre2;

        AsignarValor(
            ws.Cell(
                fila,
                4),
            valor2);

        ws.Range(
                fila,
                1,
                fila,
                4)
            .Style.Border.BottomBorder =
            XLBorderStyleValues.Thin;

        ws.Range(
                fila,
                1,
                fila,
                4)
            .Style.Border.BottomBorderColor =
            XLColor.FromArgb(
                225,
                230,
                235);
    }


    private static void AsignarValor(
        IXLCell celda,
        object valor)
    {
        switch (valor)
        {
            case int entero:
                celda.Value =
                    entero;
                break;

            case decimal numero:
                celda.Value =
                    numero;
                break;

            case double numero:
                celda.Value =
                    numero;
                break;

            case string texto:
                celda.Value =
                    texto;
                break;

            default:
                celda.Value =
                    valor.ToString()
                    ?? string.Empty;
                break;
        }
    }


    private static void EstiloSubtitulo(
        IXLRange rango)
    {
        rango.Merge();

        rango.Style.Font.Bold =
            true;

        rango.Style.Font.FontSize =
            12;

        rango.Style.Font.FontColor =
            XLColor.FromArgb(
                17,
                71,
                98);

        rango.Style.Fill.BackgroundColor =
            XLColor.FromArgb(
                235,
                247,
                252);
    }


    private static void FormatoMoneda(
        IXLCell celda)
    {
        celda.Style.NumberFormat.Format =
            "$ #,##0.00;[Red]-$ #,##0.00";
    }


    private static void FinalizarTabla(
        IXLWorksheet ws,
        int filaCabecera,
        int ultimaFila,
        int ultimaColumna)
    {
        if (ultimaFila >
            filaCabecera)
        {
            ws.Range(
                    filaCabecera,
                    1,
                    ultimaFila,
                    ultimaColumna)
                .SetAutoFilter();
        }

        ws.SheetView.FreezeRows(
            filaCabecera);

        AjustarHoja(
            ws);
    }


    private static void AjustarHoja(
        IXLWorksheet ws)
    {
        ws.ColumnsUsed()
            .AdjustToContents(
                1,
                60);

        foreach (var columna in
            ws.ColumnsUsed())
        {
            if (columna.Width >
                42)
            {
                columna.Width =
                    42;
            }

            if (columna.Width <
                10)
            {
                columna.Width =
                    10;
            }
        }

        ws.RowsUsed()
            .Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
    }


    // =============================================================
    // PDF - HEADER
    // =============================================================

    private static void ComponerCabeceraPdf(
        IContainer container,
        ReporteGeneralDto reporte)
    {
        container
            .PaddingBottom(8)
            .BorderBottom(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .Row(row =>
            {
                row.RelativeItem()
                    .Column(column =>
                    {
                        column.Item()
                            .Text(
                                "Bebés Nadadores")
                            .SemiBold()
                            .FontSize(16)
                            .FontColor(
                                Colors.Blue.Medium);

                        column.Item()
                            .Text(
                                "Reporte general")
                            .FontSize(8)
                            .FontColor(
                                Colors.Grey.Darken1);
                    });

                row.RelativeItem()
                    .AlignRight()
                    .Column(column =>
                    {
                        column.Item()
                            .AlignRight()
                            .Text(
                                $"Período: {reporte.Desde:dd/MM/yyyy} al {reporte.Hasta:dd/MM/yyyy}")
                            .SemiBold();

                        column.Item()
                            .AlignRight()
                            .Text(
                                $"Generado: {ObtenerAhoraArgentina():dd/MM/yyyy HH:mm}")
                            .FontSize(7)
                            .FontColor(
                                Colors.Grey.Darken1);
                    });
            });
    }


    // =============================================================
    // PDF - RESUMEN
    // =============================================================

    private static void ComponerResumenPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Resumen");

        column.Item()
            .Row(row =>
            {
                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Bebés activos",
                            reporte.Resumen.BebesActivos.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Asistencia",
                            $"{reporte.Resumen.AsistenciaPromedio:0.#}%"));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Ocupación",
                            $"{reporte.Resumen.OcupacionPromedio:0.#}%"));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Cobrado",
                            Moneda(
                                reporte.Resumen.CobradoPeriodo)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Deuda actual",
                            Moneda(
                                reporte.Resumen.DeudaActual)));
            });

        column.Item()
            .PaddingTop(12);

        column.Item()
            .Table(table =>
            {
                table.ColumnsDefinition(
                    columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                AgregarFilaClaveValor(
                    table,
                    "Altas del período",
                    reporte.Resumen.AltasPeriodo.ToString(
                        Cultura));

                AgregarFilaClaveValor(
                    table,
                    "Clases dictables",
                    reporte.Resumen.ClasesDictables.ToString(
                        Cultura));

                AgregarFilaClaveValor(
                    table,
                    "Clases canceladas",
                    reporte.Resumen.ClasesCanceladas.ToString(
                        Cultura));

                AgregarFilaClaveValor(
                    table,
                    "Recuperos generados",
                    reporte.Recuperos.Generados.ToString(
                        Cultura));

                AgregarFilaClaveValor(
                    table,
                    "Pagos registrados",
                    reporte.Finanzas.PagosPeriodo.ToString(
                        Cultura));
            });
    }


    // =============================================================
    // PDF - ASISTENCIA
    // =============================================================

    private static void ComponerAsistenciaPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Asistencia");

        if (reporte.AsistenciaPorDia.Count ==
            0)
        {
            TextoSinDatos(
                column,
                "No hay asistencias registradas en el período.");

            return;
        }

        column.Item()
            .Table(table =>
            {
                table.ColumnsDefinition(
                    columns =>
                    {
                        columns.ConstantColumn(70);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                table.Header(header =>
                {
                    PdfHeader(
                        header,
                        "Fecha",
                        "Presentes",
                        "Ausentes",
                        "Con aviso",
                        "Pendientes",
                        "Total",
                        "Asistencia");
                });

                foreach (var item in
                    reporte.AsistenciaPorDia)
                {
                    PdfCell(
                        table,
                        item.Fecha.ToString(
                            "dd/MM/yyyy"));

                    PdfCell(
                        table,
                        item.Presentes.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.Ausentes.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.ConAviso.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.Pendientes.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.Total.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        $"{item.PorcentajeAsistencia:0.#}%");
                }
            });
    }


    // =============================================================
    // PDF - OCUPACIÓN
    // =============================================================

    private static void ComponerOcupacionPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Ocupación por nivel");

        if (reporte.OcupacionPorNivel.Count ==
            0)
        {
            TextoSinDatos(
                column,
                "No hay clases para el período.");

            return;
        }

        column.Item()
            .Table(table =>
            {
                table.ColumnsDefinition(
                    columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                table.Header(header =>
                {
                    PdfHeader(
                        header,
                        "Nivel",
                        "Clases",
                        "Cupos",
                        "Ocupados",
                        "Vacantes",
                        "Ocupación");
                });

                foreach (var item in
                    reporte.OcupacionPorNivel)
                {
                    PdfCell(
                        table,
                        item.Nivel);

                    PdfCell(
                        table,
                        item.Clases.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.CuposTotales.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.Ocupados.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        item.Vacantes.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        $"{item.PorcentajeOcupacion:0.#}%");
                }
            });
    }


    // =============================================================
    // PDF - RECUPEROS
    // =============================================================

    private static void ComponerRecuperosPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Recuperos");

        column.Item()
            .Row(row =>
            {
                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Generados",
                            reporte.Recuperos.Generados.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Disponibles",
                            reporte.Recuperos.Disponibles.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Reservados",
                            reporte.Recuperos.Reservados.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Utilizados",
                            reporte.Recuperos.Utilizados.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Vencidos",
                            reporte.Recuperos.Vencidos.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Anulados",
                            reporte.Recuperos.Anulados.ToString(
                                Cultura)));
            });

        if (reporte.Recuperos.PorTipo.Count >
            0)
        {
            column.Item()
                .PaddingTop(10)
                .Table(table =>
                {
                    table.ColumnsDefinition(
                        columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                        });

                    table.Header(header =>
                    {
                        PdfHeader(
                            header,
                            "Motivo",
                            "Cantidad");
                    });

                    foreach (var item in
                        reporte.Recuperos.PorTipo)
                    {
                        PdfCell(
                            table,
                            item.Tipo);

                        PdfCell(
                            table,
                            item.Cantidad.ToString(
                                Cultura));
                    }
                });
        }
    }


    // =============================================================
    // PDF - FINANZAS
    // =============================================================

    private static void ComponerFinanzasPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Finanzas");

        column.Item()
            .Row(row =>
            {
                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Facturado",
                            Moneda(
                                reporte.Finanzas.FacturadoPeriodo)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Cobrado",
                            Moneda(
                                reporte.Finanzas.CobradoPeriodo)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Deuda actual",
                            Moneda(
                                reporte.Finanzas.DeudaActual)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Bebés con deuda",
                            reporte.Finanzas.BebesConDeuda.ToString(
                                Cultura)));
            });

        column.Item()
            .PaddingTop(10)
            .Text(
                "Cobros por medio")
            .SemiBold()
            .FontSize(10);

        if (reporte.Finanzas.PorMedio.Count >
            0)
        {
            column.Item()
                .PaddingTop(4)
                .Table(table =>
                {
                    table.ColumnsDefinition(
                        columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                    table.Header(header =>
                    {
                        PdfHeader(
                            header,
                            "Medio",
                            "Operaciones",
                            "Importe");
                    });

                    foreach (var item in
                        reporte.Finanzas.PorMedio)
                    {
                        PdfCell(
                            table,
                            item.Medio);

                        PdfCell(
                            table,
                            item.Cantidad.ToString(
                                Cultura));

                        PdfCell(
                            table,
                            Moneda(
                                item.Importe));
                    }
                });
        }

        column.Item()
            .PaddingTop(12)
            .Text(
                "Mayores deudores")
            .SemiBold()
            .FontSize(10);

        if (reporte.Finanzas.MayoresDeudores.Count ==
            0)
        {
            TextoSinDatos(
                column,
                "No hay deuda pendiente.");

            return;
        }

        column.Item()
            .PaddingTop(4)
            .Table(table =>
            {
                table.ColumnsDefinition(
                    columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                table.Header(header =>
                {
                    PdfHeader(
                        header,
                        "Bebé",
                        "Cuotas con saldo",
                        "Deuda");
                });

                foreach (var item in
                    reporte.Finanzas.MayoresDeudores)
                {
                    PdfCell(
                        table,
                        item.Bebe);

                    PdfCell(
                        table,
                        item.CuotasPendientes.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        Moneda(
                            item.Deuda));
                }
            });
    }


    // =============================================================
    // PDF - CAJA
    // =============================================================

    private static void ComponerCajaPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Caja");

        column.Item()
            .Row(row =>
            {
                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Ingresos",
                            Moneda(
                                reporte.Caja.Ingresos)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Egresos",
                            Moneda(
                                reporte.Caja.Egresos)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Neto",
                            Moneda(
                                reporte.Caja.Neto)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Movimientos",
                            reporte.Caja.Movimientos.ToString(
                                Cultura)));
            });

        if (reporte.Caja.PorCategoria.Count ==
            0)
        {
            TextoSinDatos(
                column,
                "No hubo movimientos de caja en el período.");

            return;
        }

        column.Item()
            .PaddingTop(10)
            .Table(table =>
            {
                table.ColumnsDefinition(
                    columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                table.Header(header =>
                {
                    PdfHeader(
                        header,
                        "Concepto",
                        "Movimientos",
                        "Ingresos",
                        "Egresos",
                        "Neto");
                });

                foreach (var item in
                    reporte.Caja.PorCategoria)
                {
                    PdfCell(
                        table,
                        item.Categoria);

                    PdfCell(
                        table,
                        item.Cantidad.ToString(
                            Cultura));

                    PdfCell(
                        table,
                        Moneda(
                            item.Ingresos));

                    PdfCell(
                        table,
                        Moneda(
                            item.Egresos));

                    PdfCell(
                        table,
                        Moneda(
                            item.Ingresos -
                            item.Egresos));
                }
            });
    }


    // =============================================================
    // PDF - BANCOS
    // =============================================================

    private static void ComponerBancosPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Bancos");

        column.Item()
            .Row(row =>
            {
                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Ingresos bancarios",
                            Moneda(
                                reporte.Bancos.Ingresos)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Egresos bancarios",
                            Moneda(
                                reporte.Bancos.Egresos)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Neto bancario",
                            Moneda(
                                reporte.Bancos.Neto)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Movimientos",
                            reporte.Bancos.Movimientos.ToString(
                                Cultura)));
            });

        if (reporte.Bancos.Cuentas.Count ==
            0)
        {
            TextoSinDatos(
                column,
                "No hay cuentas bancarias.");

            return;
        }

        foreach (var cuenta in
            reporte.Bancos.Cuentas)
        {
            column.Item()
                .PaddingTop(14)
                .BorderBottom(1)
                .BorderColor(
                    Colors.Grey.Lighten2)
                .PaddingBottom(5)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text(
                            $"{cuenta.Banco} · {cuenta.Cuenta}")
                        .SemiBold()
                        .FontSize(10);

                    row.RelativeItem()
                        .AlignRight()
                        .Text(
                            $"Ingresos {Moneda(cuenta.Ingresos)} · " +
                            $"Egresos {Moneda(cuenta.Egresos)} · " +
                            $"Neto {Moneda(cuenta.Neto)}")
                        .FontSize(7.5f)
                        .FontColor(
                            Colors.Grey.Darken1);
                });

            if (cuenta.Detalle.Count ==
                0)
            {
                column.Item()
                    .PaddingVertical(6)
                    .Text(
                        "Sin movimientos en el período.")
                    .FontSize(7.5f)
                    .FontColor(
                        Colors.Grey.Darken1);

                continue;
            }

            column.Item()
                .PaddingTop(5)
                .Table(table =>
                {
                    table.ColumnsDefinition(
                        columns =>
                        {
                            columns.ConstantColumn(72);
                            columns.ConstantColumn(48);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(2.3f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn();
                        });

                    table.Header(header =>
                    {
                        PdfHeader(
                            header,
                            "Fecha",
                            "Tipo",
                            "Origen",
                            "Concepto",
                            "Referencia",
                            "Importe");
                    });

                    foreach (var movimiento in
                        cuenta.Detalle)
                    {
                        PdfCell(
                            table,
                            ObtenerFechaArgentina(
                                    movimiento.FechaUtc)
                                .ToString(
                                    "dd/MM/yyyy HH:mm"));

                        PdfCell(
                            table,
                            movimiento.Tipo);

                        PdfCell(
                            table,
                            movimiento.Origen);

                        PdfCell(
                            table,
                            movimiento.Concepto);

                        PdfCell(
                            table,
                            movimiento.Referencia
                            ?? "—");

                        PdfCell(
                            table,
                            (
                                movimiento.Tipo ==
                                "Egreso"
                                    ? "-"
                                    : "+"
                            )
                            +
                            Moneda(
                                movimiento.Importe));
                    }
                });
        }
    }


    // =============================================================
    // PDF - SALUD
    // =============================================================

    private static void ComponerSaludPdf(
        ColumnDescriptor column,
        ReporteGeneralDto reporte)
    {
        TituloSeccion(
            column,
            "Aptos médicos - estado actual");

        column.Item()
            .Row(row =>
            {
                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Vigentes",
                            reporte.Salud.Vigentes.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Por vencer",
                            reporte.Salud.PorVencer.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Vencidos",
                            reporte.Salud.Vencidos.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Sin apto",
                            reporte.Salud.SinApto.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "Observados",
                            reporte.Salud.Observados.ToString(
                                Cultura)));

                row.RelativeItem()
                    .Element(c =>
                        KpiPdf(
                            c,
                            "No aptos",
                            reporte.Salud.NoAptos.ToString(
                                Cultura)));
            });

        column.Item()
            .PaddingTop(12)
            .Text(
                "La sección médica representa el estado actual al momento de generar el reporte.")
            .FontSize(7.5f)
            .FontColor(
                Colors.Grey.Darken1);
    }


    // =============================================================
    // PDF - HELPERS
    // =============================================================

    private static void TituloSeccion(
        ColumnDescriptor column,
        string titulo)
    {
        column.Item()
            .PaddingBottom(8)
            .Text(
                titulo)
            .SemiBold()
            .FontSize(14)
            .FontColor(
                Colors.Blue.Medium);
    }


    private static void KpiPdf(
        IContainer container,
        string titulo,
        string valor)
    {
        container
            .PaddingRight(6)
            .Border(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .Background(
                Colors.Grey.Lighten5)
            .Padding(8)
            .Column(column =>
            {
                column.Item()
                    .Text(
                        titulo)
                    .FontSize(7)
                    .FontColor(
                        Colors.Grey.Darken1);

                column.Item()
                    .PaddingTop(2)
                    .Text(
                        valor)
                    .SemiBold()
                    .FontSize(11);
            });
    }


    private static void AgregarFilaClaveValor(
        TableDescriptor table,
        string clave,
        string valor)
    {
        PdfCell(
            table,
            clave,
            bold: true);

        PdfCell(
            table,
            valor);
    }


    private static void PdfHeader(
        TableCellDescriptor header,
        params string[] textos)
    {
        foreach (var texto in
            textos)
        {
            header.Cell()
                .Background(
                    Colors.Blue.Medium)
                .PaddingVertical(5)
                .PaddingHorizontal(4)
                .Text(
                    texto)
                .SemiBold()
                .FontSize(7)
                .FontColor(
                    Colors.White);
        }
    }


    private static void PdfCell(
        TableDescriptor table,
        string texto,
        bool bold = false)
    {
        var descriptor =
            table.Cell()
                .BorderBottom(1)
                .BorderColor(
                    Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .PaddingHorizontal(4);

        var text =
            descriptor.Text(
                texto);

        if (bold)
        {
            text.SemiBold();
        }

        text.FontSize(
            7.2f);
    }


    private static void TextoSinDatos(
        ColumnDescriptor column,
        string texto)
    {
        column.Item()
            .PaddingVertical(12)
            .Text(
                texto)
            .FontColor(
                Colors.Grey.Darken1);
    }


    // =============================================================
    // COMUNES
    // =============================================================

    private static string Moneda(
        decimal importe)
    {
        return importe.ToString(
            "C0",
            Cultura);
    }


    private static DateTime ObtenerFechaArgentina(
        DateTimeOffset fechaUtc)
    {
        return TimeZoneInfo.ConvertTime(
                fechaUtc,
                ObtenerZonaArgentina())
            .DateTime;
    }


    private static DateTimeOffset ObtenerAhoraArgentina()
    {
        return TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            ObtenerZonaArgentina());
    }


    private static TimeZoneInfo ObtenerZonaArgentina()
    {
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
    }
}
