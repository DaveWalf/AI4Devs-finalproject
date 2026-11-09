using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La oferta por persona en la atención (D3 a D5 de ofrecer-solo-opciones-disponibles), con SQLite real, el catálogo de
/// nueve, un reloj manual y la impresora de prueba como único doble: la máquina de estados y, con bUnit, la pantalla.
/// El código 0001 tiene límite 1 en ALMUERZO y 2 en REFRIGERIO; de las nueve opciones, cuatro son de ALMUERZO y cinco de
/// REFRIGERIO. La fecha de negocio del reloj es 2026-10-30.
/// </summary>
public class OfertaPorPersonaTests : IDisposable
{
    private const string FechaDeNegocio = "2026-10-30";
    private static readonly Guid Snapshot = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid ServicioAlmuerzo = Guid.Parse("00000000-0000-4000-8000-000000000005");
    private static readonly Guid Ambito = Guid.Parse("00000000-0000-4000-8000-000000000006");
    private static readonly Guid CategoriaAlmuerzo = Guid.Parse("00000000-0000-4000-8000-000000000007");
    private static readonly Guid CategoriaRefrigerio = Guid.Parse("00000000-0000-4000-8000-000000000026");
    private static readonly string[] Refrigerios = ["Gaseosa solamente", "Merienda", "Refrigerio", "Refrigerio opcional", "Yogurt"];

    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    private ServicioAtencion NuevoServicio() => new(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);

    private MaquinaAtencion NuevaMaquina()
    {
        var maquina = new MaquinaAtencion(NuevoServicio());
        maquina.Iniciar();
        return maquina;
    }

