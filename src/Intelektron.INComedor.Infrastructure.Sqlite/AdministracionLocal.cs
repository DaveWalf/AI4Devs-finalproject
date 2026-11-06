using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
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

    /// <summary>La versión activa como la muestran los paneles, con su origen.</summary>
    public VistaAdministracion Leer()
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = conexion.BeginTransaction(deferred: true);
        var vista = LeerVista(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), _reloj.GetUtcNow());
        transaccion.Commit();
        return vista;
    }

    public ResultadoGuardado GuardarPersonas(IReadOnlyList<EdicionPersona> ediciones) =>
        Guardar("PERSON", (c, vista) => ValidarPersonas(c, vista, ediciones), (c, vista, cambios) => AplicarPersonas(c, vista, ediciones, cambios));

    public ResultadoGuardado GuardarOpciones(IReadOnlyList<EdicionOpcion> ediciones) =>
        Guardar("MENU_OPTION", (_, vista) => ValidarOpciones(vista, ediciones), (c, vista, cambios) => AplicarOpciones(c, vista, ediciones, cambios));

    private ResultadoGuardado Guardar(
        string ajuste,
        Func<Contexto, VistaAdministracion, List<CampoRechazado>> validar,
        Action<Contexto, VistaAdministracion, Cambios> aplicar)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        var contexto = new Contexto(conexion, transaccion, puesto, ahora);
        var vista = LeerVista(conexion, transaccion, puesto, ahora);

        if (vista.SoloLectura)
        {
            RegistrarSinCambio(contexto, ajuste, "DENIED", INWebAdministra, []);
            transaccion.Commit();
            return new ResultadoGuardado(TipoGuardado.Denegado, vista.VersionConfiguracion, INWebAdministra, []);
        }

        var campos = validar(contexto, vista);
        if (campos.Count > 0)
        {
            var motivo = campos.All(c => c.Codigo == CodigoDuplicado) ? CodigoDuplicado : CampoInvalido;
            RegistrarSinCambio(contexto, ajuste, "REJECTED", motivo, campos);
            transaccion.Commit();
            return new ResultadoGuardado(TipoGuardado.Rechazado, vista.VersionConfiguracion, motivo, campos);
        }

        var cambios = new Cambios(Guid.NewGuid());
        var numero = CrearVersion(contexto, cambios.Version);
        aplicar(contexto, vista, cambios);
        ActivarVersion(contexto, cambios, numero);

        foreach (var registro in cambios.Registros)
        {
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "CONFIGURATION_CHANGED", registro(vista.VersionConfiguracion, numero));
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

        var personas = Leer(conexion, transaccion,
                "SELECT comperCodigo, comperNombreVisible, comperActivo FROM tbComedorPersonas WHERE comvcCodigo = $vc", [("$vc", vc)],
                l => (Id: l.GetGuid(0), Nombre: l.GetString(1), Activa: l.GetInt64(2) == 1))
            .Select(p =>
            {
                var cupos = reglas[p.Id]
                    .GroupBy(r => (r.Servicio, r.Categoria))
                    .Select(g => CupoDe(g.ToList(), nombreServicio, nombreCategoria, ahora))
                    .OrderBy(c => c.Servicio, OrdenPorNombre).ThenBy(c => c.Categoria, OrdenPorNombre)
                    .ToList();
                var habilitadas = opciones
                    .Where(o => o.Cargas.All(carga => cupos.Any(c => c.ServicioId == o.ServicioId && c.CategoriaId == carga.CategoriaId && c.Limite > 0)))
                    .Select(o => o.Id)
                    .ToList();
                return new PersonaAdministrada(p.Id, p.Nombre, p.Activa, cupos, habilitadas);
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

    private static List<CampoRechazado> ValidarPersonas(Contexto contexto, VistaAdministracion vista, IReadOnlyList<EdicionPersona> ediciones)
    {
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

        foreach (var (tabla, columnas) in TablasDeConfiguracion)
        {
            var lista = string.Join(", ", columnas);
            ComandosSqlite.Ejecutar(c.Conexion, c.Transaccion,
                $"INSERT INTO {tabla} (comvcCodigo, {lista}) SELECT $nueva, {lista} FROM {tabla} WHERE comvcCodigo = $activa",
                ("$nueva", nueva), ("$activa", c.Puesto.SnapshotId));
        }

        return numero;
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
            escritor.WriteStartObject();
            foreach (var (tabla, columnas) in TablasDeConfiguracion)
            {
                escritor.WriteStartArray(tabla);
                var lista = string.Join(", ", columnas);
                using var comando = ComandosSqlite.Crear(c.Conexion, c.Transaccion,
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

        return SHA256.HashData(JsonCanonico.Canonicalizar(flujo.ToArray()));
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

                cambios.Padron = true;
                cambios.Politicas |= edicion.Cupos.Count > 0;
                var despuesAlta = PersonaRegistrada(edicion.Nombre, true, edicion.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias);
                cambios.Registrar(RegistroPersona(id, null, despuesAlta, credencial));
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

            if (cambio)
            {
                var antes = PersonaRegistrada(actual.Nombre, actual.Activa, actual.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias);
                var despues = PersonaRegistrada(edicion.Nombre, activa, edicion.Cupos.Select(x => (x.ServicioId, x.CategoriaId, x.Limite)), servicios, categorias);
                cambios.Registrar(RegistroPersona(id, antes, despues, revocada));
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

    private static object PersonaRegistrada(
        string nombre, bool activa, IEnumerable<(Guid Servicio, Guid Categoria, int Limite)> cupos,
        Dictionary<Guid, string> servicios, Dictionary<Guid, string> categorias) => new
    {
        Name = nombre,
        Active = activa,
        Quotas = cupos.Select(x => new
        {
            ServiceId = x.Servicio,
            Service = servicios[x.Servicio],
            CategoryId = x.Categoria,
            Category = categorias[x.Categoria],
            Limit = x.Limite,
        }).ToList(),
    };

    /// <summary>El registro de una persona cambiada: la marca y el identificador de la credencial, nunca su valor (D10).</summary>
    private static Func<int, int, object> RegistroPersona(Guid persona, object? antes, object despues, Guid? credencial) =>
        (versionAntes, versionDespues) => new
        {
            Setting = "PERSON",
            PersonId = persona,
            Before = antes,
            After = despues,
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

    private void RegistrarSinCambio(Contexto c, string ajuste, string resultado, string motivo, IReadOnlyList<CampoRechazado> campos) =>
        _bitacora.Insertar(c.Conexion, c.Transaccion, c.Puesto, c.Ahora, "CONFIGURATION_CHANGED", new
        {
            Setting = ajuste,
            Result = resultado,
            ReasonCode = motivo,
            Fields = campos.Select(x => new { Index = x.Indice, Field = x.Campo, Code = x.Codigo }).ToList(),
            ConfigurationVersionBefore = c.Puesto.VersionConfiguracion,
            ConfigurationVersionAfter = c.Puesto.VersionConfiguracion,
            ActorPersonId = (Guid?)null,
            ActorReason = SinAutenticacion,
        }, SeveridadBitacora.Warning);

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
