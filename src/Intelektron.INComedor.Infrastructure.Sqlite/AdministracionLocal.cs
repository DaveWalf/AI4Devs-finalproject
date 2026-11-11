using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Intelektron.INComedor.Contracts;
using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;
using static Intelektron.INComedor.Infrastructure.Sqlite.CodigosAdministracion;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// La administración en el puesto de personas, códigos, cupos, opciones y franjas mientras no haya INWeb
/// (administrar-personas-y-opciones). Cada guardado copia la versión activa entera, le aplica la edición, la activa y deja
/// un CONFIGURATION_CHANGED por entidad cambiada, todo en una transacción BEGIN IMMEDIATE (D1 y D13). Valida antes de
/// escribir, con SQL parametrizado (D11), y nunca muestra ni registra un código (D10).
/// </summary>
public sealed class AdministracionLocal
{
    public const int LargoNombrePersona = 80;
    public const int LargoNombreOpcion = 60;
    public const int LargoCodigo = 20;
    public const int LimiteMaximo = 99;
    public const long ImporteMaximo = 999_999_999;
    public const int UnidadesMaximas = 9;
    public const int VersionEsquema = 4;

    private const string SinAutenticacion = "NO_AUTHENTICATION";

    /// <summary>Las tablas de configuración de una versión, en el orden de sus claves foráneas, con sus columnas sin comvcCodigo.</summary>
    private static readonly (string Tabla, string[] Columnas)[] TablasDeConfiguracion =
    [
        ("tbComedorPersonas", ["comperCodigo", "comperNombreVisible", "comperActivo", "comperVersionMaterialIdentidad"]),
        ("tbComedorCredencialesPersonas", ["comcreCodigo", "comperCodigo", "comcreTipo", "comcreValorNormalizado",
            "comcreFechaVigenciaDesdeUtc", "comcreFechaVigenciaHastaUtc", "comcreFechaRevocacionUtc"]),
        ("tbComedorTiposServicio", ["comsrvCodigo", "comsrvNombre"]),
        ("tbComedorAmbitosCupo", ["comacCodigo", "comacDefinicion"]),
        ("tbComedorCategoriasCupo", ["comcatCodigo", "comcatNombre"]),
        ("tbComedorOpcionesMenu", ["commenCodigo", "commenVersionMenu", "comsrvCodigo", "commenNombre", "commenMoneda",
            "commenEscala", "commenImporteEmpleadoUnidadesMenores"]),
        ("tbComedorOpcionesMenu_CategoriasCupo", ["commenCodigo", "comcatCodigo", "commccUnidades"]),
        ("tbComedorReglasHabilitacion", ["comregCodigo", "comperCodigo", "comgrpCodigo", "comsrvCodigo", "comacCodigo",
            "comcatCodigo", "comregLimite", "comregPrioridad", "comregFechaVigenciaDesdeUtc", "comregFechaVigenciaHastaUtc"]),
        ("tbComedorPermisosOperadores", ["compopCodigo", "comperCodigo", "compopRol", "compopFechaVigenciaDesdeUtc",
            "compopFechaVigenciaHastaUtc", "compopFechaRevocacionUtc"]),
        ("tbComedorFranjasOpcionesMenu", ["comfraCodigo", "commenCodigo", "comfraHoraInicio", "comfraHoraFin"]),
    ];