    private static void Identificar(MaquinaAtencion maquina, string codigo) =>
        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar(codigo));

    private static Guid IdDe(MaquinaAtencion maquina, string nombre) =>
        maquina.PaginaActual.Single(o => o.Nombre == nombre).Id;

    // Sólo las opciones disponibles para la persona

    [Fact(DisplayName = "atencion-puesto · El almuerzo del día ya consumido: sólo las cinco de REFRIGERIO, en una columna, sin paginación, con «Hoy ya usaste: almuerzo 1 de 1»")]
    public async Task Con_el_almuerzo_consumido_se_ofrecen_los_refrigerios()
    {
        var maquina = NuevaMaquina();
        await ConsumirUnAlmuerzo(maquina);

        Identificar(maquina, "0001");

        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        Assert.Equal(Refrigerios, maquina.PaginaActual.Select(o => o.Nombre));
        Assert.Equal(1, maquina.ColumnasPagina);
        Assert.Equal(1, maquina.TotalPaginas);
        Assert.False(maquina.PuedeAvanzar);
        Assert.Equal("Hoy ya usaste: almuerzo 1 de 1", maquina.UsoDelDia);
    }

    [Fact(DisplayName = "atencion-puesto · La pantalla del almuerzo ya consumido: cinco opciones, la línea «Hoy ya usaste:» y ningún control de página")]
    public async Task La_pantalla_muestra_lo_ofrecido_y_la_linea_de_lo_usado()
    {
        var servicio = NuevoServicio();
        var maquina = new MaquinaAtencion(servicio);
        maquina.Iniciar();
        await ConsumirUnAlmuerzo(maquina);
        var pantalla = Dibujar(servicio, maquina);

        await Ingresar(pantalla, "0001");

        Assert.Equal(Refrigerios, pantalla.FindAll(".opcion .nombre").Select(n => n.TextContent.Trim()));
        Assert.Equal("Hoy ya usaste: almuerzo 1 de 1", pantalla.Find("#uso-del-dia").TextContent.Trim());
        Assert.Equal("1", pantalla.Find(".opciones").GetAttribute("data-columnas"));
        Assert.Empty(pantalla.FindAll("#anterior"));
        Assert.Empty(pantalla.FindAll("#siguiente"));
    }

    [Fact(DisplayName = "atencion-puesto · Nada usado en el día: las nueve opciones y ninguna línea «Hoy ya usaste:»")]
    public void Sin_consumos_se_ofrece_el_catalogo_entero()
    {
        var maquina = NuevaMaquina();

        Identificar(maquina, "0001");

        Assert.Equal(9, maquina.PaginaActual.Count);
        Assert.Equal(2, maquina.ColumnasPagina);
        Assert.Null(maquina.UsoDelDia);
    }

    [Fact(DisplayName = "atencion-puesto · Una regla cero no es cupo usado: sólo las de ALMUERZO y sin línea «Hoy ya usaste:»")]
    public void Una_regla_cero_no_es_cupo_usado()
    {
        var maquina = NuevaMaquina();
        var codigo = NuevaPersona(reglas: [(ServicioAlmuerzo, CategoriaAlmuerzo, 1), (Guid.Parse("00000000-0000-4000-8000-000000000025"), CategoriaRefrigerio, 0)]);

        Identificar(maquina, codigo);

        Assert.Equal(4, maquina.PaginaActual.Count);
        Assert.All(maquina.PaginaActual, o => Assert.Contains("Almuerzo", o.Nombre));
        Assert.Null(maquina.UsoDelDia);
    }

    [Fact(DisplayName = "atencion-puesto · La opción se agota entre la oferta y la confirmación: el mensaje de LOCAL_LIMIT_REACHED y ningún consumo nuevo")]
    public async Task La_opcion_se_agota_antes_de_confirmar()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        maquina.Elegir(IdDe(maquina, "Almuerzo con gaseosa"));
        GastarCupo("0001", CategoriaAlmuerzo, 1);

        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Resultado, maquina.Estado);
        Assert.Equal(MensajesAtencion.DeMotivo("LOCAL_LIMIT_REACHED"), maquina.Mensaje);
        Assert.False(maquina.ConsumoRegistrado);
        Assert.Equal(0L, Contar("tbComedorConsumos"));
    }

    [Fact(DisplayName = "atencion-puesto · Elegir una opción que no se ofrece no hace nada")]
    public async Task Elegir_una_opcion_no_ofrecida_no_hace_nada()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        var almuerzo = IdDe(maquina, "Almuerzo con gaseosa");
        maquina.Cancelar();
        await ConsumirUnAlmuerzo(maquina);
        Identificar(maquina, "0001");

        maquina.Elegir(almuerzo);

        Assert.Null(maquina.Elegida);
        maquina.Elegir(IdDe(maquina, "Merienda"));
        Assert.Equal("Merienda", maquina.Elegida?.Nombre);
    }

    [Fact(DisplayName = "atencion-puesto · La opción cambió y se agotó: la oferta se vuelve a calcular, la elegida se retira y queda el aviso de lo usado")]
    public async Task El_catalogo_cambiado_recalcula_la_oferta()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        var elegida = IdDe(maquina, "Almuerzo con gaseosa");
        maquina.Elegir(elegida);
        CambiarVersion(elegida, 2);
        GastarCupo("0001", CategoriaAlmuerzo, 1);

        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        Assert.Null(maquina.Elegida);
        Assert.Equal(MensajesAtencion.OpcionRetirada, maquina.Aviso);
        Assert.Equal(Refrigerios, maquina.PaginaActual.Select(o => o.Nombre));
        Assert.Equal("Hoy ya usaste: almuerzo 1 de 1", maquina.UsoDelDia);
        Assert.Equal(0L, Contar("tbComedorConsumos"));
    }

    // Aviso claro cuando no queda ninguna opción

    [Fact(DisplayName = "atencion-puesto · Todo el cupo del día usado: sin opciones, «Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2» y «Listo» vuelve a reposo sin intención nueva")]
    public void Con_todo_el_cupo_usado_no_hay_opciones()
    {
        var maquina = NuevaMaquina();
        GastarCupo("0001", CategoriaAlmuerzo, 1);
        GastarCupo("0001", CategoriaRefrigerio, 2);

        Identificar(maquina, "0001");

        Assert.Equal(EstadoAtencion.SinOpciones, maquina.Estado);
        Assert.Empty(maquina.PaginaActual);
        Assert.Equal("Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2", maquina.UsoDelDia);
        Assert.Null(maquina.MotivoSinOpciones);
        maquina.Elegir(Guid.NewGuid());
        Assert.Null(maquina.Elegida);

        maquina.Listo();

        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Null(maquina.NombreVisible);
        Assert.Equal(0L, Contar("tbComedorIntencionesConsumo"));
    }

    [Fact(DisplayName = "atencion-puesto · La pantalla sin opciones: el aviso, la línea de lo usado y «Listo», sin opciones ni «Confirmar»")]
    public async Task La_pantalla_sin_opciones()
    {
        var servicio = NuevoServicio();
        var maquina = new MaquinaAtencion(servicio);
        maquina.Iniciar();
        GastarCupo("0001", CategoriaAlmuerzo, 1);
        GastarCupo("0001", CategoriaRefrigerio, 2);
        var pantalla = Dibujar(servicio, maquina);

        await Ingresar(pantalla, "0001");

        Assert.Equal(MensajesAtencion.SinOpciones, pantalla.Find("#mensaje").TextContent.Trim());
        Assert.Equal("Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2", pantalla.Find("#uso-del-dia").TextContent.Trim());
        Assert.Empty(pantalla.FindAll(".opcion"));
        Assert.Empty(pantalla.FindAll("#confirmar"));
        Assert.Equal("Listo", pantalla.Find("#listo").TextContent.Trim());

        await pantalla.Find("#listo").ClickAsync(new());

        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.DoesNotContain("Persona sintética 01", pantalla.Markup);
    }

    [Fact(DisplayName = "atencion-puesto · Persona sin ninguna habilitación: «No tenés servicios habilitados. Consultá al responsable del comedor», sin «Hoy ya usaste:»")]
    public async Task Sin_ninguna_habilitacion()
    {
        var servicio = NuevoServicio();
        var maquina = new MaquinaAtencion(servicio);
        maquina.Iniciar();
        var codigo = NuevaPersona(reglas: []);
        var pantalla = Dibujar(servicio, maquina);

        await Ingresar(pantalla, codigo);

        Assert.Equal(EstadoAtencion.SinOpciones, maquina.Estado);
        Assert.Null(maquina.UsoDelDia);
        Assert.Equal(MensajesAtencion.SinHabilitacion, maquina.MotivoSinOpciones);
        Assert.Equal(MensajesAtencion.SinOpciones, pantalla.Find("#mensaje").TextContent.Trim());
        Assert.Equal(MensajesAtencion.SinHabilitacion, pantalla.Find("#motivo-sin-opciones").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#uso-del-dia"));
    }

    [Fact(DisplayName = "atencion-puesto · Persona inactiva: «Tu habilitación no está activa. Consultá al responsable»")]
    public void Persona_inactiva()
    {
        var maquina = NuevaMaquina();
        var codigo = NuevaPersona(reglas: [(ServicioAlmuerzo, CategoriaAlmuerzo, 1)], activa: false);

        Identificar(maquina, codigo);

        Assert.Equal(EstadoAtencion.SinOpciones, maquina.Estado);
        Assert.Equal(MensajesAtencion.DeMotivo("PERSON_INACTIVE"), maquina.MotivoSinOpciones);
        Assert.Null(maquina.UsoDelDia);
    }

    [Fact(DisplayName = "atencion-puesto · Diez segundos sin tocar la pantalla sin opciones: vuelve a reposo sin el nombre ni el detalle de esa persona")]
    public void Diez_segundos_vuelven_a_reposo()
    {
        var maquina = NuevaMaquina();
        GastarCupo("0001", CategoriaAlmuerzo, 1);
        GastarCupo("0001", CategoriaRefrigerio, 2);
        Identificar(maquina, "0001");

        _reloj.Avanzar(TimeSpan.FromSeconds(9));
        Assert.False(maquina.RevisarInactividad());
        Assert.Equal(EstadoAtencion.SinOpciones, maquina.Estado);

        _reloj.Avanzar(TimeSpan.FromSeconds(1));
        Assert.True(maquina.RevisarInactividad());

        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Null(maquina.NombreVisible);
        Assert.Null(maquina.UsoDelDia);
    }

    // Ayudantes

    private IRenderedComponent<PantallaAtencion> Dibujar(ServicioAtencion servicio, MaquinaAtencion maquina)
    {
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(maquina);
        return _contexto.Render<PantallaAtencion>();
    }

    private static async Task Ingresar(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#ingresar").ClickAsync(new());
    }

    /// <summary>Un almuerzo confirmado de verdad: la persona vuelve a reposo con el cupo de ALMUERZO usado.</summary>
    private static async Task ConsumirUnAlmuerzo(MaquinaAtencion maquina)
    {
        Identificar(maquina, "0001");
        maquina.Elegir(IdDe(maquina, "Almuerzo con gaseosa"));
        await maquina.ConfirmarAsync();
        Assert.True(maquina.ConsumoRegistrado);
        maquina.Listo();
    }

    /// <summary>Una persona sintética con un código propio, activa o no, y las reglas dadas como (servicio, categoría, límite).</summary>
    private string NuevaPersona((Guid Servicio, Guid Categoria, int Limite)[] reglas, bool activa = true)
    {
        var codigo = "OFERTA-" + Guid.NewGuid().ToString("N")[..8];
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética de oferta', $activa, 1)",
            ("$vc", Snapshot), ("$per", persona), ("$activa", activa ? 1 : 0));
        Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", Snapshot), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", codigo));
        foreach (var (servicio, categoria, limite) in reglas)
        {
            Ejecutar(conexion, "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, $limite, 1, NULL, NULL)",
                ("$vc", Snapshot), ("$reg", Guid.NewGuid()), ("$per", persona), ("$srv", servicio), ("$amb", Ambito), ("$cat", categoria), ("$limite", limite));
        }

        return codigo;
    }

    /// <summary>Deja usadas unidades de la categoría de la persona del código, en la fecha de negocio del reloj.</summary>
    private void GastarCupo(string codigo, Guid categoria, int unidades)
    {
        using var conexion = Abrir();
        Ejecutar(conexion,
            """
            INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion)
            VALUES ((SELECT comperCodigo FROM tbComedorCredencialesPersonas WHERE comcreValorNormalizado = $valor), $amb, $cat, $fecha, $usado, 1)
            """,
            ("$valor", codigo), ("$amb", Ambito), ("$cat", categoria), ("$fecha", FechaDeNegocio), ("$usado", unidades));
    }

    private void CambiarVersion(Guid opcion, int version)
    {
        using var conexion = Abrir();
        Ejecutar(conexion, "UPDATE tbComedorOpcionesMenu SET commenVersionMenu = $ver WHERE commenCodigo = $men", ("$ver", version), ("$men", opcion));
    }

    private long Contar(string tabla)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }

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
}
