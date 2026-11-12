using System.Security.Cryptography;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La máquina de estados de la atención (D5 y D6 de atender-seleccion-y-confirmacion), con SQLite real, un reloj
/// manual y la impresora de prueba como único doble. Las opciones son las de la semilla de atención.
/// </summary>
public class MaquinaAtencionTests : IDisposable
{
    private static readonly Guid AlmuerzoComun = SemillaAtencion.PrimeraOpcionId;

    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");
    private readonly ImpresoraDePrueba _impresora = new();

    public void Dispose() => _archivo.Dispose();

    private MaquinaAtencion NuevaMaquina()
    {
        var maquina = new MaquinaAtencion(new ServicioAtencion(_archivo.Ruta, _reloj, _impresora));
        maquina.Iniciar();
        return maquina;
    }

    [Fact(DisplayName = "AT16 · Recorrer un catálogo de 15 opciones: cada una aparece una sola vez; en la primera página no se retrocede y en la última no se avanza")]
    public void Recorre_las_15_opciones_paginadas()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");

        var vistas = new List<Guid>();
        Assert.False(maquina.PuedeRetroceder);
        vistas.AddRange(maquina.PaginaActual.Select(o => o.Id));
        while (maquina.PuedeAvanzar)
        {
            maquina.Siguiente();
            vistas.AddRange(maquina.PaginaActual.Select(o => o.Id));
        }

        Assert.False(maquina.PuedeAvanzar);
        while (maquina.PuedeRetroceder)
        {
            maquina.Anterior();
        }

