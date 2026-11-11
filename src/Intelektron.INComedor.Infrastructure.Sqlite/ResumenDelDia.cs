namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Lo que la señal de vida cuenta de un día: consumos, rechazos, comprobantes sin imprimir y advertencias.</summary>
public sealed record ResumenDia(int Consumos, int Rechazos, int SinImprimir, int Advertencias);

/// <summary>
/// El resumen de un período de la bitácora general de la base, para la señal de vida diaria (D5 de avisar-por-correo): sólo
/// cuenta tipos de evento, sin leer nombres, personas ni códigos.
/// </summary>
public sealed class ResumenDelDia
{
    private readonly BaseComedor _baseComedor;

    public ResumenDelDia(BaseComedor baseComedor)
    {
        _baseComedor = baseComedor;
    }

    /// <summary>
    /// Los eventos con el instante del hecho en [desde, hasta): los consumos autorizados, los rechazos (identificación
    /// rechazada y consumo denegado), los comprobantes que terminaron fallidos o inciertos y las advertencias o errores.
    /// </summary>
    public ResumenDia Contar(DateTimeOffset desde, DateTimeOffset hasta)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT
                COALESCE(SUM(combitTipoEvento = 'CONSUMPTION_AUTHORIZED'), 0),
                COALESCE(SUM(combitTipoEvento IN ('IDENTIFICATION_REJECTED', 'CONSUMPTION_DENIED')), 0),
                COALESCE(SUM(combitTipoEvento = 'PRINT_JOB_UPDATED' AND (json_extract(combitContexto, '$.State') = 'FAILED'
                    OR (json_extract(combitContexto, '$.State') = 'UNKNOWN' AND json_extract(combitContexto, '$.PreviousState') = 'UNKNOWN'))), 0),
                COALESCE(SUM(combitSeveridad IN ($advertencia, $error)), 0)
            FROM tbComedorBitacora
            WHERE combitFechaHechoUtc >= $desde AND combitFechaHechoUtc < $hasta
            """;
        comando.Parameters.AddWithValue("$desde", FormatoInstante.Texto(desde));
        comando.Parameters.AddWithValue("$hasta", FormatoInstante.Texto(hasta));
        comando.Parameters.AddWithValue("$advertencia", SeveridadBitacora.Warning);
        comando.Parameters.AddWithValue("$error", SeveridadBitacora.Error);
        using var lector = comando.ExecuteReader();
        lector.Read();
        return new ResumenDia(lector.GetInt32(0), lector.GetInt32(1), lector.GetInt32(2), lector.GetInt32(3));
    }
}
