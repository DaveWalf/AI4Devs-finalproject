using System.Text.Json;
using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La administración local sin pantalla (administracion-local de administrar-personas-y-opciones), llamando al servicio sobre
/// una carpeta de datos temporal real con la base adentro, el fixture sintético (versión de configuración 1, el código
/// 000123 de la persona habilitada) y el reloj fijo el 06/11/2026 a las 12:00 de Buenos Aires.
/// </summary>
public class AdministracionLocalTests : IDisposable
{
    private const string CodigoNuevo = "0105";

    private static readonly string[] TablasDeConfiguracion =
    [
        "tbComedorPersonas", "tbComedorCredencialesPersonas", "tbComedorTiposServicio", "tbComedorAmbitosCupo",
        "tbComedorCategoriasCupo", "tbComedorOpcionesMenu", "tbComedorOpcionesMenu_CategoriasCupo",
        "tbComedorReglasHabilitacion", "tbComedorPermisosOperadores", "tbComedorFranjasOpcionesMenu",
    ];

    private readonly ImpresoraDePrueba _impresora = new();

    public AdministracionLocalTests()
    {
        Directory.CreateDirectory(Carpeta);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "administracion-" + Guid.NewGuid().ToString("N"));

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    private RelojFijo Reloj { get; } = new("2026-11-06T15:00:00Z");

