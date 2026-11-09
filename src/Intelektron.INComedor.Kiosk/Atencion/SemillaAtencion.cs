using System.IO;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Agrega los datos sintéticos de atención sobre la base de la demostración, sin tocar sus filas (D10 de
/// atender-seleccion-y-confirmacion). Corre después de la semilla de la demostración, en su propia transacción: la parte
/// común y las opciones del catálogo de quince o del de nueve (D6 de presentar-atencion-tactil).
/// </summary>
public static class SemillaAtencion
{
    private const string RecursoComun = "Atencion.semilla_atencion_comun.sql";
    private const string RecursoQuince = "Atencion.opciones_quince.sql";
    private const string RecursoNueve = "Atencion.opciones_nueve.sql";

    /// <summary>La primera opción que agrega, en los dos catálogos: si ya existe, la semilla no hace nada.</summary>
    public static readonly Guid PrimeraOpcionId = Guid.Parse("00000000-0000-4000-8000-000000000011");

    public static void Aplicar(BaseComedor baseComedor, bool catalogoNueve = false)
    {
        using var conexion = baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);

        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = transaccion;
            consulta.CommandText = "SELECT COUNT(*) FROM tbComedorOpcionesMenu WHERE commenCodigo = $men";
            consulta.Parameters.AddWithValue("$men", PrimeraOpcionId);
            if (Convert.ToInt64(consulta.ExecuteScalar()) == 0)
            {
                using var siembra = conexion.CreateCommand();
                siembra.Transaction = transaccion;
                siembra.CommandText = LeerRecurso(RecursoComun) + "\n" + LeerRecurso(catalogoNueve ? RecursoNueve : RecursoQuince);
                siembra.ExecuteNonQuery();
            }
        }

        transaccion.Commit();
    }

    private static string LeerRecurso(string recurso)
    {
        using var flujo = typeof(SemillaAtencion).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {recurso}.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }
}
