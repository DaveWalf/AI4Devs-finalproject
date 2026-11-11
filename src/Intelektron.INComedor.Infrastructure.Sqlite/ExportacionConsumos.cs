using System.Globalization;
using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Los formatos de la exportación de consumos (D6 de buscar-y-exportar-consumos).</summary>
public enum FormatoExportacion
{
    Csv,
    Xlsx,
}

/// <summary>
/// El archivo escrito, sólo su nombre y nunca la ruta, las filas que tiene y si la consulta tenía más que el tope; o el
/// motivo por el que el filtro se rechazó sin escribir nada.
/// </summary>
public sealed record ResultadoExportacion(string? Archivo, int Filas, bool HayMas, string? Rechazo);

/// <summary>
/// La exportación de la consulta de consumos (consulta-consumos de buscar-y-exportar-consumos, D6): lee los consumos del
/// filtro aplicado, con su orden y hasta 10 000, y los escribe con las columnas de la tabla, sin ningún código, con
/// <see cref="EscritorPlanilla"/>: el temporal, el nombre libre del segundo, el apóstrofo de las fórmulas y el libro mínimo.
/// </summary>
public sealed class ExportacionConsumos
{
    public const int Tope = 10_000;

    /// <summary>Cuántos nombres prueba en el mismo segundo: el de siempre y de <c>-2</c> a <c>-9</c>.</summary>
    public const int NombresPorSegundo = EscritorPlanilla.NombresPorSegundo;

    private static readonly string[] Encabezado = ["Fecha y hora", "N.º", "Persona", "Opción", "Servicio", "Cargo", "Moneda", "Estado"];

    private readonly ConsultaConsumos _consulta;

    /// <summary>Las columnas de la exportación, las de la tabla de Consumos (D1 de exportar-consumos-automaticamente).</summary>
    public static IReadOnlyList<string> Columnas => Encabezado;

    /// <summary>Una fila exportada, sin ningún código: la misma de la exportación a mano.</summary>
    public static IReadOnlyList<object> FilaDe(ConsumoConsultado consumo, TimeZoneInfo zona) => Fila(consumo, zona);

    public ExportacionConsumos(ConsultaConsumos consulta)
    {
        _consulta = consulta;
    }

    /// <summary>
    /// Escribe el archivo y devuelve su nombre. Sin poder escribir en <paramref name="carpeta"/>, o con los nueve nombres de
    /// ese segundo ya tomados, lanza la excepción de E/S después de borrar el temporal; lo que ya estaba en la carpeta no se
    /// toca. Un doble toque en el mismo segundo deja el segundo archivo con <c>-2</c> antes de la extensión.
    /// </summary>
    public ResultadoExportacion Exportar(FiltroConsumos filtro, FormatoExportacion formato, string carpeta, TimeZoneInfo zona)
    {
        var resultado = _consulta.Leer(filtro, Tope);
        if (resultado.Rechazo is { } rechazo)
        {
            return new ResultadoExportacion(null, 0, false, rechazo);
        }

        var ahora = TimeZoneInfo.ConvertTime(_consulta.Ahora(), zona);
        var raiz = "INComedor-consumos-" + ahora.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var nombre = EscritorPlanilla.Escribir(carpeta, raiz, formato, "Consumos", Encabezado, resultado.Filas.Select(f => Fila(f, zona)));
        return new ResultadoExportacion(nombre, resultado.Filas.Count, resultado.HayMas, null);
    }

    /// <summary>El estado del comprobante como lo dice la tabla de Consumos.</summary>
    public static string TextoEstado(EstadoTrabajoImpresion? estado) => estado switch
    {
        EstadoTrabajoImpresion.Submitted => "Enviado",
        EstadoTrabajoImpresion.Failed => "Falló",
        EstadoTrabajoImpresion.Unknown => "Incierto",
        null => "—",
        _ => "En cola",
    };

    /// <summary>Una fila exportada: los textos de la tabla y el cargo como número, en las unidades de su moneda.</summary>
    private static IReadOnlyList<object> Fila(ConsumoConsultado consumo, TimeZoneInfo zona)
    {
        var local = TimeZoneInfo.ConvertTime(consumo.ConfirmadoUtc, zona);
        var divisor = 1m;
        for (var i = 0; i < consumo.Escala; i++)
        {
            divisor *= 10;
        }

        return
        [
            local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), consumo.Numero ?? string.Empty, consumo.Persona, consumo.Opcion,
            consumo.Servicio, new NumeroPlanilla(consumo.CargoUnidadesMenores / divisor, consumo.Escala), consumo.Moneda,
            TextoEstado(consumo.EstadoComprobante),
        ];
    }
}
