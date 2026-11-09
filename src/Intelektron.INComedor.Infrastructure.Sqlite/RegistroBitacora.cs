using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// Escritura de un registro de bitácora dentro de la transacción de quien lo pide, con secuencia por puesto y
/// generación (D9 de atender-seleccion-y-confirmacion). El contexto nunca lleva el código de identificación.
/// </summary>
internal sealed class RegistroBitacora
{
    // Hay una sola generación de bitácora; la generación nueva al restaurar una base queda fuera de
    // escribir-bitacoras-externas (punto 1.1 de su gate).
    internal static readonly Guid GeneracionBitacora = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// El arranque de este proceso: todos los registros que escribe llevan el mismo BootId, y el escritor de bitácoras
    /// marca como recuperado lo que llega de otro arranque (D5 de escribir-bitacoras-externas).
    /// </summary>
    internal static readonly Guid ArranqueDelProceso = Guid.NewGuid();

    private readonly Guid _arranque;
    private readonly string _componente;

    public RegistroBitacora(string componente)
        : this(ArranqueDelProceso, componente)
    {
    }

    public RegistroBitacora(Guid arranque, string componente)
    {
        _arranque = arranque;
        _componente = componente;
    }

    public void Insertar(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        PuestoLocal puesto,
        DateTimeOffset ahora,
        string tipoEvento,
        object contexto,
        string severidad = SeveridadBitacora.Info)
    {
        var secuencia = ComandosSqlite.EscalarLong(conexion, transaccion,
            "SELECT COALESCE(MAX(combitSecuencia), 0) + 1 FROM tbComedorBitacora WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen",
            ("$pue", puesto.Id), ("$gen", GeneracionBitacora));
        var local = TimeZoneInfo.ConvertTime(ahora, puesto.ZonaHoraria);
        var desplazamiento = puesto.ZonaHoraria.GetUtcOffset(ahora);
        var ahoraTexto = FormatoInstante.Texto(ahora);

        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorBitacora (combitCodigo, compueCodigo, combitCodigoGeneracion, combitSecuencia, combitCodigoArranque,
                combitFechaHechoUtc, combitFechaRegistroUtc, combitZonaHoraria, combitDesplazamientoUtc, combitMesBitacora,
                combitTipoEvento, combitSeveridad, combitComponente, combitVersionEsquema, combitContexto)
            VALUES ($id, $pue, $gen, $sec, $boot, $ahora, $ahora, $zona, $offset, $mes, $tipo, $severidad, $componente, 1, $contexto)
            """,
            ("$severidad", severidad),
            ("$id", Guid.NewGuid()), ("$pue", puesto.Id), ("$gen", GeneracionBitacora), ("$sec", secuencia),
            ("$boot", _arranque), ("$ahora", ahoraTexto), ("$zona", puesto.ZonaHoraria.Id),
            ("$offset", (desplazamiento < TimeSpan.Zero ? "-" : "+") + desplazamiento.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture)),
            ("$mes", local.ToString("yyyyMM", CultureInfo.InvariantCulture)), ("$tipo", tipoEvento),
            ("$componente", _componente), ("$contexto", JsonSerializer.Serialize(contexto)));
    }
}

/// <summary>La severidad es del hecho y se guarda con él (D12 de escribir-bitacoras-externas).</summary>
public static class SeveridadBitacora
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";
}