    private AdministracionLocal Admin => new(Base, Reloj);

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, recursive: true);
        }
    }

    // ── Cada guardado es una versión local nueva ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Guardar un cambio de cupo crea la versión 2 activa con el cambio, y la versión 1 sigue con todas sus filas como estaban")]
    public void Guardar_crea_la_version_2_y_conserva_la_1()
    {
        var antes = FotoDeVersion(Fixture.SnapshotId);

        var resultado = Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 2)]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        Assert.Equal(2, resultado.VersionActiva);
        Assert.Equal(2, VersionActiva());
        Assert.Equal(2, Cupo(Admin.Leer(), Fixture.Habilitada.Id, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo)!.Limite);
        Assert.Equal(antes, FotoDeVersion(Fixture.SnapshotId));
        Assert.Equal(2L, Contar("tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "administracion-local · Un consumo de la versión 1 con 1.500,00 conserva su versión, su opción de esa versión y su importe después de llevar la opción a 1.800,00")]
    public void Un_consumo_anterior_conserva_su_version()
    {
        var consumo = Atender(FixtureSintetico.CodigoSintetico, Fixture.AlmuerzoComun.Id).ConsumptionId!.Value;

        var resultado = Admin.GuardarOpciones([ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), 180000)]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        using var conexion = Base.AbrirConexion();
        Assert.Equal(Fixture.SnapshotId, Sql.EscalarGuid(conexion, "SELECT comvcCodigo FROM tbComedorConsumos WHERE comconCodigo = $c", ("$c", consumo)));
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT comconVersionMenu FROM tbComedorConsumos WHERE comconCodigo = $c", ("$c", consumo)));
        var precio = JsonDocument.Parse(Sql.Escalar<string>(conexion, "SELECT comconPrecioAplicado FROM tbComedorConsumos WHERE comconCodigo = $c", ("$c", consumo))).RootElement;
        Assert.Equal(150000, precio.GetProperty("EmployeeChargeMinor").GetInt64());
        Assert.Equal(150000L, Sql.Escalar<long>(conexion,
            "SELECT commenImporteEmpleadoUnidadesMenores FROM tbComedorOpcionesMenu WHERE comvcCodigo = $vc AND commenCodigo = $men",
            ("$vc", Fixture.SnapshotId), ("$men", Fixture.AlmuerzoComun.Id)));
    }

    [Fact(DisplayName = "administracion-local · Un guardado inválido no cambia nada: una persona nueva sin nombre se rechaza con su campo, la versión activa sigue siendo la 1 y no hay versión 2")]
    public void Un_guardado_invalido_no_cambia_nada()
    {
        var resultado = Admin.GuardarPersonas([new EdicionPersona(null, "", [], CodigoNuevo)]);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Equal(CodigosAdministracion.CampoInvalido, resultado.Motivo);
        Assert.Contains(resultado.Campos, c => c is { Indice: 0, Campo: "Nombre", Codigo: CodigosAdministracion.Requerido });
        Assert.Equal(1, resultado.VersionActiva);
        Assert.Equal(1, VersionActiva());
        Assert.Equal(1L, Contar("tbComedorVersionesConfiguracion"));
    }

    // ── Con INWeb como dueño la edición se bloquea ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Versión activa de INWeb: la vista lista las personas y es de sólo lectura")]
    public void Version_activa_de_INWeb_es_de_solo_lectura()
    {
        MarcarOrigen(Fixture.SnapshotId, "INWEB");

        var vista = Admin.Leer();

        Assert.Equal(OrigenConfiguracion.INWeb, vista.Origen);
        Assert.True(vista.SoloLectura);
        Assert.Equal(3, vista.Personas.Count);
    }

    [Fact(DisplayName = "administracion-local · Guardado pedido con INWeb activo: se deniega, no hay versión nueva y la activa sigue siendo la de INWeb")]
    public void Guardado_con_INWeb_activo_se_deniega()
    {
        MarcarOrigen(Fixture.SnapshotId, "INWEB");

        var resultado = Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 2)]);
        var opciones = Admin.GuardarOpciones([ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), 180000)]);

        Assert.Equal(TipoGuardado.Denegado, resultado.Tipo);
        Assert.Equal(CodigosAdministracion.INWebAdministra, resultado.Motivo);
        Assert.Equal(TipoGuardado.Denegado, opciones.Tipo);
        Assert.Equal(1L, Contar("tbComedorVersionesConfiguracion"));
        Assert.Equal(Fixture.SnapshotId, SnapshotActivo());
    }

    [Fact(DisplayName = "administracion-local · Versión local: la del fixture no tiene origen registrado, el guardado se aplica y la versión nueva tiene origen LOCAL")]
    public void Version_local_se_edita_y_la_nueva_es_local()
    {
        Assert.Equal(OrigenConfiguracion.Local, Admin.Leer().Origen);

        var resultado = Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 2)]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        using var conexion = Base.AbrirConexion();
        Assert.Equal("LOCAL", Sql.Escalar<string>(conexion, "SELECT comvoOrigen FROM tbComedorOrigenesVersiones WHERE comvcCodigo = $vc", ("$vc", SnapshotActivo())));
        Assert.Equal(OrigenConfiguracion.Local, Admin.Leer().Origen);
    }

    // ── Agregar una persona ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Alta y primera atención: «Persona sintética 05» con 0105 y ALMUERZO límite 1; al tipear 0105 se abre su sesión y se le ofrecen sólo opciones de ALMUERZO")]
    public void Alta_y_primera_atencion()
    {
        var resultado = Admin.GuardarPersonas([Alta("Persona sintética 05", CodigoNuevo, (Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 1))]);
        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);

        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoNuevo, PropositoSesion.Consumption);

        Assert.True(sesion.Abierta);
        Assert.Equal("Persona sintética 05", sesion.NombreVisible);
        var oferta = new ConsultaDisponibilidad(Base, Reloj).Ofrecer(sesion.IdentitySessionId!.Value, sesion.PersonId!.Value);
        var deAlmuerzo = Fixture.Opciones.Take(8).Select(o => o.Id).ToHashSet();
        Assert.NotEmpty(oferta.Ofrecidas);
        Assert.All(oferta.Ofrecidas, o => Assert.Contains(o.Id, deAlmuerzo));
    }

    [Fact(DisplayName = "administracion-local · Código ya usado, aunque esté revocado: dar de alta otra persona con el código de una baja se rechaza con DUPLICATE_CODE, sin decir de quién es, y sin versión nueva")]
    public void Codigo_ya_usado_aunque_este_revocado()
    {
        Assert.Equal(TipoGuardado.Aplicado, Admin.GuardarPersonas([Edicion(Fixture.Habilitada.Id) with { Quitar = true }]).Tipo);

        var resultado = Admin.GuardarPersonas([Alta("Otra persona", FixtureSintetico.CodigoSintetico)]);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Equal(CodigosAdministracion.CodigoDuplicado, resultado.Motivo);
        var campo = Assert.Single(resultado.Campos);
        Assert.Equal(("Codigo", CodigosAdministracion.CodigoDuplicado), (campo.Campo, campo.Codigo));
        Assert.Equal(2, VersionActiva());
        Assert.Equal(2L, Contar("tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "administracion-local · Datos fuera de forma: el código 12a4, un nombre de 81 caracteres y un nombre con un salto de línea se rechazan con su campo, sin versión nueva")]
    public void Datos_fuera_de_forma()
    {
        var resultado = Admin.GuardarPersonas(
        [
            Alta("Persona con código raro", "12a4"),
            Alta(new string('a', 81), "0106"),
            Alta("Persona\ncon salto", "0107"),
        ]);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Contains(resultado.Campos, c => c is { Indice: 0, Campo: "Codigo", Codigo: CodigosAdministracion.NoSonDigitos });
        Assert.Contains(resultado.Campos, c => c is { Indice: 1, Campo: "Nombre", Codigo: CodigosAdministracion.DemasiadoLargo });
        Assert.Contains(resultado.Campos, c => c is { Indice: 2, Campo: "Nombre", Codigo: CodigosAdministracion.CaracterDeControl });
        Assert.Equal(1L, Contar("tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "administracion-local · Ceros iniciales: con una persona agregada con 0105, 105 no se resuelve y 0105 sí")]
    public void Ceros_iniciales()
    {
        Admin.GuardarPersonas([Alta("Persona sintética 05", CodigoNuevo, (Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 1))]);
        var identificacion = new IdentificacionPorCodigo(Base, Reloj);

        var sinCeros = identificacion.Identificar("105", PropositoSesion.Consumption);
        var conCeros = identificacion.Identificar(CodigoNuevo, PropositoSesion.Consumption);

        Assert.False(sinCeros.Abierta);
        Assert.Equal("IDENTITY_NOT_RESOLVED", sinCeros.ReasonCode);
        Assert.True(conCeros.Abierta);
    }

    // ── Quitar una persona es darla de baja ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Baja de una persona con consumos: su código da CREDENTIAL_REVOKED y no abre sesión, su consumo sigue registrado y la vista la muestra de baja")]
    public void Baja_de_una_persona_con_consumos()
    {
        var consumo = Atender(FixtureSintetico.CodigoSintetico, Fixture.AlmuerzoComun.Id).ConsumptionId!.Value;

        var resultado = Admin.GuardarPersonas([Edicion(Fixture.Habilitada.Id) with { Quitar = true }]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var identificacion = new IdentificacionPorCodigo(Base, Reloj).Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);
        Assert.False(identificacion.Abierta);
        Assert.Equal("CREDENTIAL_REVOKED", identificacion.ReasonCode);
        using var conexion = Base.AbrirConexion();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos WHERE comconCodigo = $c", ("$c", consumo)));
        var persona = Assert.Single(Admin.Leer().Personas, p => p.Id == Fixture.Habilitada.Id);
        Assert.False(persona.Activa);
        Assert.Equal(Reloj.Ahora, FormatoInstante.Leer(Sql.Escalar<string>(conexion,
            "SELECT comcreFechaRevocacionUtc FROM tbComedorCredencialesPersonas WHERE comvcCodigo = $vc AND comperCodigo = $per",
            ("$vc", SnapshotActivo()), ("$per", Fixture.Habilitada.Id))));
    }

    [Fact(DisplayName = "administracion-local · Baja con una sesión abierta en la versión 1 y una opción elegida: al confirmar después de activar la baja, DENIED con PERSON_INACTIVE y ningún consumo")]
    public void Baja_con_una_sesion_abierta()
    {
        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);
        Admin.GuardarPersonas([Edicion(Fixture.Habilitada.Id) with { Quitar = true }]);

        var resultado = new UnidadConfirmacion(Base, Reloj).Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, Fixture.AlmuerzoComun.Id, 1, 1));

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        Assert.Equal("PERSON_INACTIVE", resultado.ReasonCode);
        Assert.Equal(0L, Contar("tbComedorConsumos"));
    }

    // ── Modificar los cupos y el nombre de una persona ───────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Subir el límite con un almuerzo ya usado: llevar ALMUERZO a 2 vuelve a ofrecer el almuerzo y no muestra «Hoy ya usaste:» para ALMUERZO")]
    public void Subir_el_limite_con_un_almuerzo_ya_usado()
    {
        Atender(FixtureSintetico.CodigoSintetico, Fixture.AlmuerzoComun.Id);
        Assert.Contains(Ofrecer(FixtureSintetico.CodigoSintetico).Agotadas, a => a.Id == Fixture.CategoriaAlmuerzo);

        Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 2)]);

        var oferta = Ofrecer(FixtureSintetico.CodigoSintetico);
        Assert.Contains(oferta.Ofrecidas, o => o.Id == Fixture.AlmuerzoComun.Id);
        Assert.DoesNotContain(oferta.Agotadas, a => a.Id == Fixture.CategoriaAlmuerzo);
    }

    [Fact(DisplayName = "administracion-local · Límite cero: llevar REFRIGERIO a 0 retiene sus opciones con RULE_DENIED y no muestra «Hoy ya usaste:» para REFRIGERIO")]
    public void Limite_cero()
    {
        Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioRefrigerio, Fixture.CategoriaRefrigerio, 0)]);

        var oferta = Ofrecer(FixtureSintetico.CodigoSintetico);

        var refrigerios = Fixture.Opciones.Skip(9).Select(o => o.Id).ToList();
        Assert.All(refrigerios, id => Assert.Equal("RULE_DENIED", Assert.Single(oferta.Retenidas, r => r.Id == id).Motivo));
        Assert.DoesNotContain(oferta.Agotadas, a => a.Id == Fixture.CategoriaRefrigerio);
        var reglas = ReglasDelPar(Fixture.Habilitada.Id, Fixture.ServicioRefrigerio, Fixture.CategoriaRefrigerio);
        Assert.Equal((0L, 1L, true), Assert.Single(reglas));
    }

    [Fact(DisplayName = "administracion-local · Bajar el límite por debajo de lo usado: con dos refrigerios consumidos y REFRIGERIO en 1, los dos consumos siguen y la oferta dice refrigerio 2 de 1")]
    public void Bajar_el_limite_por_debajo_de_lo_usado()
    {
        Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioRefrigerio, Fixture.CategoriaRefrigerio, 2)]);
        Atender(FixtureSintetico.CodigoSintetico, Fixture.Refrigerio.Id);
        Atender(FixtureSintetico.CodigoSintetico, Fixture.Refrigerio.Id);

        Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioRefrigerio, Fixture.CategoriaRefrigerio, 1)]);

        Assert.Equal(2L, Contar("tbComedorConsumos"));
        var agotada = Assert.Single(Ofrecer(FixtureSintetico.CodigoSintetico).Agotadas, a => a.Id == Fixture.CategoriaRefrigerio);
        Assert.Equal((2, 1), (agotada.Usado, agotada.Limite));
    }

    [Fact(DisplayName = "administracion-local · Límite fuera de rango: -1 y 100 se rechazan con el campo señalado y sin versión nueva")]
    public void Limite_fuera_de_rango()
    {
        foreach (var limite in new[] { -1, 100 })
        {
            var resultado = Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite)]);

            Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
            Assert.Contains(resultado.Campos, c => c.Campo.StartsWith("Cupos[", StringComparison.Ordinal) && c.Campo.EndsWith("].Limite", StringComparison.Ordinal)
                && c.Codigo == CodigosAdministracion.FueraDeRango);
        }

        Assert.Equal(1L, Contar("tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "administracion-local · Un par con reglas de vigencia o prioridad se ve con el límite de la que decide y el aviso; editarlo deja una sola regla de prioridad 1, sin vigencia y con el ámbito del puesto")]
    public void Par_con_varias_reglas_se_reemplaza_por_una()
    {
        using (var conexion = Base.AbrirConexion())
        {
            Sql.Ejecutar(conexion,
                "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, 3, 5, '2026-01-01T03:00:00.000Z', NULL)",
                ("$vc", Fixture.SnapshotId), ("$reg", Guid.NewGuid()), ("$per", Fixture.Habilitada.Id),
                ("$srv", Fixture.ServicioAlmuerzo), ("$amb", Fixture.Ambito), ("$cat", Fixture.CategoriaAlmuerzo));
        }

        var cupo = Cupo(Admin.Leer(), Fixture.Habilitada.Id, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo)!;
        Assert.Equal(3, cupo.Limite);
        Assert.True(cupo.ReglasQueSeReemplazan);

        Admin.GuardarPersonas([ConCupo(Edicion(Fixture.Habilitada.Id), Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 2)]);

        Assert.Equal((2L, 1L, true), Assert.Single(ReglasDelPar(Fixture.Habilitada.Id, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo)));
        Assert.False(Cupo(Admin.Leer(), Fixture.Habilitada.Id, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo)!.ReglasQueSeReemplazan);
    }

    [Fact(DisplayName = "administracion-local · Quitar un cupo deja el par sin regla, y cambiar el nombre visible queda en la versión nueva")]
    public void Quitar_un_cupo_y_cambiar_el_nombre()
    {
        var edicion = Edicion(Fixture.Habilitada.Id);
        var sinRefrigerio = edicion with
        {
            Nombre = "Persona renombrada",
            Cupos = edicion.Cupos.Where(c => !(c.ServicioId == Fixture.ServicioRefrigerio && c.CategoriaId == Fixture.CategoriaRefrigerio)).ToList(),
        };

        Assert.Equal(TipoGuardado.Aplicado, Admin.GuardarPersonas([sinRefrigerio]).Tipo);

        Assert.Empty(ReglasDelPar(Fixture.Habilitada.Id, Fixture.ServicioRefrigerio, Fixture.CategoriaRefrigerio));
        var persona = Assert.Single(Admin.Leer().Personas, p => p.Id == Fixture.Habilitada.Id);
        Assert.Equal("Persona renombrada", persona.Nombre);
        Assert.Equal(2, persona.Cupos.Count);
    }

    // ── Opciones del comedor con todos sus parámetros ────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Cambio de precio con la opción elegida: «Almuerzo común» pasa a 1.800,00 con la versión de menú 2, y confirmar con la versión 1 devuelve CatalogoCambiado")]
    public void Cambio_de_precio_con_la_opcion_elegida()
    {
        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);

        Admin.GuardarOpciones([ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), 180000)]);
        var resultado = new UnidadConfirmacion(Base, Reloj).Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, Fixture.AlmuerzoComun.Id, 1, 1));

        Assert.Equal(TipoResultado.CatalogoCambiado, resultado.Tipo);
        var opcion = Assert.Single(new ConsultaCatalogo(Base).Leer(), o => o.Id == Fixture.AlmuerzoComun.Id);
        Assert.Equal((2, 180000L), (opcion.Version, opcion.ImporteUnidadesMenores));
        Assert.Equal(0L, Contar("tbComedorConsumos"));
    }

    [Fact(DisplayName = "administracion-local · Agregar «Fruta» de REFRIGERIO, sin cargo y con una carga de REFRIGERIO: la oferta tiene una opción más, «Fruta», sin importe")]
    public void Agregar_una_opcion()
    {
        var antes = Ofrecer(FixtureSintetico.CodigoSintetico).Ofrecidas.Count;

        var resultado = Admin.GuardarOpciones([new EdicionOpcion(null, "Fruta", Fixture.ServicioRefrigerio, 0, [new CargaEditada(Fixture.CategoriaRefrigerio, 1)], [])]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var oferta = Ofrecer(FixtureSintetico.CodigoSintetico);
        Assert.Equal(antes + 1, oferta.Ofrecidas.Count);
        var fruta = Assert.Single(oferta.Ofrecidas, o => o.Nombre == "Fruta");
        Assert.Equal((0L, 1), (fruta.ImporteUnidadesMenores, fruta.Version));
        Assert.Equal(16, new ConsultaCatalogo(Base).Leer().Count);
    }

    [Fact(DisplayName = "administracion-local · Quitar una opción con consumos: no se ofrece a nadie y el consumo sigue registrado con su opción y su importe")]
    public void Quitar_una_opcion_con_consumos()
    {
        var consumo = Atender(FixtureSintetico.CodigoSintetico, Fixture.Refrigerio.Id).ConsumptionId!.Value;

        var resultado = Admin.GuardarOpciones([EdicionOpcion(Fixture.Refrigerio.Id) with { Quitar = true }]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        Assert.DoesNotContain(new ConsultaCatalogo(Base).Leer(), o => o.Id == Fixture.Refrigerio.Id);
        Assert.DoesNotContain(Admin.Leer().Opciones, o => o.Id == Fixture.Refrigerio.Id);
        using var conexion = Base.AbrirConexion();
        Assert.Equal(Fixture.Refrigerio.Id, Sql.EscalarGuid(conexion, "SELECT commenCodigo FROM tbComedorConsumos WHERE comconCodigo = $c", ("$c", consumo)));
        Assert.Equal(150000L, Sql.Escalar<long>(conexion,
            """
            SELECT men.commenImporteEmpleadoUnidadesMenores FROM tbComedorConsumos con
            JOIN tbComedorOpcionesMenu men ON men.comvcCodigo = con.comvcCodigo AND men.commenCodigo = con.commenCodigo
            WHERE con.comconCodigo = $c
            """, ("$c", consumo)));
    }

    [Fact(DisplayName = "administracion-local · Opción inválida: importe -1, sin nombre y sin cargas se rechazan cada una con su campo, sin versión nueva")]
    public void Opcion_invalida()
    {
        var resultado = Admin.GuardarOpciones(
        [
            ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), -1),
            EdicionOpcion(Fixture.AlmuerzoOpcional.Id) with { Nombre = "" },
            new EdicionOpcion(null, "Sin cargas", Fixture.ServicioAlmuerzo, 1000, [], []),
        ]);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Contains(resultado.Campos, c => c is { Indice: 0, Campo: "Importe", Codigo: CodigosAdministracion.FueraDeRango });
        Assert.Contains(resultado.Campos, c => c is { Indice: 1, Campo: "Nombre", Codigo: CodigosAdministracion.Requerido });
        Assert.Contains(resultado.Campos, c => c is { Indice: 2, Campo: "Cargas", Codigo: CodigosAdministracion.Requerido });
        Assert.Equal(1L, Contar("tbComedorVersionesConfiguracion"));
    }

    // ── Franjas horarias de una opción ───────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Cargar la franja del almuerzo: «Almuerzo común» figura con 11:00 a 14:00 y conserva su versión de menú")]
    public void Cargar_la_franja_del_almuerzo()
    {
        var resultado = Admin.GuardarOpciones([EdicionOpcion(Fixture.AlmuerzoComun.Id) with { Franjas = [new FranjaEditada("11:00", "14:00")] }]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var opcion = Assert.Single(Admin.Leer().Opciones, o => o.Id == Fixture.AlmuerzoComun.Id);
        Assert.Equal(new FranjaHoraria(new TimeOnly(11, 0), new TimeOnly(14, 0)), Assert.Single(opcion.Franjas));
        Assert.Equal(1, opcion.Version);
    }

    [Fact(DisplayName = "administracion-local · Franja que cruza la medianoche, de 22:00 a 02:00: a la 01:30 la opción se ofrece y a las 02:00 se retiene")]
    public void Franja_que_cruza_la_medianoche()
    {
        Admin.GuardarOpciones([EdicionOpcion(Fixture.AlmuerzoComun.Id) with { Franjas = [new FranjaEditada("22:00", "02:00")] }]);

        Reloj.Ahora = DateTimeOffset.Parse("2026-11-07T01:30:00-03:00");
        Assert.Contains(Ofrecer(FixtureSintetico.CodigoSintetico).Ofrecidas, o => o.Id == Fixture.AlmuerzoComun.Id);
        Reloj.Ahora = DateTimeOffset.Parse("2026-11-07T02:00:00-03:00");
        var retenida = Assert.Single(Ofrecer(FixtureSintetico.CodigoSintetico).Retenidas, r => r.Id == Fixture.AlmuerzoComun.Id);
        Assert.Equal("OUTSIDE_SERVICE_HOURS", retenida.Causa);
    }

    [Fact(DisplayName = "administracion-local · Franja vacía: una franja de 12:00 a 12:00 se rechaza con la franja señalada y sin versión nueva")]
    public void Franja_vacia()
    {
        var resultado = Admin.GuardarOpciones([EdicionOpcion(Fixture.AlmuerzoComun.Id) with { Franjas = [new FranjaEditada("12:00", "12:00")] }]);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Contains(resultado.Campos, c => c is { Indice: 0, Campo: "Franjas[0]", Codigo: CodigosAdministracion.FranjaVacia });
        Assert.Equal(1L, Contar("tbComedorVersionesConfiguracion"));
    }

    // ── Cada guardado queda en la bitácora general ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "administracion-local · Alta de una persona: la general tiene CONFIGURATION_CHANGED de persona con el antes nulo, el después con nombre, estado y cupos, la credencial nueva, APPLIED y las versiones 1 y 2; ninguna línea de las dos bitácoras contiene 0105")]
    public void Bitacora_del_alta()
    {
        Admin.GuardarPersonas([Alta("Persona sintética 05", CodigoNuevo, (Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, 1))]);
        new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoNuevo, PropositoSesion.Consumption);

        new EscritorBitacoras(Base, Carpeta, Reloj).Entregar();

        var general = Lineas(DestinoBitacora.General);
        var contexto = Assert.Single(general, l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("PERSON", contexto.GetProperty("Setting").GetString());
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("Before").ValueKind);
        var despues = contexto.GetProperty("After");
        Assert.Equal("Persona sintética 05", despues.GetProperty("Name").GetString());
        Assert.True(despues.GetProperty("Active").GetBoolean());
        var cupo = Assert.Single(despues.GetProperty("Quotas").EnumerateArray());
        Assert.Equal(1, cupo.GetProperty("Limit").GetInt32());
        Assert.True(contexto.GetProperty("CredentialChanged").GetBoolean());
        Assert.Equal(JsonValueKind.String, contexto.GetProperty("CredentialId").ValueKind);
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
        Assert.Equal(1, contexto.GetProperty("ConfigurationVersionBefore").GetInt32());
        Assert.Equal(2, contexto.GetProperty("ConfigurationVersionAfter").GetInt32());
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("ActorPersonId").ValueKind);
        Assert.Equal("NO_AUTHENTICATION", contexto.GetProperty("ActorReason").GetString());
        Assert.Contains(Lineas(DestinoBitacora.Interacciones), l => Tipo(l) == "SESSION_OPENED");
        AfirmarSinElCodigo(CodigoNuevo);
    }

    [Fact(DisplayName = "administracion-local · Cambio de importe: la general tiene CONFIGURATION_CHANGED de opción con el importe antes 150000 y después 180000 en unidades menores, y la versión de menú 1 y 2")]
    public void Bitacora_del_cambio_de_importe()
    {
        Admin.GuardarOpciones([ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), 180000)]);

        new EscritorBitacoras(Base, Carpeta, Reloj).Entregar();

        var contexto = Assert.Single(Lineas(DestinoBitacora.General), l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("MENU_OPTION", contexto.GetProperty("Setting").GetString());
        Assert.Equal(Fixture.AlmuerzoComun.Id, contexto.GetProperty("MenuItemId").GetGuid());
        Assert.Equal(150000, contexto.GetProperty("Before").GetProperty("EmployeeChargeMinor").GetInt64());
        Assert.Equal(180000, contexto.GetProperty("After").GetProperty("EmployeeChargeMinor").GetInt64());
        Assert.Equal(1, contexto.GetProperty("Before").GetProperty("MenuVersion").GetInt32());
        Assert.Equal(2, contexto.GetProperty("After").GetProperty("MenuVersion").GetInt32());
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
    }

    [Fact(DisplayName = "administracion-local · Guardado rechazado: la general tiene CONFIGURATION_CHANGED con REJECTED, DUPLICATE_CODE y la versión activa sin cambio, sin el código")]
    public void Bitacora_del_guardado_rechazado()
    {
        Admin.GuardarPersonas([Alta("Persona repetida", FixtureSintetico.CodigoSintetico)]);

        new EscritorBitacoras(Base, Carpeta, Reloj).Entregar();

        var contexto = Assert.Single(Lineas(DestinoBitacora.General), l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("PERSON", contexto.GetProperty("Setting").GetString());
        Assert.Equal("REJECTED", contexto.GetProperty("Result").GetString());
        Assert.Equal("DUPLICATE_CODE", contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal(1, contexto.GetProperty("ConfigurationVersionBefore").GetInt32());
        Assert.Equal(1, contexto.GetProperty("ConfigurationVersionAfter").GetInt32());
        Assert.Equal("Codigo", Assert.Single(contexto.GetProperty("Fields").EnumerateArray()).GetProperty("Field").GetString());
        AfirmarSinElCodigo(FixtureSintetico.CodigoSintetico);
    }

    [Fact(DisplayName = "administracion-local · Guardado denegado: con INWeb activo, la general tiene CONFIGURATION_CHANGED con DENIED e INWEB_OWNS_DATA")]
    public void Bitacora_del_guardado_denegado()
    {
        MarcarOrigen(Fixture.SnapshotId, "INWEB");
        Admin.GuardarOpciones([ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), 180000)]);

        new EscritorBitacoras(Base, Carpeta, Reloj).Entregar();

        var contexto = Assert.Single(Lineas(DestinoBitacora.General), l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("MENU_OPTION", contexto.GetProperty("Setting").GetString());
        Assert.Equal("DENIED", contexto.GetProperty("Result").GetString());
        Assert.Equal("INWEB_OWNS_DATA", contexto.GetProperty("ReasonCode").GetString());
    }

    [Fact(DisplayName = "administracion-local · D14: la versión nueva sube el padrón por una persona y el catálogo por una opción, lleva el esquema 4 y un hash SHA-256 de su contenido")]
    public void Numeros_y_hash_de_la_version()
    {
        Admin.GuardarPersonas([Edicion(Fixture.Habilitada.Id) with { Nombre = "Persona renombrada" }]);
        Admin.GuardarOpciones([ConImporte(EdicionOpcion(Fixture.AlmuerzoComun.Id), 180000)]);

        using var conexion = Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT comvcVersionConfiguracion, comvcVersionPoliticas, comvcVersionCatalogo, comvcVersionPadron, comvcVersionEsquema, comvcHashContenido, comvcEstado FROM tbComedorVersionesConfiguracion ORDER BY comvcVersionConfiguracion";
        using var lector = comando.ExecuteReader();
        var filas = new List<(long Config, long Politicas, long Catalogo, long Padron, long Esquema, byte[] Hash, string Estado)>();
        while (lector.Read())
        {
            filas.Add((lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2), lector.GetInt64(3), lector.GetInt64(4), (byte[])lector.GetValue(5), lector.GetString(6)));
        }

        Assert.Equal(3, filas.Count);
        Assert.Equal((1L, 1L, 1L, 1L, "SUPERSEDED"), (filas[0].Config, filas[0].Politicas, filas[0].Catalogo, filas[0].Padron, filas[0].Estado));
        Assert.Equal((2L, 1L, 1L, 2L, 4L, "SUPERSEDED"), (filas[1].Config, filas[1].Politicas, filas[1].Catalogo, filas[1].Padron, filas[1].Esquema, filas[1].Estado));
        Assert.Equal((3L, 1L, 2L, 2L, 4L, "ACTIVE"), (filas[2].Config, filas[2].Politicas, filas[2].Catalogo, filas[2].Padron, filas[2].Esquema, filas[2].Estado));
        Assert.Equal(32, filas[2].Hash.Length);
        Assert.NotEqual(new byte[32], filas[2].Hash);
        Assert.NotEqual(filas[1].Hash, filas[2].Hash);
    }

    // ── Ayudantes ────────────────────────────────────────────────────────────────────────────────────────────

    private EdicionPersona Edicion(Guid personaId)
    {
        var persona = Assert.Single(Admin.Leer().Personas, p => p.Id == personaId);
        return new EdicionPersona(persona.Id, persona.Nombre, persona.Cupos.Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)).ToList());
    }

    private static EdicionPersona ConCupo(EdicionPersona edicion, Guid servicio, Guid categoria, int limite) => edicion with
    {
        Cupos = edicion.Cupos.Where(c => !(c.ServicioId == servicio && c.CategoriaId == categoria)).Append(new CupoEditado(servicio, categoria, limite)).ToList(),
    };

    private static EdicionPersona Alta(string nombre, string codigo, params (Guid Servicio, Guid Categoria, int Limite)[] cupos) =>
        new(null, nombre, cupos.Select(c => new CupoEditado(c.Servicio, c.Categoria, c.Limite)).ToList(), codigo);

    private EdicionOpcion EdicionOpcion(Guid opcionId)
    {
        var opcion = Assert.Single(Admin.Leer().Opciones, o => o.Id == opcionId);
        return new EdicionOpcion(opcion.Id, opcion.Nombre, opcion.ServicioId, opcion.ImporteUnidadesMenores,
            opcion.Cargas.Select(c => new CargaEditada(c.CategoriaId, c.Unidades)).ToList(),
            opcion.Franjas.Select(f => new FranjaEditada(f.Inicio.ToString("HH:mm"), f.Fin.ToString("HH:mm"))).ToList());
    }

    private static EdicionOpcion ConImporte(EdicionOpcion edicion, long importe) => edicion with { ImporteUnidadesMenores = importe };

    private static CupoAdministrado? Cupo(VistaAdministracion vista, Guid persona, Guid servicio, Guid categoria) =>
        vista.Personas.Single(p => p.Id == persona).Cupos.SingleOrDefault(c => c.ServicioId == servicio && c.CategoriaId == categoria);

    private ResultadoConfirmacion Atender(string codigo, Guid opcionId)
    {
        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(codigo, PropositoSesion.Consumption);
        Assert.True(sesion.Abierta);
        var opcion = new ConsultaCatalogo(Base).Leer().Single(o => o.Id == opcionId);
        var resultado = new UnidadConfirmacion(Base, Reloj).Confirmar(
            new SolicitudConfirmacion(Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, opcion.Id, opcion.Version, 1));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        return resultado;
    }

    private OfertaDePersona Ofrecer(string codigo)
    {
        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(codigo, PropositoSesion.Consumption);
        Assert.True(sesion.Abierta);
        return new ConsultaDisponibilidad(Base, Reloj).Ofrecer(sesion.IdentitySessionId!.Value, sesion.PersonId!.Value);
    }

    private void MarcarOrigen(Guid snapshot, string origen)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) VALUES ($vc, $origen)",
            ("$vc", snapshot), ("$origen", origen));
    }

    private int VersionActiva()
    {
        using var conexion = Base.AbrirConexion();
        return (int)Sql.Escalar<long>(conexion,
            "SELECT vc.comvcVersionConfiguracion FROM tbComedorConfiguracionPuestos cp JOIN tbComedorVersionesConfiguracion vc ON vc.comvcCodigo = cp.comvcCodigoActiva");
    }

    private Guid SnapshotActivo()
    {
        using var conexion = Base.AbrirConexion();
        return Sql.EscalarGuid(conexion, "SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos");
    }

    private long Contar(string tabla)
    {
        using var conexion = Base.AbrirConexion();
        return Sql.Contar(conexion, tabla);
    }

    /// <summary>Límite, prioridad y si no tiene vigencia, de cada regla del par en la versión activa.</summary>
    private List<(long Limite, long Prioridad, bool SinVigencia)> ReglasDelPar(Guid persona, Guid servicio, Guid categoria)
    {
        using var conexion = Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT comregLimite, comregPrioridad, comregFechaVigenciaDesdeUtc IS NULL AND comregFechaVigenciaHastaUtc IS NULL
            FROM tbComedorReglasHabilitacion
            WHERE comvcCodigo = (SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos)
              AND comperCodigo = $per AND comsrvCodigo = $srv AND comcatCodigo = $cat AND comacCodigo = $amb
            """;
        comando.Parameters.AddWithValue("$per", persona);
        comando.Parameters.AddWithValue("$srv", servicio);
        comando.Parameters.AddWithValue("$cat", categoria);
        comando.Parameters.AddWithValue("$amb", Fixture.Ambito);
        using var lector = comando.ExecuteReader();
        var reglas = new List<(long, long, bool)>();
        while (lector.Read())
        {
            reglas.Add((lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2) == 1));
        }

        return reglas;
    }

    /// <summary>Cada fila de cada tabla de configuración de una versión, como texto comparable.</summary>
    private List<string> FotoDeVersion(Guid snapshot)
    {
        using var conexion = Base.AbrirConexion();
        var foto = new List<string>();
        foreach (var tabla in TablasDeConfiguracion)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM {tabla} WHERE comvcCodigo = $vc ORDER BY 2, 3";
            comando.Parameters.AddWithValue("$vc", snapshot);
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                foto.Add(tabla + ": " + string.Join(" | ", Enumerable.Range(0, lector.FieldCount).Select(i => lector.IsDBNull(i) ? "NULL" : lector.GetValue(i).ToString())));
            }
        }

        return foto;
    }

    private List<JsonElement> Lineas(DestinoBitacora destino)
    {
        var ruta = Path.Combine(Carpeta, "bitacoras", Fixture.PuestoId.ToString(), EscritorBitacoras.NombreArchivo(destino, "202611"));
        return !File.Exists(ruta)
            ? []
            : File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
    }

    private static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    /// <summary>
    /// Ningún valor de texto de ninguna línea de las dos bitácoras contiene el código. Los identificadores sintéticos son
    /// UUID al azar y pueden contener esos dígitos: se excluyen los valores que son un UUID, como en el E2E de B.
    /// </summary>
    private void AfirmarSinElCodigo(string codigo)
    {
        var lineas = Lineas(DestinoBitacora.General).Concat(Lineas(DestinoBitacora.Interacciones)).ToList();
        Assert.NotEmpty(lineas);
        foreach (var linea in lineas)
        {
            Assert.All(Textos(linea).Where(t => !Guid.TryParse(t, out _)), t => Assert.DoesNotContain(codigo, t));
        }
    }

    private static IEnumerable<string> Textos(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.String => [elemento.GetString()!],
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(p => Textos(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Textos),
        _ => [elemento.GetRawText()],
    };
}
