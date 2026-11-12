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
    // En el primer corte hay una sola generación de bitácora; el cambio de generación tras restaurar
    // una base es de la segunda ola (bitácoras mensuales).
    private static readonly Guid GeneracionBitacora = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Guid _arranque;
    private readonly string _componente;

    public RegistroBitacora(string componente)
        : this(Guid.NewGuid(), componente)
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
        object contexto)
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
            VALUES ($id, $pue, $gen, $sec, $boot, $ahora, $ahora, $zona, $offset, $mes, $tipo, 'INFO', $componente, 1, $contexto)
            """,
            ("$id", Guid.NewGuid()), ("$pue", puesto.Id), ("$gen", GeneracionBitacora), ("$sec", secuencia),
            ("$boot", _arranque), ("$ahora", ahoraTexto), ("$zona", puesto.ZonaHoraria.Id),
            ("$offset", (desplazamiento < TimeSpan.Zero ? "-" : "+") + desplazamiento.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture)),
            ("$mes", local.ToString("yyyyMM", CultureInfo.InvariantCulture)), ("$tipo", tipoEvento),
            ("$componente", _componente), ("$contexto", JsonSerializer.Serialize(contexto)));
    }
}