    private static readonly StringComparer OrdenPorNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), ignoreCase: true);

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.AdministracionLocal");

    public AdministracionLocal(BaseComedor baseComedor, TimeProvider reloj)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
    }

    /// <summary>El reloj del puesto, el de la fecha de negocio de lo usado hoy, para nombrar una exportación (D5 de editar-raciones-en-personas).</summary>
    public TimeProvider Reloj => _reloj;

    /// <summary>La versión activa como la muestran los paneles, con su origen.</summary>
    public VistaAdministracion Leer()
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = conexion.BeginTransaction(deferred: true);
        var vista = LeerVista(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), _reloj.GetUtcNow());
        transaccion.Commit();
        return vista;
    }

    /// <summary>
    /// La versión activa entera, tabla por tabla, en JSON (D1 de resguardar-configuracion): las tablas y columnas de
    /// <see cref="TablasDeConfiguracion"/> sin la versión, con enteros, textos o nulos.
    /// </summary>
    public ConfiguracionLeida LeerConfiguracion()
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = conexion.BeginTransaction(deferred: true);
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        using var flujo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(flujo, new JsonWriterOptions { Indented = true }))
        {
            EscribirTablas(escritor, conexion, transaccion, puesto.SnapshotId);
        }

        transaccion.Commit();
        return new ConfiguracionLeida(puesto.VersionConfiguracion, VersionEsquema, Encoding.UTF8.GetString(flujo.ToArray()));
    }

    /// <summary>
    /// Revisa entera la configuración de un resguardo, sin dejar nada escrito (D2): la ensaya como la recuperación, en una
    /// transacción BEGIN IMMEDIATE que se deshace. Con INWeb como dueño, el motivo es el de un guardado denegado.
    /// </summary>
    public RevisionConfiguracion RevisarConfiguracion(string configuracion)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var revision = Ensayar(conexion, transaccion, _reloj.GetUtcNow(), configuracion, out _);
        transaccion.Rollback();
        return revision;
    }

    /// <summary>
    /// Crea una versión nueva con la configuración de un resguardo y la activa, como un guardado (D3): denegado con INWeb
    /// como dueño; rechazado si no tiene las tablas, las columnas, los tipos y los valores de un guardado de este esquema, o
    /// si la base o la pantalla no pueden tomar lo que trae (INVALID_BACKUP), o si un código es, en esta base, de otra
    /// persona en cualquier versión (CODE_CONFLICT, RB25). Todo en una transacción BEGIN IMMEDIATE: un rechazo no escribe
    /// nada. Los registros de la recuperación los deja quien la pide (D5): reemplazar la configuración no edita entidades.
    /// </summary>
    public ResultadoGuardado RecuperarVersion(string configuracion)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var anterior = PuestoLocal.Leer(conexion, transaccion).VersionConfiguracion;
        var revision = Ensayar(conexion, transaccion, _reloj.GetUtcNow(), configuracion, out var numero);
        if (revision.Motivo is { } motivo)
        {
            transaccion.Rollback();
            var tipo = motivo == INWebAdministra ? TipoGuardado.Denegado : TipoGuardado.Rechazado;
            return new ResultadoGuardado(tipo, anterior, motivo, []);
        }

        transaccion.Commit();
        return new ResultadoGuardado(TipoGuardado.Aplicado, numero, null, []);
    }

    /// <summary>
    /// Lo que una importación necesita además de la vista, leído en la misma transacción (D3 y D4 de
    /// importar-personas-desde-excel): el código vigente de cada persona activa, los códigos usados en cualquier versión, que
    /// no se reasignan (RB25), y las personas con un permiso de operador o de administrador sin revocar.
    /// </summary>
    internal PadronImportacion LeerParaImportar()
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = conexion.BeginTransaction(deferred: true);
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        var ahora = _reloj.GetUtcNow();
        var vista = LeerVista(conexion, transaccion, puesto, ahora);
        var activas = vista.Personas.Where(p => p.Activa).Select(p => p.Id).ToHashSet();

        // Una credencial sin revocar de una persona activa la reconoce aunque esté vencida o todavía no rija: la fila corrige
        // a esa persona; sus fechas no se tocan (D3).
        var vigentes = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var credenciales = Leer(conexion, transaccion,
            """
            SELECT comperCodigo, comcreValorNormalizado FROM tbComedorCredencialesPersonas
            WHERE comvcCodigo = $vc AND comcreTipo = 'SERIAL_CODE' AND comcreFechaRevocacionUtc IS NULL
            """, [("$vc", puesto.SnapshotId)],
            l => (Persona: l.GetGuid(0), Valor: l.GetString(1)));
        foreach (var credencial in credenciales.Where(c => activas.Contains(c.Persona)))
        {
            vigentes.TryAdd(credencial.Valor, credencial.Persona);
        }

        var usados = Leer(conexion, transaccion,
                "SELECT DISTINCT comcreValorNormalizado FROM tbComedorCredencialesPersonas WHERE comcreTipo = 'SERIAL_CODE'", [], l => l.GetString(0))
            .ToHashSet(StringComparer.Ordinal);
        var conPermiso = Leer(conexion, transaccion,
                """
                SELECT DISTINCT comperCodigo FROM tbComedorPermisosOperadores
                WHERE comvcCodigo = $vc AND compopRol IN ('OPERATOR', 'SYSTEM_ADMINISTRATOR') AND compopFechaRevocacionUtc IS NULL
                """, [("$vc", puesto.SnapshotId)], l => l.GetGuid(0))
            .ToHashSet();
        transaccion.Commit();
        return new PadronImportacion(vista, vigentes, usados, conPermiso);
    }

    /// <summary>
    /// Guarda las personas. Con <paramref name="actor"/>, el operador que entró a Configuración con su código, cada registro lo
    /// lleva como actor; sin él, el nulo con NO_AUTHENTICATION (D7 de entrar-a-configuracion-con-teclado).
    /// </summary>
    public ResultadoGuardado GuardarPersonas(IReadOnlyList<EdicionPersona> ediciones, Guid? actor = null) =>
        Guardar("PERSON", (c, vista) => ValidarPersonas(c, vista, ediciones, actor), (c, vista, cambios) => AplicarPersonas(c, vista, ediciones, cambios), actor);

    /// <summary>Guarda las opciones, con el actor como en <see cref="GuardarPersonas"/>.</summary>
    public ResultadoGuardado GuardarOpciones(IReadOnlyList<EdicionOpcion> ediciones, Guid? actor = null) =>
        Guardar("MENU_OPTION", (_, vista) => ValidarOpciones(vista, ediciones), (c, vista, cambios) => AplicarOpciones(c, vista, ediciones, cambios), actor);

    private ResultadoGuardado Guardar(
        string ajuste,
        Func<Contexto, VistaAdministracion, List<CampoRechazado>> validar,
        Action<Contexto, VistaAdministracion, Cambios> aplicar,
        Guid? actor)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        var contexto = new Contexto(conexion, transaccion, puesto, ahora);
        var vista = LeerVista(conexion, transaccion, puesto, ahora);

        if (vista.SoloLectura)
        {
            RegistrarSinCambio(contexto, ajuste, "DENIED", INWebAdministra, [], actor);
            transaccion.Commit();
            return new ResultadoGuardado(TipoGuardado.Denegado, vista.VersionConfiguracion, INWebAdministra, []);
        }

        var campos = validar(contexto, vista);
        if (campos.Count > 0)
        {
            // Un solo motivo que explica todos los campos lo nombra; si no, el campo inválido de siempre.
            var motivo = campos.All(c => c.Codigo == CodigoDuplicado) ? CodigoDuplicado
                : campos.All(c => c.Codigo == PermisoPropio) ? PermisoPropio
                : CampoInvalido;
            RegistrarSinCambio(contexto, ajuste, "REJECTED", motivo, campos, actor);
            transaccion.Commit();
            return new ResultadoGuardado(TipoGuardado.Rechazado, vista.VersionConfiguracion, motivo, campos);
        }

        var cambios = new Cambios(Guid.NewGuid());
        var numero = CrearVersion(contexto, cambios.Version);
        aplicar(contexto, vista, cambios);
        ActivarVersion(contexto, cambios, numero);

        foreach (var registro in cambios.Registros)
        {
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "CONFIGURATION_CHANGED", ConActor(registro(vista.VersionConfiguracion, numero), actor));
        }

        transaccion.Commit();
        return new ResultadoGuardado(TipoGuardado.Aplicado, numero, null, []);
    }

    // ── Lectura ──────────────────────────────────────────────────────────────────────────────────────────────

    private static VistaAdministracion LeerVista(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, DateTimeOffset ahora)
    {
        var vc = puesto.SnapshotId;
        var origen = Texto(conexion, transaccion, "SELECT comvoOrigen FROM tbComedorOrigenesVersiones WHERE comvcCodigo = $vc", ("$vc", vc)) == "INWEB"
            ? OrigenConfiguracion.INWeb
            : OrigenConfiguracion.Local;

        var servicios = Leer(conexion, transaccion, "SELECT comsrvCodigo, comsrvNombre FROM tbComedorTiposServicio WHERE comvcCodigo = $vc", [("$vc", vc)],
                l => new ServicioAdministrado(l.GetGuid(0), l.GetString(1)))
            .OrderBy(s => s.Nombre, OrdenPorNombre).ToList();
        var categorias = Leer(conexion, transaccion, "SELECT comcatCodigo, comcatNombre FROM tbComedorCategoriasCupo WHERE comvcCodigo = $vc", [("$vc", vc)],
                l => new CategoriaAdministrada(l.GetGuid(0), l.GetString(1)))
            .OrderBy(c => c.Nombre, OrdenPorNombre).ToList();
        var nombreServicio = servicios.ToDictionary(s => s.Id, s => s.Nombre);
        var nombreCategoria = categorias.ToDictionary(c => c.Id, c => c.Nombre);

        var cargas = Leer(conexion, transaccion,
                "SELECT commenCodigo, comcatCodigo, commccUnidades FROM tbComedorOpcionesMenu_CategoriasCupo WHERE comvcCodigo = $vc", [("$vc", vc)],
                l => (Opcion: l.GetGuid(0), Carga: new CargaAdministrada(l.GetGuid(1), nombreCategoria[l.GetGuid(1)], l.GetInt32(2))))
            .ToLookup(c => c.Opcion, c => c.Carga);
        var franjas = Leer(conexion, transaccion,
                "SELECT commenCodigo, comfraHoraInicio, comfraHoraFin FROM tbComedorFranjasOpcionesMenu WHERE comvcCodigo = $vc", [("$vc", vc)],
                l => (Opcion: l.GetGuid(0), Franja: new FranjaHoraria(LecturaDecision.LeerHora(l.GetString(1)), LecturaDecision.LeerHora(l.GetString(2)))))
            .ToLookup(f => f.Opcion, f => f.Franja);
        var opciones = Leer(conexion, transaccion,
                """
                SELECT commenCodigo, commenVersionMenu, commenNombre, comsrvCodigo, commenMoneda, commenEscala, commenImporteEmpleadoUnidadesMenores
                FROM tbComedorOpcionesMenu WHERE comvcCodigo = $vc
                """, [("$vc", vc)],
                l => new OpcionAdministrada(l.GetGuid(0), l.GetInt32(1), l.GetString(2), l.GetGuid(3), nombreServicio[l.GetGuid(3)],
                    l.GetString(4), l.GetInt32(5), l.GetInt64(6),
                    cargas[l.GetGuid(0)].OrderBy(c => c.Categoria, OrdenPorNombre).ToList(),
                    franjas[l.GetGuid(0)].OrderBy(f => f.Inicio).ThenBy(f => f.Fin).ToList()))
            .OrderBy(o => o.Nombre, OrdenPorNombre).ThenBy(o => o.Id.ToString("D", CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToList();

        var reglas = Leer(conexion, transaccion,
                """
                SELECT comperCodigo, comsrvCodigo, comcatCodigo, comregLimite, comregPrioridad, comregFechaVigenciaDesdeUtc, comregFechaVigenciaHastaUtc
                FROM tbComedorReglasHabilitacion WHERE comvcCodigo = $vc AND comperCodigo IS NOT NULL
                """, [("$vc", vc)],
                l => new ReglaLeida(l.GetGuid(0), l.GetGuid(1), l.GetGuid(2), l.GetInt32(3), l.GetInt32(4),
                    LecturaDecision.LeerInstanteOpcional(l, 5), LecturaDecision.LeerInstanteOpcional(l, 6)))
            .ToLookup(r => r.Persona);

        // Lo usado hoy por persona y categoría, de la misma tabla con que la atención cuenta el cupo (D1 de editar-raciones-en-personas).
        var fecha = CalculadoraFechaNegocio.Calcular(ahora, puesto.ZonaHoraria.Id, puesto.InicioDiaNegocio);
        var usados = Leer(conexion, transaccion,
                """
                SELECT comperCodigo, comcatCodigo, SUM(comucUsado) FROM tbComedorUsosCupo
                WHERE comucFechaNegocio = $fecha GROUP BY comperCodigo, comcatCodigo
                """, [("$fecha", LecturaDecision.Texto(fecha))],
                l => (Persona: l.GetGuid(0), Categoria: l.GetGuid(1), Usado: l.GetInt32(2)))
            .ToDictionary(u => (u.Persona, u.Categoria), u => u.Usado);

        // Quién es operador ahora: el permiso OPERATOR que el evaluador de RB26 da por vigente en este instante (D1 de
        // marcar-operadores-en-personas).
        var permisos = Leer(conexion, transaccion,
                """
                SELECT compopCodigo, comperCodigo, compopRol, compopFechaVigenciaDesdeUtc, compopFechaVigenciaHastaUtc, compopFechaRevocacionUtc
                FROM tbComedorPermisosOperadores WHERE comvcCodigo = $vc
                """, [("$vc", vc)],
                l => (Persona: l.GetGuid(1), Permiso: new PermisoOperador(l.GetGuid(0),
                    l.GetString(2) == "OPERATOR" ? RolOperador.Operator : RolOperador.SystemAdministrator,
                    LecturaDecision.LeerInstanteOpcional(l, 3), LecturaDecision.LeerInstanteOpcional(l, 4), LecturaDecision.LeerInstanteOpcional(l, 5))))
            .ToLookup(p => p.Persona, p => p.Permiso);

        var personas = Leer(conexion, transaccion,
                "SELECT comperCodigo, comperNombreVisible, comperActivo FROM tbComedorPersonas WHERE comvcCodigo = $vc", [("$vc", vc)],
                l => (Id: l.GetGuid(0), Nombre: l.GetString(1), Activa: l.GetInt64(2) == 1))
            .Select(p =>
            {
                var cupos = reglas[p.Id]
                    .GroupBy(r => (r.Servicio, r.Categoria))
                    .Select(g => CupoDe(g.ToList(), nombreServicio, nombreCategoria, ahora) with
                    {
                        UsadoHoy = usados.GetValueOrDefault((p.Id, g.Key.Categoria)),
                    })
                    .OrderBy(c => c.Servicio, OrdenPorNombre).ThenBy(c => c.Categoria, OrdenPorNombre)
                    .ToList();
                var habilitadas = opciones
                    .Where(o => o.Cargas.All(carga => cupos.Any(c => c.ServicioId == o.ServicioId && c.CategoriaId == carga.CategoriaId && c.Limite > 0)))
                    .Select(o => o.Id)
                    .ToList();
                var operador = EvaluadorPermisoOperador.Evaluar(permisos[p.Id].ToList(), RolOperador.Operator, ahora).Concedido;
                return new PersonaAdministrada(p.Id, p.Nombre, p.Activa, cupos, habilitadas, operador);
            })
            .OrderBy(p => p.Nombre, OrdenPorNombre).ThenBy(p => p.Id.ToString("D", CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToList();

        return new VistaAdministracion(puesto.VersionConfiguracion, origen, servicios, categorias, personas, opciones);
    }

    /// <summary>
    /// El límite que hoy decide el par: entre las reglas vigentes (o todas, si ninguna lo está) una regla cero prevalece y,
    /// si no, la de mayor prioridad (RB02). Un par con más de una regla, o con prioridad o vigencia, se reemplaza al editarlo.
    /// </summary>
    private static CupoAdministrado CupoDe(
        List<ReglaLeida> reglas, Dictionary<Guid, string> servicios, Dictionary<Guid, string> categorias, DateTimeOffset ahora)
    {
        var vigentes = reglas.Where(r => (r.Desde is null || r.Desde <= ahora) && (r.Hasta is null || ahora < r.Hasta)).ToList();
        var candidatas = vigentes.Count > 0 ? vigentes : reglas;
        var limite = candidatas.Any(r => r.Limite == 0) ? 0 : candidatas.OrderByDescending(r => r.Prioridad).First().Limite;
        var reemplazables = reglas.Count > 1 || reglas.Any(r => r.Prioridad != 1 || r.Desde is not null || r.Hasta is not null);
        var primera = reglas[0];
        return new CupoAdministrado(primera.Servicio, servicios[primera.Servicio], primera.Categoria, categorias[primera.Categoria], limite, reemplazables);
    }

    // ── Validación (D11) ─────────────────────────────────────────────────────────────────────────────────────

    private static List<CampoRechazado> ValidarPersonas(Contexto contexto, VistaAdministracion vista, IReadOnlyList<EdicionPersona> ediciones, Guid? actor)
    {
        var porId = vista.Personas.ToDictionary(p => p.Id);
        var campos = new List<CampoRechazado>();
        var personas = vista.Personas.Select(p => p.Id).ToHashSet();
        var servicios = vista.Servicios.Select(s => s.Id).ToHashSet();
        var categorias = vista.Categorias.Select(c => c.Id).ToHashSet();
        var vistas = new HashSet<Guid>();
        var codigos = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < ediciones.Count; i++)
        {
            var edicion = ediciones[i];
            void Rechazar(string campo, string codigo) => campos.Add(new CampoRechazado(i, campo, codigo));

            if (edicion.PersonaId is { } id)
            {
                if (!personas.Contains(id))
                {
                    Rechazar("PersonaId", Desconocido);
                }
                else if (!vistas.Add(id))
                {
                    Rechazar("PersonaId", Repetido);
                }

                if (edicion.Codigo is not null)
                {
                    Rechazar("Codigo", NoEditable);
                }
            }
            else if (edicion.Quitar)
            {
                Rechazar("PersonaId", Requerido);
            }
            else if (ValidarCodigo(edicion.Codigo) is { } error)
            {
                Rechazar("Codigo", error);
            }
            else if (!codigos.Add(edicion.Codigo!) || CodigoExiste(contexto, edicion.Codigo!))
            {
                Rechazar("Codigo", CodigoDuplicado);
            }

            if (ValidarTexto(edicion.Nombre, LargoNombrePersona) is { } nombre)
            {
                Rechazar("Nombre", nombre);
            }

            // El permiso de operador (D2 de marcar-operadores-en-personas): un operador no cambia el suyo, y nadie marca a una
            // persona dada de baja, que no puede identificarse.
            if (edicion.Operador is { } quiere)
            {
                var actual = edicion.PersonaId is { } existente ? porId.GetValueOrDefault(existente) : null;
                if (actor is { } quien && edicion.PersonaId == quien && quiere != (actual?.Operador ?? false))
                {
                    Rechazar("Operador", PermisoPropio);
                }
                else if (quiere && (edicion.Quitar || actual is { Activa: false }) && actual?.Operador != true)
                {
                    Rechazar("Operador", NoEditable);
                }
            }

            var pares = new HashSet<(Guid, Guid)>();
            for (var j = 0; j < edicion.Cupos.Count; j++)
            {
                var cupo = edicion.Cupos[j];
                if (!servicios.Contains(cupo.ServicioId))
                {
                    Rechazar($"Cupos[{j}].Servicio", Desconocido);
                }

                if (!categorias.Contains(cupo.CategoriaId))
                {
                    Rechazar($"Cupos[{j}].Categoria", Desconocido);
                }

                if (cupo.Limite is < 0 or > LimiteMaximo)
                {
                    Rechazar($"Cupos[{j}].Limite", FueraDeRango);
                }

                if (!pares.Add((cupo.ServicioId, cupo.CategoriaId)))
                {
                    Rechazar($"Cupos[{j}]", Repetido);
                }
            }
        }

        return campos;
    }

    private static List<CampoRechazado> ValidarOpciones(VistaAdministracion vista, IReadOnlyList<EdicionOpcion> ediciones)
    {
        var campos = new List<CampoRechazado>();
        var opciones = vista.Opciones.Select(o => o.Id).ToHashSet();
        var servicios = vista.Servicios.Select(s => s.Id).ToHashSet();
        var categorias = vista.Categorias.Select(c => c.Id).ToHashSet();
        var vistas = new HashSet<Guid>();

        for (var i = 0; i < ediciones.Count; i++)
        {
            var edicion = ediciones[i];
            void Rechazar(string campo, string codigo) => campos.Add(new CampoRechazado(i, campo, codigo));

            if (edicion.OpcionId is { } id)
            {
                if (!opciones.Contains(id))
                {
                    Rechazar("OpcionId", Desconocido);
                }
                else if (!vistas.Add(id))
                {
                    Rechazar("OpcionId", Repetido);
                }
            }
            else if (edicion.Quitar)
            {
                Rechazar("OpcionId", Requerido);
            }

            if (edicion.Quitar)
            {
                continue;
            }

            if (ValidarTexto(edicion.Nombre, LargoNombreOpcion) is { } nombre)
            {
                Rechazar("Nombre", nombre);
            }

            if (!servicios.Contains(edicion.ServicioId))
            {
                Rechazar("Servicio", Desconocido);
            }

            if (edicion.ImporteUnidadesMenores is < 0 or > ImporteMaximo)
            {
                Rechazar("Importe", FueraDeRango);
            }

            if (edicion.Cargas.Count == 0)
            {
                Rechazar("Cargas", Requerido);
            }

            var categoriasDeLaOpcion = new HashSet<Guid>();
            for (var j = 0; j < edicion.Cargas.Count; j++)
            {
                var carga = edicion.Cargas[j];
                if (!categorias.Contains(carga.CategoriaId))
                {
                    Rechazar($"Cargas[{j}].Categoria", Desconocido);
                }
                else if (!categoriasDeLaOpcion.Add(carga.CategoriaId))
                {
                    Rechazar($"Cargas[{j}].Categoria", Repetido);
                }

                if (carga.Unidades is < 1 or > UnidadesMaximas)
                {
                    Rechazar($"Cargas[{j}].Unidades", FueraDeRango);
                }
            }

            for (var j = 0; j < edicion.Franjas.Count; j++)
            {
                var franja = edicion.Franjas[j];
                if (!LeerHoraEditada(franja.Inicio, out var inicio) || !LeerHoraEditada(franja.Fin, out var fin))
                {
                    Rechazar($"Franjas[{j}]", HoraInvalida);
                }
                else if (inicio == fin)
                {
                    Rechazar($"Franjas[{j}]", FranjaVacia);
                }
            }
        }

        return campos;
    }

    /// <summary>De 1 al largo dado, sin caracteres de control ni separadores de línea; null si es válido.</summary>
    public static string? ValidarTexto(string? texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Requerido;
        }

        if (texto.Any(c => char.IsControl(c)
                || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
        {
            return CaracterDeControl;
        }

        return texto.Length > largo ? DemasiadoLargo : null;
    }

    /// <summary>De 1 a 20 dígitos ASCII, conservado tal cual con sus ceros iniciales (RB01); null si es válido.</summary>
    public static string? ValidarCodigo(string? codigo)
    {
        if (string.IsNullOrEmpty(codigo))
        {
            return Requerido;
        }

        if (!codigo.All(char.IsAsciiDigit))
        {
            return NoSonDigitos;
        }

        return codigo.Length > LargoCodigo ? DemasiadoLargo : null;
    }

    public static bool LeerHoraEditada(string? texto, out TimeOnly hora) =>
        TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out hora);

    /// <summary>El código no se reasigna: se busca en todas las versiones, revocado o no, de cualquier persona (RB25).</summary>
    private static bool CodigoExiste(Contexto c, string codigo) =>
        ComandosSqlite.EscalarLong(c.Conexion, c.Transaccion,
            "SELECT COUNT(*) FROM tbComedorCredencialesPersonas WHERE comcreTipo = 'SERIAL_CODE' AND comcreValorNormalizado = $valor",
            ("$valor", codigo)) > 0;

    // ── La versión nueva (D1 y D14) ──────────────────────────────────────────────────────────────────────────

    private static int CrearVersion(Contexto c, Guid nueva)
    {
        var numero = InsertarVersion(c, nueva);
        foreach (var (tabla, columnas) in TablasDeConfiguracion)
        {
            var lista = string.Join(", ", columnas);
            ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                $"INSERT INTO {tabla} (comvcCodigo, {lista}) SELECT $nueva, {lista} FROM {tabla} WHERE comvcCodigo = $activa",
                ("$nueva", nueva), ("$activa", c.Puesto.SnapshotId));
        }

        return numero;
    }

    /// <summary>La fila de la versión nueva, con los números de la activa, todavía sin contenido.</summary>
    private static int InsertarVersion(Contexto c, Guid nueva)
    {
        var numero = (int)ComandosSqlite.EscalarLong(c.Conexion, c.Transaccion,
            "SELECT MAX(comvcVersionConfiguracion) + 1 FROM tbComedorVersionesConfiguracion");
        var ahora = FormatoInstante.Texto(c.Ahora);
        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            """
            INSERT INTO tbComedorVersionesConfiguracion (comvcCodigo, comvcVersionConfiguracion, comvcVersionPoliticas, comvcVersionCatalogo,
                comvcVersionPadron, comvcVersionEsquema, comvcHashContenido, comvcEstado, comvcFechaPublicacionUtc, comvcFechaActivacionUtc)
            SELECT $nueva, $numero, comvcVersionPoliticas, comvcVersionCatalogo, comvcVersionPadron, $esquema, zeroblob(32), 'ACTIVE', $ahora, $ahora
            FROM tbComedorVersionesConfiguracion WHERE comvcCodigo = $activa
            """,
            ("$nueva", nueva), ("$numero", numero), ("$esquema", VersionEsquema), ("$ahora", ahora), ("$activa", c.Puesto.SnapshotId));
        return numero;
    }

    /// <summary>
    /// Las filas de una tabla del resguardo en la versión nueva, con un comando preparado y SQL parametrizado (D3): cada valor
    /// ya tiene el tipo de su columna (un GUID, un texto, un entero o un nulo) y se enlaza como lo enlaza cualquier guardado.
    /// </summary>
    private static void InsertarFilas(Contexto c, Guid version, string tabla, string[] columnas, List<object[]> filas)
    {
        var parametros = columnas.Select((_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
        using var comando = ComandosSqlite.Crear(c.Conexion, c.Transaccion,
            $"INSERT INTO {tabla} (comvcCodigo, {string.Join(", ", columnas)}) VALUES ($vc, {string.Join(", ", parametros)})",
            ("$vc", version));
        var valores = parametros.Select(p => comando.Parameters.Add(new SqliteParameter { ParameterName = p })).ToArray();
        foreach (var fila in filas)
        {
            for (var i = 0; i < fila.Length; i++)
            {
                valores[i].Value = fila[i];
            }

            comando.ExecuteNonQuery();
        }
    }

    // ── El resguardo (D1 a D3 de resguardar-configuracion) ───────────────────────────────────────────────────

    /// <summary>Cómo se lee cada columna de un resguardo: lo que la base guarda y la pantalla lee de ella.</summary>
    private enum TipoColumna
    {
        Guid,
        Instante,
        Hora,
        Entero,
        Texto,
    }

    private static readonly HashSet<string> ColumnasEnteras = new(StringComparer.Ordinal)
    {
        "comperActivo", "comperVersionMaterialIdentidad", "commenVersionMenu", "commenEscala", "commenImporteEmpleadoUnidadesMenores",
        "commccUnidades", "comregLimite", "comregPrioridad",
    };

    /// <summary>Un GUID termina en «Codigo», un instante lleva «Fecha», una hora es de una franja; los enteros, por su nombre.</summary>
    private static TipoColumna Tipo(string columna) =>
        columna.EndsWith("Codigo", StringComparison.Ordinal) ? TipoColumna.Guid
        : columna.Contains("Fecha", StringComparison.Ordinal) ? TipoColumna.Instante
        : columna.StartsWith("comfraHora", StringComparison.Ordinal) ? TipoColumna.Hora
        : ColumnasEnteras.Contains(columna) ? TipoColumna.Entero
        : TipoColumna.Texto;

    /// <summary>
    /// Ensaya la recuperación en la transacción de quien llama, sin commit (D2 y D3): INWeb como dueño; la forma, los tipos y
    /// los valores de un guardado a mano; la versión nueva insertada y activada, con sus claves foráneas, sus CHECK y sus
    /// únicos; ningún código de otra persona en cualquier versión (RB25), y la vista que la pantalla lee. Un rechazo deja la
    /// transacción para deshacer; una falla de la base que no es de una restricción sigue de largo.
    /// </summary>
    private static RevisionConfiguracion Ensayar(
        SqliteConnection conexion, SqliteTransaction transaccion, DateTimeOffset ahora, string configuracion, out int numero)
    {
        numero = 0;
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        if (LeerVista(conexion, transaccion, puesto, ahora).SoloLectura)
        {
            return new RevisionConfiguracion(INWebAdministra, 0, 0, 0);
        }

        if (LeerTablas(configuracion) is not { } tablas || !ValoresDeUnGuardado(tablas))
        {
            return new RevisionConfiguracion(ResguardoInvalido, 0, 0, 0);
        }

        var contexto = new Contexto(conexion, transaccion, puesto, ahora);
        var cambios = new Cambios(Guid.NewGuid()) { Padron = true, Politicas = true, Catalogo = true };
        try
        {
            numero = InsertarVersion(contexto, cambios.Version);
            foreach (var (tabla, columnas) in TablasDeConfiguracion)
            {
                InsertarFilas(contexto, cambios.Version, tabla, columnas, tablas[tabla]);
            }

            ActivarVersion(contexto, cambios, numero);
            if (CodigoDeOtraPersona(conexion, transaccion, cambios.Version))
            {
                return new RevisionConfiguracion(CodigoEnConflicto, 0, 0, 0);
            }

            LeerVista(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), ahora);
        }
        catch (SqliteException error) when (error.SqliteErrorCode == SqliteRestriccion)
        {
            return new RevisionConfiguracion(ResguardoInvalido, 0, 0, 0);
        }
        catch (Exception error) when (error is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            return new RevisionConfiguracion(ResguardoInvalido, 0, 0, 0);
        }

        return new RevisionConfiguracion(null, Personas(tablas), tablas["tbComedorOpcionesMenu"].Count, Permisos(tablas, ahora));
    }

    /// <summary>SQLITE_CONSTRAINT: una clave foránea, un CHECK, un único o un NOT NULL que el resguardo no cumple.</summary>
    private const int SqliteRestriccion = 19;

    /// <summary>
    /// Un código de la versión nueva que alguna versión de esta base le da a otra persona (RB25): una sola pasada por las
    /// credenciales, contra el conjunto de códigos de la versión nueva.
    /// </summary>
    private static bool CodigoDeOtraPersona(SqliteConnection conexion, SqliteTransaction transaccion, Guid version) =>
        ComandosSqlite.EscalarLong(conexion, transaccion,
            """
            SELECT COUNT(*) FROM (
                SELECT comcreValorNormalizado FROM tbComedorCredencialesPersonas
                WHERE comcreTipo = 'SERIAL_CODE' AND comcreValorNormalizado IN (
                    SELECT comcreValorNormalizado FROM tbComedorCredencialesPersonas WHERE comvcCodigo = $vc AND comcreTipo = 'SERIAL_CODE')
                GROUP BY comcreValorNormalizado
                HAVING COUNT(DISTINCT comperCodigo) > 1)
            """,
            ("$vc", version)) > 0;

    /// <summary>Las personas activas que trae el resguardo.</summary>
    private static int Personas(Dictionary<string, List<object[]>> tablas)
    {
        var activo = Indice("tbComedorPersonas", "comperActivo");
        return tablas["tbComedorPersonas"].Count(p => p[activo] is 1L);
    }

    /// <summary>Los permisos de operador del resguardo vigentes ahora: sin revocar, ya empezados y sin vencer.</summary>
    private static int Permisos(Dictionary<string, List<object[]>> tablas, DateTimeOffset ahora)
    {
        var (rol, desde, hasta, revocacion) = (
            Indice("tbComedorPermisosOperadores", "compopRol"), Indice("tbComedorPermisosOperadores", "compopFechaVigenciaDesdeUtc"),
            Indice("tbComedorPermisosOperadores", "compopFechaVigenciaHastaUtc"), Indice("tbComedorPermisosOperadores", "compopFechaRevocacionUtc"));
        return tablas["tbComedorPermisosOperadores"].Count(p => p[rol] is "OPERATOR" && p[revocacion] is DBNull
            && (p[desde] is not string d || FormatoInstante.Leer(d) <= ahora)
            && (p[hasta] is not string h || FormatoInstante.Leer(h) > ahora));
    }

    /// <summary>
    /// Los valores con las reglas de un guardado a mano (D2): los nombres de personas y opciones, los códigos, el importe, las
    /// unidades de cada carga y el límite de cada regla. Un resguardo no es un atajo para lo que la pantalla no deja guardar.
    /// </summary>
    private static bool ValoresDeUnGuardado(Dictionary<string, List<object[]>> tablas)
    {
        bool Todas(string tabla, string columna, Func<object, bool> valida)
        {
            var indice = Indice(tabla, columna);
            return tablas[tabla].All(f => valida(f[indice]));
        }

        var (tipo, valor) = (Indice("tbComedorCredencialesPersonas", "comcreTipo"), Indice("tbComedorCredencialesPersonas", "comcreValorNormalizado"));
        return Todas("tbComedorPersonas", "comperNombreVisible", n => ValidarTexto(n as string, LargoNombrePersona) is null)
            && Todas("tbComedorPersonas", "comperActivo", a => a is 0L or 1L)
            && tablas["tbComedorCredencialesPersonas"].All(c => c[tipo] is not "SERIAL_CODE" || ValidarCodigo(c[valor] as string) is null)
            && Todas("tbComedorOpcionesMenu", "commenNombre", n => ValidarTexto(n as string, LargoNombreOpcion) is null)
            && Todas("tbComedorOpcionesMenu", "commenImporteEmpleadoUnidadesMenores", i => i is long importe && importe is >= 0 and <= ImporteMaximo)
            && Todas("tbComedorOpcionesMenu_CategoriasCupo", "commccUnidades", u => u is long unidades && unidades is >= 1 and <= UnidadesMaximas)
            && Todas("tbComedorReglasHabilitacion", "comregLimite", l => l is long limite && limite is >= 0 and <= LimiteMaximo);
    }

    private static int Indice(string tabla, string columna) =>
        Array.IndexOf(TablasDeConfiguracion.First(t => t.Tabla == tabla).Columnas, columna);

    /// <summary>
    /// Las filas de cada tabla de configuración, con sus valores en el orden de sus columnas y con el tipo de cada una; nulo si
    /// el JSON no es un objeto con exactamente las diez tablas, cada una una lista de objetos con exactamente sus columnas (D2).
    /// </summary>
    private static Dictionary<string, List<object[]>>? LeerTablas(string configuracion)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(configuracion, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException)
        {
            return null;
        }

        using (documento)
        {
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var tablas = new Dictionary<string, List<object[]>>(StringComparer.Ordinal);
            foreach (var tabla in documento.RootElement.EnumerateObject())
            {
                var columnas = TablasDeConfiguracion.FirstOrDefault(t => t.Tabla == tabla.Name).Columnas;
                if (columnas is null || tablas.ContainsKey(tabla.Name) || tabla.Value.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var filas = new List<object[]>();
                foreach (var fila in tabla.Value.EnumerateArray())
                {
                    if (LeerFila(fila, columnas) is not { } valores)
                    {
                        return null;
                    }

                    filas.Add(valores);
                }

                tablas.Add(tabla.Name, filas);
            }

            return tablas.Count == TablasDeConfiguracion.Length ? tablas : null;
        }
    }

    /// <summary>
    /// Una fila con exactamente sus columnas, cada valor del tipo de la suya: un GUID, un instante y una hora se leen y se
    /// vuelven a escribir como los escribe el puesto, un entero sólo como número y un texto sólo como texto; un nulo, en
    /// cualquiera (los NOT NULL los controla la base). Así un código no entra como número ni un importe como texto.
    /// </summary>
    private static object[]? LeerFila(JsonElement fila, string[] columnas)
    {
        if (fila.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var valores = new object[columnas.Length];
        var leidas = new bool[columnas.Length];
        foreach (var celda in fila.EnumerateObject())
        {
            var indice = Array.IndexOf(columnas, celda.Name);
            if (indice < 0 || leidas[indice] || Valor(celda.Value, Tipo(celda.Name)) is not { } valor)
            {
                return null;
            }

            valores[indice] = valor;
            leidas[indice] = true;
        }

        return leidas.All(l => l) ? valores : null;
    }

    private static object? Valor(JsonElement celda, TipoColumna tipo)
    {
        if (celda.ValueKind == JsonValueKind.Null)
        {
            return DBNull.Value;
        }

        var texto = celda.ValueKind == JsonValueKind.String ? celda.GetString() : null;
        return tipo switch
        {
            TipoColumna.Entero => celda.ValueKind == JsonValueKind.Number && celda.TryGetInt64(out var entero) ? entero : null,
            TipoColumna.Texto => texto,
            TipoColumna.Guid => Guid.TryParse(texto, out var guid) ? guid : null,
            TipoColumna.Instante => DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instante)
                ? FormatoInstante.Texto(instante)
                : null,
            _ => TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora) ? Hora(hora) : null,
        };
    }

    private static void ActivarVersion(Contexto c, Cambios cambios, int numero)
    {
        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            """
            UPDATE tbComedorVersionesConfiguracion
            SET comvcVersionPadron = comvcVersionPadron + $padron,
                comvcVersionPoliticas = comvcVersionPoliticas + $politicas,
                comvcVersionCatalogo = comvcVersionCatalogo + $catalogo,
                comvcHashContenido = $hash
            WHERE comvcCodigo = $nueva
            """,
            ("$padron", cambios.Padron ? 1 : 0), ("$politicas", cambios.Politicas ? 1 : 0), ("$catalogo", cambios.Catalogo ? 1 : 0),
            ("$hash", HashDeContenido(c, cambios.Version)), ("$nueva", cambios.Version));
        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            "UPDATE tbComedorVersionesConfiguracion SET comvcEstado = 'SUPERSEDED' WHERE comvcCodigo = $activa",
            ("$activa", c.Puesto.SnapshotId));
        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            "UPDATE tbComedorConfiguracionPuestos SET comvcCodigoActiva = $nueva WHERE compueCodigo = $pue",
            ("$nueva", cambios.Version), ("$pue", c.Puesto.Id));
        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) VALUES ($nueva, 'LOCAL')",
            ("$nueva", cambios.Version));
    }

    /// <summary>SHA-256 del contenido de la versión, tabla por tabla en un orden fijo y en JSON canónico (D14).</summary>
    private static byte[] HashDeContenido(Contexto c, Guid version)
    {
        using var flujo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(flujo))
        {
            EscribirTablas(escritor, c.Conexion, c.Transaccion, version);
        }

        return SHA256.HashData(JsonCanonico.Canonicalizar(flujo.ToArray()));
    }

    /// <summary>
    /// El contenido de una versión, tabla por tabla en un orden fijo, cada fila ordenada por sus columnas: lo que mide el hash
    /// (D14) y lo que lleva el resguardo (D1 de resguardar-configuracion).
    /// </summary>
    private static void EscribirTablas(Utf8JsonWriter escritor, SqliteConnection conexion, SqliteTransaction transaccion, Guid version)
    {
        escritor.WriteStartObject();
        foreach (var (tabla, columnas) in TablasDeConfiguracion)
        {
            escritor.WriteStartArray(tabla);
            var lista = string.Join(", ", columnas);
            using var comando = ComandosSqlite.Crear(conexion, transaccion,
                $"SELECT {lista} FROM {tabla} WHERE comvcCodigo = $vc ORDER BY {lista}", ("$vc", version));
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                escritor.WriteStartObject();
                for (var i = 0; i < columnas.Length; i++)
                {
                    escritor.WritePropertyName(columnas[i]);
                    switch (lector.GetValue(i))
                    {
                        case DBNull:
                            escritor.WriteNullValue();
                            break;
                        case long entero:
                            escritor.WriteNumberValue(entero);
                            break;
                        case var valor:
                            escritor.WriteStringValue(Convert.ToString(valor, CultureInfo.InvariantCulture));
                            break;
                    }
                }

                escritor.WriteEndObject();
            }

            escritor.WriteEndArray();
        }

        escritor.WriteEndObject();
    }

    // ── Personas (D8 y D9) ───────────────────────────────────────────────────────────────────────────────────

    private static void AplicarPersonas(Contexto c, VistaAdministracion vista, IReadOnlyList<EdicionPersona> ediciones, Cambios cambios)
    {
        var ambito = Texto(c.Conexion, c.Transaccion,
            "SELECT comacCodigo FROM tbComedorAmbitosCupo WHERE comvcCodigo = $vc ORDER BY comacCodigo LIMIT 1", ("$vc", cambios.Version)) is { } texto
            ? Guid.Parse(texto)
            : throw new InvalidOperationException("La versión activa no tiene ningún ámbito de cupo.");
        var servicios = vista.Servicios.ToDictionary(s => s.Id, s => s.Nombre);
        var categorias = vista.Categorias.ToDictionary(x => x.Id, x => x.Nombre);

        foreach (var edicion in ediciones)
        {
            if (edicion.PersonaId is not { } id)
            {
                id = Guid.NewGuid();
                var credencial = Guid.NewGuid();
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    """
                    INSERT INTO tbComedorPersonas (comvcCodigo, comperCodigo, comperNombreVisible, comperActivo, comperVersionMaterialIdentidad)
                    VALUES ($vc, $per, $nombre, 1, 1)
                    """,
                    ("$vc", cambios.Version), ("$per", id), ("$nombre", edicion.Nombre));
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    """
                    INSERT INTO tbComedorCredencialesPersonas (comvcCodigo, comcreCodigo, comperCodigo, comcreTipo, comcreValorNormalizado,
                        comcreFechaVigenciaDesdeUtc, comcreFechaVigenciaHastaUtc, comcreFechaRevocacionUtc)
                    VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)
                    """,
                    ("$vc", cambios.Version), ("$cre", credencial), ("$per", id), ("$valor", edicion.Codigo));
                foreach (var cupo in edicion.Cupos)
                {
                    InsertarRegla(c, cambios.Version, id, ambito, cupo);
                }

                if (edicion.Operador == true)
                {
                    CambiarPermiso(c, cambios.Version, id, dar: true);
                }

                cambios.Padron = true;
                cambios.Politicas |= edicion.Cupos.Count > 0 || edicion.Operador == true;
                var despuesAlta = PersonaRegistrada(edicion.Nombre, true, edicion.Operador == true,
                    edicion.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias);
                cambios.Registrar(RegistroPersona(id, null, despuesAlta, credencial,
                    CuposCambiados([], edicion.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias)));
                continue;
            }

            var actual = vista.Personas.Single(p => p.Id == id);
            var cambio = false;
            Guid? revocada = null;

            if (edicion.Nombre != actual.Nombre)
            {
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    "UPDATE tbComedorPersonas SET comperNombreVisible = $nombre WHERE comvcCodigo = $vc AND comperCodigo = $per",
                    ("$nombre", edicion.Nombre), ("$vc", cambios.Version), ("$per", id));
                cambios.Padron = cambio = true;
            }

            var activa = actual.Activa;
            if (edicion.Quitar)
            {
                // La baja no borra nada: la persona queda inactiva y su código revocado y ocupado (D8, RB25).
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    "UPDATE tbComedorPersonas SET comperActivo = 0 WHERE comvcCodigo = $vc AND comperCodigo = $per",
                    ("$vc", cambios.Version), ("$per", id));
                revocada = Texto(c.Conexion, c.Transaccion,
                        "SELECT comcreCodigo FROM tbComedorCredencialesPersonas WHERE comvcCodigo = $vc AND comperCodigo = $per AND comcreFechaRevocacionUtc IS NULL ORDER BY comcreCodigo LIMIT 1",
                        ("$vc", cambios.Version), ("$per", id)) is { } credencial
                    ? Guid.Parse(credencial)
                    : null;
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    "UPDATE tbComedorCredencialesPersonas SET comcreFechaRevocacionUtc = $ahora WHERE comvcCodigo = $vc AND comperCodigo = $per AND comcreFechaRevocacionUtc IS NULL",
                    ("$ahora", FormatoInstante.Texto(c.Ahora)), ("$vc", cambios.Version), ("$per", id));
                cambio |= actual.Activa || revocada is not null;
                cambios.Padron |= actual.Activa || revocada is not null;
                activa = false;
            }

            // Un par cuyo límite no cambió conserva sus reglas; uno editado queda con una sola regla y uno que falta, sin ninguna (D9).
            var editados = edicion.Cupos.ToDictionary(x => (x.ServicioId, x.CategoriaId), x => x.Limite);
            foreach (var cupo in actual.Cupos)
            {
                var par = (cupo.ServicioId, cupo.CategoriaId);
                if (editados.TryGetValue(par, out var limite) && limite == cupo.Limite)
                {
                    continue;
                }

                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    "DELETE FROM tbComedorReglasHabilitacion WHERE comvcCodigo = $vc AND comperCodigo = $per AND comsrvCodigo = $srv AND comcatCodigo = $cat",
                    ("$vc", cambios.Version), ("$per", id), ("$srv", cupo.ServicioId), ("$cat", cupo.CategoriaId));
                if (editados.ContainsKey(par))
                {
                    InsertarRegla(c, cambios.Version, id, ambito, new CupoEditado(cupo.ServicioId, cupo.CategoriaId, limite));
                }

                cambios.Politicas = cambio = true;
            }

            foreach (var cupo in edicion.Cupos.Where(x => !actual.Cupos.Any(a => a.ServicioId == x.ServicioId && a.CategoriaId == x.CategoriaId)))
            {
                InsertarRegla(c, cambios.Version, id, ambito, cupo);
                cambios.Politicas = cambio = true;
            }

            // Dar o quitar el permiso sobre la fila que la versión nueva ya copió (D1 de marcar-operadores-en-personas).
            var operador = actual.Operador;
            if (edicion.Operador is { } quiere && quiere != actual.Operador)
            {
                CambiarPermiso(c, cambios.Version, id, quiere);
                operador = quiere;
                cambios.Politicas = cambio = true;
            }

            if (cambio)
            {
                var antes = PersonaRegistrada(actual.Nombre, actual.Activa, actual.Operador,
                    actual.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias);
                var despues = PersonaRegistrada(edicion.Nombre, activa, operador,
                    edicion.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias);
                cambios.Registrar(RegistroPersona(id, antes, despues, revocada,
                    CuposCambiados(actual.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)),
                        edicion.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias)));
            }
        }
    }

    private static void InsertarRegla(Contexto c, Guid version, Guid persona, Guid ambito, CupoEditado cupo) =>
        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            """
            INSERT INTO tbComedorReglasHabilitacion (comvcCodigo, comregCodigo, comperCodigo, comgrpCodigo, comsrvCodigo, comacCodigo,
                comcatCodigo, comregLimite, comregPrioridad, comregFechaVigenciaDesdeUtc, comregFechaVigenciaHastaUtc)
            VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, $limite, 1, NULL, NULL)
            """,
            ("$vc", version), ("$reg", Guid.NewGuid()), ("$per", persona), ("$srv", cupo.ServicioId), ("$amb", ambito),
            ("$cat", cupo.CategoriaId), ("$limite", cupo.Limite));

    /// <summary>
    /// Da el permiso OPERATOR desde ahora y sin fin, sobre la fila de la versión nueva si ya la tiene (revocada o vencida) o con
    /// una nueva; o lo revoca en este instante, sin borrar la fila (D1 de marcar-operadores-en-personas, RB26).
    /// </summary>
    private static void CambiarPermiso(Contexto c, Guid version, Guid persona, bool dar)
    {
        var ahora = FormatoInstante.Texto(c.Ahora);
        if (!dar)
        {
            ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                """
                UPDATE tbComedorPermisosOperadores SET compopFechaRevocacionUtc = $ahora
                WHERE comvcCodigo = $vc AND comperCodigo = $per AND compopRol = 'OPERATOR' AND compopFechaRevocacionUtc IS NULL
                """,
                ("$ahora", ahora), ("$vc", version), ("$per", persona));
            return;
        }

        if (Texto(c.Conexion, c.Transaccion,
                "SELECT compopCodigo FROM tbComedorPermisosOperadores WHERE comvcCodigo = $vc AND comperCodigo = $per AND compopRol = 'OPERATOR' LIMIT 1",
                ("$vc", version), ("$per", persona)) is { } fila)
        {
            ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                """
                UPDATE tbComedorPermisosOperadores
                SET compopFechaVigenciaDesdeUtc = $ahora, compopFechaVigenciaHastaUtc = NULL, compopFechaRevocacionUtc = NULL
                WHERE comvcCodigo = $vc AND compopCodigo = $pop
                """,
                ("$ahora", ahora), ("$vc", version), ("$pop", fila));
            return;
        }

        ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
            """
            INSERT INTO tbComedorPermisosOperadores (comvcCodigo, compopCodigo, comperCodigo, compopRol, compopFechaVigenciaDesdeUtc,
                compopFechaVigenciaHastaUtc, compopFechaRevocacionUtc)
            VALUES ($vc, $pop, $per, 'OPERATOR', $ahora, NULL, NULL)
            """,
            ("$vc", version), ("$pop", Guid.NewGuid()), ("$per", persona), ("$ahora", ahora));
    }

    private static object PersonaRegistrada(
        string nombre, bool activa, bool operador, IEnumerable<(Guid Servicio, Guid Categoria, int Limite)> cupos,
        Dictionary<Guid, string> servicios, Dictionary<Guid, string> categorias) => new
    {
        Name = nombre,
        Active = activa,
        Operator = operador,
        Quotas = cupos.Select(x => new
        {
            ServiceId = x.Servicio,
            Service = servicios[x.Servicio],
            CategoryId = x.Categoria,
            Category = categorias[x.Categoria],
            Limit = x.Limite,
        }).ToList(),
    };

    /// <summary>
    /// Los cupos cuyo límite cambió, con el antes y el después (nulo si no había cupo o se quitó): qué cambió, para no tener
    /// que comparar la persona entera de antes y de después (D3 de editar-raciones-en-personas).
    /// </summary>
    private static List<object> CuposCambiados(
        IEnumerable<(Guid Servicio, Guid Categoria, int Limite)> antes, IEnumerable<(Guid Servicio, Guid Categoria, int Limite)> despues,
        Dictionary<Guid, string> servicios, Dictionary<Guid, string> categorias)
    {
        var previos = antes.ToDictionary(x => (x.Servicio, x.Categoria), x => (int?)x.Limite);
        var nuevos = despues.ToDictionary(x => (x.Servicio, x.Categoria), x => (int?)x.Limite);
        return previos.Keys.Union(nuevos.Keys)
            .Select(par => (Par: par, Antes: previos.GetValueOrDefault(par), Despues: nuevos.GetValueOrDefault(par)))
            .Where(x => x.Antes != x.Despues)
            .OrderBy(x => servicios[x.Par.Servicio], OrdenPorNombre).ThenBy(x => categorias[x.Par.Categoria], OrdenPorNombre)
            .Select(x => (object)new
            {
                ServiceId = x.Par.Servicio,
                Service = servicios[x.Par.Servicio],
                CategoryId = x.Par.Categoria,
                Category = categorias[x.Par.Categoria],
                Before = x.Antes,
                After = x.Despues,
            })
            .ToList();
    }

    /// <summary>El registro de una persona cambiada: la marca y el identificador de la credencial, nunca su valor (D10).</summary>
    private static Func<int, int, object> RegistroPersona(Guid persona, object? antes, object despues, Guid? credencial, List<object> cuposCambiados) =>
        (versionAntes, versionDespues) => new
        {
            Setting = "PERSON",
            PersonId = persona,
            Before = antes,
            After = despues,
            QuotaChanges = cuposCambiados,
            CredentialChanged = credencial is not null,
            CredentialId = credencial,
            Result = "APPLIED",
            ConfigurationVersionBefore = versionAntes,
            ConfigurationVersionAfter = versionDespues,
            ActorPersonId = (Guid?)null,
            ActorReason = SinAutenticacion,
        };

    // ── Opciones y franjas (D7) ──────────────────────────────────────────────────────────────────────────────

    private static void AplicarOpciones(Contexto c, VistaAdministracion vista, IReadOnlyList<EdicionOpcion> ediciones, Cambios cambios)
    {
        var servicios = vista.Servicios.ToDictionary(s => s.Id, s => s.Nombre);
        var categorias = vista.Categorias.ToDictionary(x => x.Id, x => x.Nombre);
        var moneda = vista.Opciones.FirstOrDefault()?.Moneda ?? "ARS";
        var escala = vista.Opciones.FirstOrDefault()?.Escala ?? 2;

        foreach (var edicion in ediciones)
        {
            var franjas = edicion.Franjas
                .Select(f => (Inicio: LecturaDecision.LeerHora(f.Inicio), Fin: LecturaDecision.LeerHora(f.Fin)))
                .Distinct().OrderBy(f => f.Inicio).ThenBy(f => f.Fin).ToList();

            if (edicion.OpcionId is not { } id)
            {
                id = Guid.NewGuid();
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    """
                    INSERT INTO tbComedorOpcionesMenu (comvcCodigo, commenCodigo, commenVersionMenu, comsrvCodigo, commenNombre, commenMoneda,
                        commenEscala, commenImporteEmpleadoUnidadesMenores)
                    VALUES ($vc, $men, 1, $srv, $nombre, $moneda, $escala, $importe)
                    """,
                    ("$vc", cambios.Version), ("$men", id), ("$srv", edicion.ServicioId), ("$nombre", edicion.Nombre),
                    ("$moneda", moneda), ("$escala", escala), ("$importe", edicion.ImporteUnidadesMenores));
                InsertarCargas(c, cambios.Version, id, edicion.Cargas);
                InsertarFranjas(c, cambios.Version, id, franjas);
                cambios.Catalogo = true;
                var nueva = OpcionRegistrada(edicion.Nombre, edicion.ServicioId, moneda, escala, edicion.ImporteUnidadesMenores, 1,
                    edicion.Cargas.Select(x => (x.CategoriaId, x.Unidades)), franjas, servicios, categorias);
                cambios.Registrar(RegistroOpcion(id, null, nueva));
                continue;
            }

            var actual = vista.Opciones.Single(o => o.Id == id);
            var antes = OpcionRegistrada(actual.Nombre, actual.ServicioId, actual.Moneda, actual.Escala, actual.ImporteUnidadesMenores, actual.Version,
                actual.Cargas.Select(x => (x.CategoriaId, x.Unidades)), actual.Franjas.Select(f => (f.Inicio, f.Fin)).ToList(), servicios, categorias);

            if (edicion.Quitar)
            {
                // Sale de la versión nueva; sus consumos apuntan a la versión en que se registraron (D8).
                foreach (var tabla in new[] { "tbComedorFranjasOpcionesMenu", "tbComedorOpcionesMenu_CategoriasCupo", "tbComedorOpcionesMenu" })
                {
                    ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                        $"DELETE FROM {tabla} WHERE comvcCodigo = $vc AND commenCodigo = $men", ("$vc", cambios.Version), ("$men", id));
                }

                cambios.Catalogo = true;
                cambios.Registrar(RegistroOpcion(id, antes, null));
                continue;
            }

            var cargasActuales = actual.Cargas.Select(x => (x.CategoriaId, x.Unidades)).OrderBy(x => x.CategoriaId).ToList();
            var cargasNuevas = edicion.Cargas.Select(x => (x.CategoriaId, x.Unidades)).OrderBy(x => x.CategoriaId).ToList();
            var cambiaVersion = edicion.Nombre != actual.Nombre || edicion.ServicioId != actual.ServicioId
                || edicion.ImporteUnidadesMenores != actual.ImporteUnidadesMenores || !cargasActuales.SequenceEqual(cargasNuevas);
            var cambianFranjas = !actual.Franjas.Select(f => (f.Inicio, f.Fin)).SequenceEqual(franjas);
            var version = actual.Version;

            if (cambiaVersion)
            {
                // Lo que la persona vio y RB10 manda reconfirmar sube la versión de la opción; las franjas no (D7).
                version++;
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    """
                    UPDATE tbComedorOpcionesMenu
                    SET commenVersionMenu = $version, comsrvCodigo = $srv, commenNombre = $nombre, commenImporteEmpleadoUnidadesMenores = $importe
                    WHERE comvcCodigo = $vc AND commenCodigo = $men
                    """,
                    ("$version", version), ("$srv", edicion.ServicioId), ("$nombre", edicion.Nombre),
                    ("$importe", edicion.ImporteUnidadesMenores), ("$vc", cambios.Version), ("$men", id));
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    "DELETE FROM tbComedorOpcionesMenu_CategoriasCupo WHERE comvcCodigo = $vc AND commenCodigo = $men",
                    ("$vc", cambios.Version), ("$men", id));
                InsertarCargas(c, cambios.Version, id, edicion.Cargas);
            }

            if (cambianFranjas)
            {
                ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                    "DELETE FROM tbComedorFranjasOpcionesMenu WHERE comvcCodigo = $vc AND commenCodigo = $men",
                    ("$vc", cambios.Version), ("$men", id));
                InsertarFranjas(c, cambios.Version, id, franjas);
            }

            if (cambiaVersion || cambianFranjas)
            {
                cambios.Catalogo = true;
                var despues = OpcionRegistrada(edicion.Nombre, edicion.ServicioId, actual.Moneda, actual.Escala, edicion.ImporteUnidadesMenores,
                    version, edicion.Cargas.Select(x => (x.CategoriaId, x.Unidades)), franjas, servicios, categorias);
                cambios.Registrar(RegistroOpcion(id, antes, despues));
            }
        }
    }

    private static void InsertarCargas(Contexto c, Guid version, Guid opcion, IEnumerable<CargaEditada> cargas)
    {
        foreach (var carga in cargas)
        {
            ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                "INSERT INTO tbComedorOpcionesMenu_CategoriasCupo (comvcCodigo, commenCodigo, comcatCodigo, commccUnidades) VALUES ($vc, $men, $cat, $unidades)",
                ("$vc", version), ("$men", opcion), ("$cat", carga.CategoriaId), ("$unidades", carga.Unidades));
        }
    }

    private static void InsertarFranjas(Contexto c, Guid version, Guid opcion, IEnumerable<(TimeOnly Inicio, TimeOnly Fin)> franjas)
    {
        foreach (var (inicio, fin) in franjas)
        {
            ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                """
                INSERT INTO tbComedorFranjasOpcionesMenu (comvcCodigo, commenCodigo, comfraCodigo, comfraHoraInicio, comfraHoraFin)
                VALUES ($vc, $men, $fra, $inicio, $fin)
                """,
                ("$vc", version), ("$men", opcion), ("$fra", Guid.NewGuid()), ("$inicio", Hora(inicio)), ("$fin", Hora(fin)));
        }
    }

    private static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static object OpcionRegistrada(
        string nombre, Guid servicio, string moneda, int escala, long importe, int version,
        IEnumerable<(Guid Categoria, int Unidades)> cargas, IReadOnlyList<(TimeOnly Inicio, TimeOnly Fin)> franjas,
        Dictionary<Guid, string> servicios, Dictionary<Guid, string> categorias) => new
    {
        Name = nombre,
        ServiceId = servicio,
        Service = servicios[servicio],
        Currency = moneda,
        Scale = escala,
        EmployeeChargeMinor = importe,
        MenuVersion = version,
        Charges = cargas.Select(x => new { CategoryId = x.Categoria, Category = categorias[x.Categoria], Units = x.Unidades }).ToList(),
        ServiceHours = franjas.Select(f => new { Start = Hora(f.Inicio), End = Hora(f.Fin) }).ToList(),
    };

    private static Func<int, int, object> RegistroOpcion(Guid opcion, object? antes, object? despues) =>
        (versionAntes, versionDespues) => new
        {
            Setting = "MENU_OPTION",
            MenuItemId = opcion,
            Before = antes,
            After = despues,
            Result = "APPLIED",
            ConfigurationVersionBefore = versionAntes,
            ConfigurationVersionAfter = versionDespues,
            ActorPersonId = (Guid?)null,
            ActorReason = SinAutenticacion,
        };

    // ── Bitácora de lo que no cambió nada (D13) ──────────────────────────────────────────────────────────────

    private void RegistrarSinCambio(Contexto c, string ajuste, string resultado, string motivo, IReadOnlyList<CampoRechazado> campos, Guid? actor) =>
        _bitacora.Insertar(c.Conexion, c.Transaccion, c.Puesto, c.Ahora, "CONFIGURATION_CHANGED", ConActor(new
        {
            Setting = ajuste,
            Result = resultado,
            ReasonCode = motivo,
            Fields = campos.Select(x => new { Index = x.Indice, Field = x.Campo, Code = x.Codigo }).ToList(),
            ConfigurationVersionBefore = c.Puesto.VersionConfiguracion,
            ConfigurationVersionAfter = c.Puesto.VersionConfiguracion,
            ActorPersonId = (Guid?)null,
            ActorReason = SinAutenticacion,
        }, actor), SeveridadBitacora.Warning);

    /// <summary>
    /// Con un operador, su persona en ActorPersonId y sin ActorReason, en el mismo lugar del registro; sin actor, el registro
    /// queda igual, con el nulo y NO_AUTHENTICATION (D7 de entrar-a-configuracion-con-teclado).
    /// </summary>
    private static object ConActor(object registro, Guid? actor)
    {
        if (actor is not { } persona)
        {
            return registro;
        }

        var nodo = JsonSerializer.SerializeToNode(registro)!.AsObject();
        nodo["ActorPersonId"] = persona;
        nodo.Remove("ActorReason");
        return nodo;
    }

    // ── Ayudantes ────────────────────────────────────────────────────────────────────────────────────────────

    private static List<T> Leer<T>(SqliteConnection conexion, SqliteTransaction transaccion, string sql, (string, object?)[] parametros, Func<SqliteDataReader, T> leer)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion, sql, parametros);
        using var lector = comando.ExecuteReader();
        var filas = new List<T>();
        while (lector.Read())
        {
            filas.Add(leer(lector));
        }

        return filas;
    }

    private static string? Texto(SqliteConnection conexion, SqliteTransaction transaccion, string sql, params (string, object?)[] parametros)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion, sql, parametros);
        return comando.ExecuteScalar() as string;
    }

    private sealed record ReglaLeida(Guid Persona, Guid Servicio, Guid Categoria, int Limite, int Prioridad, DateTimeOffset? Desde, DateTimeOffset? Hasta);

    private sealed record Contexto(SqliteConnection Conexion, SqliteTransaction Transaccion, PuestoLocal Puesto, DateTimeOffset Ahora);

    /// <summary>La versión nueva, qué partes de la configuración cambió (D14) y los registros que deja (D13).</summary>
    private sealed class Cambios(Guid version)
    {
        public Guid Version { get; } = version;

        public bool Padron { get; set; }

        public bool Politicas { get; set; }

        public bool Catalogo { get; set; }

        public List<Func<int, int, object>> Registros { get; } = [];

        public void Registrar(Func<int, int, object> registro) => Registros.Add(registro);
    }
}
