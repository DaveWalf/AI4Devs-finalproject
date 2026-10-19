using System.Text;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public class ReplayTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "AT26 · Doble toque con el mismo IntentId: devuelve el resultado original y no cambia ninguna tabla")]
    public void Doble_toque_devuelve_el_resultado_original()
    {
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);
        var original = Unidad.Confirmar(solicitud);
        using var conexion = Abrir();
        var antes = Sql.ConteoDeEscritura(conexion);

        var repetido = Unidad.Confirmar(solicitud);

        Assert.Equal(TipoResultado.Autorizada, original.Tipo);
        Assert.Equal(original, repetido);
        Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));
    }

    [Fact(DisplayName = "AT26 · Otra intención con una sesión ya usada: la sesión no es utilizable y no se escribe ninguna fila")]
    public void Otra_intencion_con_una_sesion_ya_usada_no_escribe_nada()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);
        Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion));
        using var conexion = Abrir();
        var antes = Sql.ConteoDeEscritura(conexion);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio, sesion));

        Assert.Equal(TipoResultado.SesionNoUtilizable, resultado.Tipo);
        Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorConsumos"));
    }

    [Fact(DisplayName = "AT35 · Replay después de vencer la sesión o cambiar el padrón o el catálogo: devuelve la decisión original")]
    public void Replay_ignora_sesion_vencida_baja_de_padron_y_cambio_de_catalogo()
    {
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);
        var sesionSinUsar = NuevaSesion(Fixture.Habilitada);
        var original = Unidad.Confirmar(solicitud);
        using var conexion = Abrir();

        Reloj.Ahora = Reloj.Ahora.AddHours(2);
        Sql.Ejecutar(conexion, "UPDATE tbComedorPersonas SET comperActivo = 0 WHERE comperCodigo = $per", ("$per", Fixture.Habilitada.Id));
        Sql.Ejecutar(conexion, "UPDATE tbComedorOpcionesMenu SET commenVersionMenu = 2 WHERE commenCodigo = $men", ("$men", Fixture.AlmuerzoComun.Id));
        var antes = Sql.ConteoDeEscritura(conexion);

        var repetido = Unidad.Confirmar(solicitud);

        Assert.Equal(original, repetido);
        Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));

        // Una intención distinta con una sesión vencida se evalúa con el estado actual: no escribe nada.
        var conSesionVencida = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio, sesionSinUsar));
        var conLaSesionYaUsada = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio, solicitud.IdentitySessionId));

        Assert.Equal(TipoResultado.SesionNoUtilizable, conSesionVencida.Tipo);
        Assert.Equal(TipoResultado.SesionNoUtilizable, conLaSesionYaUsada.Tipo);
        Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));
    }

    [Fact(DisplayName = "AT25 · Mismo IntentId con otro contenido: conflicto, sin escribir y con la fila original igual byte por byte")]
    public void Mismo_intentid_con_otro_contenido_es_conflicto()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion);
        Unidad.Confirmar(solicitud);
        using var conexion = Abrir();
        var antes = Sql.ConteoDeEscritura(conexion);
        var filaAntes = FilaComoTexto(conexion, solicitud.IntentId);

        var otroContenido = solicitud with { MenuItemId = Fixture.AlmuerzoOpcional.Id };
        var resultado = Unidad.Confirmar(otroContenido);

        Assert.Equal(TipoResultado.Conflicto, resultado.Tipo);
        Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));
        Assert.Equal(filaAntes, FilaComoTexto(conexion, solicitud.IntentId));
    }

    private static string FilaComoTexto(SqliteConnection conexion, Guid intentId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT * FROM tbComedorIntencionesConsumo WHERE comicCodigo = $ic";
        comando.Parameters.AddWithValue("$ic", intentId);
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());
        var texto = new StringBuilder();
        for (var i = 0; i < lector.FieldCount; i++)
        {
            var valor = lector.IsDBNull(i) ? "NULL" : lector.GetValue(i) is byte[] bytes ? Convert.ToHexString(bytes) : lector.GetValue(i).ToString();
            texto.Append(lector.GetName(i)).Append('=').Append(valor).Append('|');
        }

        return texto.ToString();
    }
}