        Assert.Equal(1, maquina.Pagina);
        Assert.Equal(15, vistas.Count);
        Assert.Equal(15, vistas.Distinct().Count());
        Assert.Equal(3, maquina.TotalPaginas);
        Assert.All(maquina.PaginaActual, o => Assert.StartsWith("ARS ", o.Cargo));
    }

    [Fact(DisplayName = "HU01-B · Cancelar antes de confirmar: vuelve a reposo sin intención ni consumo nuevos")]
    public void Cancelar_vuelve_a_reposo_sin_escribir()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);

        maquina.Cancelar();

        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Null(maquina.NombreVisible);
        Assert.Equal((0L, 0L), IntencionesYConsumos());
    }

    [Fact(DisplayName = "AT16 · Treinta segundos sin actividad: vuelve a reposo sin escribir, y la persona siguiente no ve el nombre ni la elección de la anterior")]
    public void Treinta_segundos_sin_actividad_vuelven_a_reposo()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        var anterior = maquina.NombreVisible;

        _reloj.Avanzar(TimeSpan.FromSeconds(30));
        maquina.RevisarInactividad();

        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Equal((0L, 0L), IntencionesYConsumos());
        Identificar(maquina, "9001");
        Assert.NotEqual(anterior, maquina.NombreVisible);
        Assert.Null(maquina.Elegida);
        Assert.Equal(1, maquina.Pagina);
    }

    [Fact(DisplayName = "AT16 · Un toque reinicia el plazo: con «Siguiente» a los 29 s sigue en la selección a los 45 s y vuelve a reposo a los 30 s del último toque")]
    public void Un_toque_reinicia_el_plazo()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");

        _reloj.Avanzar(TimeSpan.FromSeconds(29));
        maquina.Siguiente();
        _reloj.Avanzar(TimeSpan.FromSeconds(16));
        maquina.RevisarInactividad();
        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);

        _reloj.Avanzar(TimeSpan.FromSeconds(13.999));
        maquina.RevisarInactividad();
        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);

        _reloj.Avanzar(TimeSpan.FromMilliseconds(1));
        maquina.RevisarInactividad();
        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
    }

    [Fact(DisplayName = "AT01 · HU01-A · Autorización sin red: «Consumo registrado. Retirá tu comprobante» y un solo consumo de esa persona y esa opción, con comprobante")]
    public async Task Autoriza_y_registra_un_consumo()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        var persona = maquina.PersonaId;
        maquina.Elegir(AlmuerzoComun);

        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Resultado, maquina.Estado);
        Assert.Equal("Consumo registrado. Retirá tu comprobante", maquina.Mensaje);
        Assert.True(maquina.ConsumoRegistrado);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
        Assert.Equal(persona, Guid.Parse(Escalar<string>(conexion, "SELECT comperCodigo FROM tbComedorConsumos")));
        Assert.Equal(AlmuerzoComun, Guid.Parse(Escalar<string>(conexion, "SELECT commenCodigo FROM tbComedorConsumos")));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorComprobantes"));
    }

    [Fact(DisplayName = "AT26 · HU01-G · Doble toque: las dos confirmaciones usan el mismo IntentId, y hay una sola intención y un solo consumo")]
    public async Task Doble_toque_usa_el_mismo_IntentId()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        Task primera, segunda;
        Guid? intentPrimera, intentSegunda;

        using (var bloqueo = RetenerEscritura())
        {
            primera = maquina.ConfirmarAsync();
            intentPrimera = maquina.IntentId;
            Assert.Equal(EstadoAtencion.Confirmando, maquina.Estado);
            segunda = maquina.ConfirmarAsync();
            intentSegunda = maquina.IntentId;
        }

        await Task.WhenAll(primera, segunda);

        Assert.Same(primera, segunda);
        Assert.Equal(intentPrimera, intentSegunda);
        Assert.Equal((1L, 1L), IntencionesYConsumos());
        using var conexion = Abrir();
        Assert.Equal(intentPrimera, Guid.Parse(Escalar<string>(conexion, "SELECT comicCodigo FROM tbComedorIntencionesConsumo")));
    }

    [Fact(DisplayName = "AT26 · HU01-G · Otra identificación durante la confirmación: el puesto señala ocupado, no abre otra sesión y el consumo es de la persona original")]
    public async Task Otra_identificacion_durante_la_confirmacion_no_cambia_la_persona()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        var original = maquina.PersonaId;
        maquina.Elegir(AlmuerzoComun);
        Task confirmacion;
        ResultadoToque toque;

        using (var bloqueo = RetenerEscritura())
        {
            confirmacion = maquina.ConfirmarAsync();
            toque = maquina.Identificar("9001");
            Assert.Equal("Puesto ocupado: hay una confirmación en curso", maquina.Aviso);
        }

        await confirmacion;

        Assert.Equal(ResultadoToque.Ocupado, toque);
        Assert.Equal(original, maquina.PersonaId);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"));
        Assert.Equal(original, Guid.Parse(Escalar<string>(conexion, "SELECT comperCodigo FROM tbComedorConsumos")));
    }

    [Fact(DisplayName = "AT02 · Cada rechazo con su mensaje: LOCAL_LIMIT_REACHED, SERVICE_NOT_ENABLED, RULE_DENIED, PERSON_INACTIVE, CREDENTIAL_REVOKED y CREDENTIAL_EXPIRED dan seis mensajes distintos, sin consumo informado")]
    public async Task Cada_rechazo_tiene_su_mensaje()
    {
        var mensajes = new Dictionary<string, string>();
        foreach (var motivo in new[] { "LOCAL_LIMIT_REACHED", "SERVICE_NOT_ENABLED", "RULE_DENIED", "PERSON_INACTIVE", "CREDENTIAL_REVOKED", "CREDENTIAL_EXPIRED" })
        {
            using var archivo = new ArchivoTemporal();
            var maquina = new MaquinaAtencion(new ServicioAtencion(archivo.Ruta, _reloj, _impresora));
            maquina.Iniciar();
            var codigo = PrepararRechazo(archivo.Ruta, motivo);
            if (motivo == "LOCAL_LIMIT_REACHED")
            {
                Identificar(maquina, codigo);
                maquina.Elegir(AlmuerzoComun);
                await maquina.ConfirmarAsync();
                maquina.Listo();
            }

            Identificar(maquina, codigo);
            maquina.Elegir(AlmuerzoComun);
            DespuesDeIdentificar(archivo.Ruta, motivo, codigo);

            await maquina.ConfirmarAsync();

            Assert.Equal(EstadoAtencion.Resultado, maquina.Estado);
            Assert.False(maquina.ConsumoRegistrado);
            Assert.Equal(motivo, maquina.MotivoResultado);
            mensajes[motivo] = maquina.Mensaje!;
        }

        Assert.Equal(6, mensajes.Values.Distinct().Count());
        Assert.DoesNotContain(mensajes.Values, m => m.Contains("registrado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "AT35 · La sesión venció mientras elegía: pide identificarse de nuevo y vuelve a reposo, sin escribir nada")]
    public async Task Sesion_vencida_pide_identificarse_de_nuevo()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        for (var i = 0; i < 7; i++)
        {
            _reloj.Avanzar(TimeSpan.FromSeconds(20));
            if (maquina.PuedeAvanzar)
            {
                maquina.Siguiente();
            }
            else
            {
                maquina.Anterior();
            }

            maquina.RevisarInactividad();
        }

        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Equal("Tu sesión venció. Identificate de nuevo", maquina.Aviso);
        Assert.Equal((0L, 0L), IntencionesYConsumos());
    }

    [Fact(DisplayName = "TK02.1 · HU03-E · Vuelta a reposo después del resultado: con «Listo» o a los 10 s, sin el nombre ni la elección de esa persona")]
    public async Task Vuelve_a_reposo_despues_del_resultado()
    {
        var conListo = NuevaMaquina();
        Identificar(conListo, "0001");
        conListo.Elegir(AlmuerzoComun);
        await conListo.ConfirmarAsync();

        conListo.Listo();

        AssertReposoLimpio(conListo);

        var porPlazo = NuevaMaquina();
        Identificar(porPlazo, "0001");
        porPlazo.Elegir(SemillaAtencionRefrigerio());
        await porPlazo.ConfirmarAsync();
        _reloj.Avanzar(TimeSpan.FromSeconds(9.999));
        porPlazo.RevisarInactividad();
        Assert.Equal(EstadoAtencion.Resultado, porPlazo.Estado);
        _reloj.Avanzar(TimeSpan.FromMilliseconds(1));
        porPlazo.RevisarInactividad();

        AssertReposoLimpio(porPlazo);
    }

    [Fact(DisplayName = "TK03.1 · Base de una versión de esquema desconocida: la atención queda en error de almacenamiento y el archivo sin cambios")]
    public void Base_de_version_desconocida_queda_en_error()
    {
        using (var conexion = Abrir())
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "PRAGMA user_version = 99; CREATE TABLE futura (id INTEGER)";
            comando.ExecuteNonQuery();
        }

        var hash = Hash();

        var maquina = NuevaMaquina();

        Assert.Equal(EstadoAtencion.ErrorAlmacenamiento, maquina.Estado);
        Assert.Equal(ResultadoToque.NoDisponible, maquina.Identificar("0001"));
        Assert.Equal(hash, Hash());
    }

    [Fact(DisplayName = "AT47 · HU01-D · Cambio de precio entre la selección y la confirmación: muestra 1.800,00 y pide reconfirmar, sin intención nueva")]
    public async Task Cambio_de_precio_pide_reconfirmar()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        Assert.Equal("ARS 1.500,00", maquina.Elegida!.Cargo);
        CambiarPrecio(AlmuerzoComun, version: 2, importe: 180000);

        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Reconfirmacion, maquina.Estado);
        Assert.Equal("ARS 1.800,00", maquina.Vigente!.Cargo);
        Assert.Equal(2, maquina.Vigente.Version);
        Assert.Equal((0L, 0L), IntencionesYConsumos());
    }

    [Fact(DisplayName = "AT47 · Reconfirmar con el valor vigente: AUTHORIZED con la versión 2 y 1.800,00, una sola intención con un IntentId distinto del primer intento")]
    public async Task Reconfirmar_autoriza_con_la_version_vigente()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        CambiarPrecio(AlmuerzoComun, version: 2, importe: 180000);
        await maquina.ConfirmarAsync();
        var primerIntento = maquina.IntentId;

        await maquina.ReconfirmarAsync();

        Assert.Equal(EstadoAtencion.Resultado, maquina.Estado);
        Assert.True(maquina.ConsumoRegistrado);
        Assert.NotEqual(primerIntento, maquina.IntentId);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorIntencionesConsumo"));
        Assert.Equal(maquina.IntentId, Guid.Parse(Escalar<string>(conexion, "SELECT comicCodigo FROM tbComedorIntencionesConsumo")));
        Assert.Equal(2L, Escalar<long>(conexion, "SELECT comconVersionMenu FROM tbComedorConsumos"));
        Assert.Contains("\"EmployeeChargeMinor\":180000", Escalar<string>(conexion, "SELECT comconPrecioAplicado FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "HU01-D · Volver sin reconfirmar: vuelve a la selección y no se escribió ninguna intención")]
    public async Task Volver_sin_reconfirmar()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        CambiarPrecio(AlmuerzoComun, version: 2, importe: 180000);
        await maquina.ConfirmarAsync();

        maquina.Volver();

        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        Assert.Equal((0L, 0L), IntencionesYConsumos());
    }

    [Fact(DisplayName = "AT47 · Opción retirada del catálogo: vuelve a la selección con el aviso, sin ofrecerla y sin intención")]
    public async Task Opcion_retirada_vuelve_a_la_seleccion()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(AlmuerzoComun);
        using (var conexion = Abrir())
        {
            Ejecutar(conexion, "DELETE FROM tbComedorOpcionesMenu_CategoriasCupo WHERE commenCodigo = $men", ("$men", AlmuerzoComun));
            Ejecutar(conexion, "DELETE FROM tbComedorOpcionesMenu WHERE commenCodigo = $men", ("$men", AlmuerzoComun));
        }

        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        Assert.Equal("La opción elegida ya no está disponible. Elegí otra", maquina.Aviso);
        Assert.Null(maquina.Elegida);
        var ofrecidas = new List<Guid>(maquina.PaginaActual.Select(o => o.Id));
        while (maquina.PuedeAvanzar)
        {
            maquina.Siguiente();
            ofrecidas.AddRange(maquina.PaginaActual.Select(o => o.Id));
        }

        Assert.DoesNotContain(AlmuerzoComun, ofrecidas);
        Assert.Equal(14, ofrecidas.Count);
        Assert.Equal((0L, 0L), IntencionesYConsumos());
    }

    private static void Identificar(MaquinaAtencion maquina, string codigo)
    {
        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar(codigo));
        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
    }

    private static void AssertReposoLimpio(MaquinaAtencion maquina)
    {
        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Null(maquina.NombreVisible);
        Assert.Null(maquina.Elegida);
        Assert.Null(maquina.Mensaje);
    }

    private static Guid SemillaAtencionRefrigerio() => Guid.Parse("00000000-0000-4000-8000-000000000019");

    /// <summary>
    /// Arma la persona del rechazo pedido con un código propio. Los de credencial se revocan o vencen después de
    /// identificarse, en <see cref="DespuesDeIdentificar"/>, porque con la credencial caída el código ya no abre sesión.
    /// </summary>
    private static string PrepararRechazo(string ruta, string motivo)
    {
        if (motivo == "LOCAL_LIMIT_REACHED")
        {
            return "0001";
        }

        var codigo = "RECHAZO-" + motivo;
        var persona = Guid.NewGuid();
        var snapshot = Guid.Parse("00000000-0000-4000-8000-000000000001");
        using var conexion = AbrirRuta(ruta);
        Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética de rechazo', 1, 1)",
            ("$vc", snapshot), ("$per", persona));
        Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", snapshot), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", codigo));

        // Todos menos SERVICE_NOT_ENABLED tienen una regla de ALMUERZO; la de RULE_DENIED tiene límite 0.
        if (motivo != "SERVICE_NOT_ENABLED")
        {
            Ejecutar(conexion,
                "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, $limite, 1, NULL, NULL)",
                ("$vc", snapshot), ("$reg", Guid.NewGuid()), ("$per", persona),
                ("$srv", Guid.Parse("00000000-0000-4000-8000-000000000005")), ("$amb", Guid.Parse("00000000-0000-4000-8000-000000000006")),
                ("$cat", Guid.Parse("00000000-0000-4000-8000-000000000007")), ("$limite", motivo == "RULE_DENIED" ? 0 : 1));
        }

        if (motivo == "PERSON_INACTIVE")
        {
            Ejecutar(conexion, "UPDATE tbComedorPersonas SET comperActivo = 0 WHERE comperCodigo = $per", ("$per", persona));
        }

        return codigo;
    }

    private void DespuesDeIdentificar(string ruta, string motivo, string codigo)
    {
        var columna = motivo switch
        {
            "CREDENTIAL_REVOKED" => "comcreFechaRevocacionUtc",
            "CREDENTIAL_EXPIRED" => "comcreFechaVigenciaHastaUtc",
            _ => null,
        };
        if (columna is null)
        {
            return;
        }

        using var conexion = AbrirRuta(ruta);
        Ejecutar(conexion, $"UPDATE tbComedorCredencialesPersonas SET {columna} = $ahora WHERE comcreValorNormalizado = $valor",
            ("$ahora", _reloj.Ahora.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")), ("$valor", codigo));
    }

    private void CambiarPrecio(Guid opcion, int version, long importe)
    {
        using var conexion = Abrir();
        Ejecutar(conexion, "UPDATE tbComedorOpcionesMenu SET commenVersionMenu = $ver, commenImporteEmpleadoUnidadesMenores = $imp WHERE commenCodigo = $men",
            ("$ver", version), ("$imp", importe), ("$men", opcion));
    }

    /// <summary>Retiene el bloqueo de escritura: la confirmación espera y sigue «en curso» hasta soltarlo.</summary>
    private BloqueoDeEscritura RetenerEscritura() => new(Abrir());

    private (long Intenciones, long Consumos) IntencionesYConsumos()
    {
        using var conexion = Abrir();
        return (Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorIntencionesConsumo"),
            Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    private SqliteConnection Abrir() => AbrirRuta(_archivo.Ruta);

    private static SqliteConnection AbrirRuta(string ruta)
    {
        var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }

    private string Hash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_archivo.Ruta)));

    private static void Ejecutar(SqliteConnection conexion, string sql, params (string Nombre, object Valor)[] parametros)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        comando.ExecuteNonQuery();
    }

    private static T Escalar<T>(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        var valor = comando.ExecuteScalar();
        return valor is T ya ? ya : (T)Convert.ChangeType(valor!, typeof(T));
    }

    /// <summary>Una transacción BEGIN IMMEDIATE; al soltarla se deshace y se cierra su conexión.</summary>
    private sealed class BloqueoDeEscritura : IDisposable
    {
        private readonly SqliteConnection _conexion;
        private readonly SqliteTransaction _transaccion;

        public BloqueoDeEscritura(SqliteConnection conexion)
        {
            _conexion = conexion;
            _transaccion = conexion.BeginTransaction(deferred: false);
        }

        public void Dispose()
        {
            _transaccion.Dispose();
            _conexion.Dispose();
        }
    }
}
