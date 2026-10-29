using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Confirmación atómica de una intención de consumo (RB06) con replay por IntentId (RB07).</summary>
public sealed class UnidadConfirmacion
{
    // En el primer corte hay una sola generación de bitácora; el cambio de generación tras restaurar
    // una base es de la segunda ola (bitácoras mensuales).
    private static readonly Guid GeneracionBitacora = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions OpcionesResultado = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly IPuntosDeFalla _puntosDeFalla;
    private readonly Guid _arranque = Guid.NewGuid();

    public UnidadConfirmacion(BaseComedor baseComedor, TimeProvider reloj)
        : this(baseComedor, reloj, SinPuntosDeFalla.Instancia)
    {
    }

    internal UnidadConfirmacion(BaseComedor baseComedor, TimeProvider reloj, IPuntosDeFalla puntosDeFalla)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
        _puntosDeFalla = puntosDeFalla;
    }

    public ResultadoConfirmacion Confirmar(SolicitudConfirmacion solicitud)
    {
        var hash = HashIntencion.Calcular(solicitud);

        // 1. Lectura previa, sin transacción y sin escribir.
        using (var lectura = _baseComedor.AbrirConexion())
        {
            var previa = BuscarIntencion(lectura, null, solicitud.IntentId);
            if (previa is not null)
            {
                return Replay(previa, hash);
            }
        }

        _puntosDeFalla.DespuesDeLecturaPrevia();

        // 2. El bloqueo de escritura se toma al empezar; bajo el bloqueo se vuelve a buscar el IntentId.
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);

        var concurrente = BuscarIntencion(conexion, transaccion, solicitud.IntentId);
        if (concurrente is not null)
        {
            return Replay(concurrente, hash);
        }

        // 3. Relectura bajo el bloqueo.
        var ahora = _reloj.GetUtcNow();
        var puesto = LeerPuesto(conexion, transaccion);

        // 4. Validaciones previas a decidir: no escriben nada.
        if (!SesionUtilizable(conexion, transaccion, solicitud, puesto, ahora))
        {
            return new ResultadoConfirmacion(TipoResultado.SesionNoUtilizable);
        }

        var opcion = LeerOpcion(conexion, transaccion, puesto, solicitud.MenuItemId);
        if (opcion is null || opcion.Version != solicitud.MenuVersion)
        {
            return new ResultadoConfirmacion(TipoResultado.CatalogoCambiado);
        }

        // 5. Decide el dominio.
        var fecha = CalculadoraFechaNegocio.Calcular(ahora, puesto.ZonaHoraria.Id, puesto.InicioDiaNegocio);
        var reglas = LeerReglasVigentes(conexion, transaccion, puesto, solicitud.PersonId, opcion.ServicioId, ahora);
        var usos = LeerUsos(conexion, transaccion, solicitud.PersonId, fecha);
        var decision = EvaluadorCupo.Evaluar(solicitud.PersonId, opcion.ServicioId, fecha, opcion.Cargas, reglas, usos);

        // 6. Persistencia de la decisión según RB06.
        var resultado = decision.Autorizada
            ? PersistirAutorizacion(conexion, transaccion, solicitud, hash, puesto, opcion, fecha, decision, ahora)
            : PersistirRechazo(conexion, transaccion, solicitud, hash, puesto, decision, fecha, ahora);

        // 7. Commit y devolución del resultado.
        _puntosDeFalla.AntesDelCommit();
        transaccion.Commit();
        _puntosDeFalla.DespuesDelCommit();
        return resultado;
    }

    private ResultadoConfirmacion PersistirAutorizacion(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        SolicitudConfirmacion solicitud,
        byte[] hash,
        Puesto puesto,
        OpcionMenu opcion,
        FechaNegocio fecha,
        DecisionCupo decision,
        DateTimeOffset ahora)
    {
        var decisionId = Guid.NewGuid();
        var consumoId = Guid.NewGuid();
        var comprobanteId = Guid.NewGuid();
        var eventoId = Guid.NewGuid();
        var fechaTexto = Texto(fecha);
        var ahoraTexto = FormatoInstante.Texto(ahora);
        var resultado = new ResultadoConfirmacion(TipoResultado.Autorizada, decisionId, consumoId, comprobanteId);

        var secuenciaCliente = ComandosSqlite.EscalarLong(conexion, transaccion,
            "SELECT COALESCE(MAX(comconSecuenciaCliente), 0) + 1 FROM tbComedorConsumos");
        var precio = new PriceSnapshotEvento(opcion.Moneda, opcion.Escala, opcion.ImporteEmpleadoUnidadesMenores);

        var evento = new ConsumptionRecordedEvento(
            eventoId, "CONSUMPTION_RECORDED", 1, solicitud.IntentId, decisionId, consumoId, comprobanteId,
            solicitud.PersonId, solicitud.MenuItemId, solicitud.MenuVersion, solicitud.Quantity, fechaTexto,
            decision.Cargas
                .Select(c => new AppliedQuotaChargeEvento(c.Clave.AmbitoCupoId, c.Clave.CategoriaCupoId, c.Unidades, fechaTexto))
                .ToList(),
            ahoraTexto, secuenciaCliente, puesto.VersionConfiguracion, puesto.VersionCatalogo, puesto.VersionPoliticas, precio);
        var contenidoEvento = JsonSerializer.Serialize(evento);
        var hashEvento = HashIntencion.DeTexto(contenidoEvento);

        InsertarIntencion(conexion, transaccion, solicitud, hash, "AUTHORIZED", decisionId, resultado, ahoraTexto);

        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorConsumos (comconCodigo, comicCodigo, comvcCodigo, comperCodigo, commenCodigo, comicmCodigo,
                comconFechaNegocio, comconCantidad, comconVersionMenu, comconVersionPoliticas, comconVersionCatalogo,
                comconSecuenciaCliente, comconFechaConfirmacionLocalUtc, comconPrecioAplicado)
            VALUES ($con, $ic, $vc, $per, $men, NULL, $fecha, $cant, $vmenu, $vpol, $vcat, $sec, $ahora, $precio)
            """,
            ("$con", consumoId), ("$ic", solicitud.IntentId), ("$vc", puesto.SnapshotId), ("$per", solicitud.PersonId),
            ("$men", solicitud.MenuItemId), ("$fecha", fechaTexto), ("$cant", solicitud.Quantity),
            ("$vmenu", solicitud.MenuVersion), ("$vpol", puesto.VersionPoliticas), ("$vcat", puesto.VersionCatalogo),
            ("$sec", secuenciaCliente), ("$ahora", ahoraTexto), ("$precio", JsonSerializer.Serialize(precio)));

        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorConsumosConocidos (comconCodigo, compueCodigoOrigen, comevCodigoOrigen, comperCodigo,
                comccFechaNegocio, comccFechaObservacionUtc, comccOrigen, comccHashContenido)
            VALUES ($con, $pue, $evento, $per, $fecha, $ahora, 'OWN', $hash)
            """,
            ("$con", consumoId), ("$pue", puesto.Id), ("$evento", eventoId), ("$per", solicitud.PersonId),
            ("$fecha", fechaTexto), ("$ahora", ahoraTexto), ("$hash", hashEvento));

        foreach (var carga in decision.Cargas)
        {
            ComandosSqlite.Ejecutar(conexion, transaccion,
                """
                INSERT INTO tbComedorCargosCupoConocidos (comconCodigo, comacCodigo, comcatCodigo, comcccFechaNegocio, comcccUnidades)
                VALUES ($con, $amb, $cat, $fecha, $unidades)
                """,
                ("$con", consumoId), ("$amb", carga.Clave.AmbitoCupoId), ("$cat", carga.Clave.CategoriaCupoId),
                ("$fecha", fechaTexto), ("$unidades", carga.Unidades));

            ComandosSqlite.Ejecutar(conexion, transaccion,
                """
                INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion)
                VALUES ($per, $amb, $cat, $fecha, $unidades, 1)
                ON CONFLICT (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio)
                DO UPDATE SET comucUsado = comucUsado + $unidades, comucVersion = comucVersion + 1
                """,
                ("$per", solicitud.PersonId), ("$amb", carga.Clave.AmbitoCupoId), ("$cat", carga.Clave.CategoriaCupoId),
                ("$fecha", fechaTexto), ("$unidades", carga.Unidades));
        }

        var datosImpresion = JsonSerializer.Serialize(new
        {
            TicketId = comprobanteId,
            ConsumptionId = consumoId,
            MenuItemId = solicitud.MenuItemId,
            MenuLabel = opcion.Nombre,
            BusinessDate = fechaTexto,
            solicitud.Quantity,
            precio.Currency,
            precio.Scale,
            precio.EmployeeChargeMinor,
        });
        ComandosSqlite.Ejecutar(conexion, transaccion,
            "INSERT INTO tbComedorComprobantes (comcmpCodigo, comconCodigo, comcmpDatosImpresion, comcmpFechaCreacionUtc) VALUES ($tk, $con, $datos, $ahora)",
            ("$tk", comprobanteId), ("$con", consumoId), ("$datos", datosImpresion), ("$ahora", ahoraTexto));

        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorTrabajosImpresion (comimpCodigo, comcmpCodigo, comimpNumeroCopia, comimpEstado,
                comimpIdentificadorTrabajoSpooler, comimpCodigoUltimoError, comimpFechaActualizacionUtc)
            VALUES ($job, $tk, 0, 'QUEUED', NULL, NULL, $ahora)
            """,
            ("$job", Guid.NewGuid()), ("$tk", comprobanteId), ("$ahora", ahoraTexto));

        ComandosSqlite.Ejecutar(conexion, transaccion,
            "UPDATE tbComedorSesionesIdentidad SET comicCodigoConsumidora = $ic WHERE comsidCodigo = $sid",
            ("$ic", solicitud.IntentId), ("$sid", solicitud.IdentitySessionId));

        InsertarEvento(conexion, transaccion, eventoId, solicitud.IntentId, comprobanteId, "CONSUMPTION_RECORDED", contenidoEvento, hashEvento);

        InsertarBitacora(conexion, transaccion, puesto, ahora, "CONSUMPTION_AUTHORIZED",
            new { solicitud.IntentId, DecisionId = decisionId, ConsumptionId = consumoId, TicketId = comprobanteId, solicitud.PersonId, BusinessDate = fechaTexto });

        return resultado;
    }

    private ResultadoConfirmacion PersistirRechazo(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        SolicitudConfirmacion solicitud,
        byte[] hash,
        Puesto puesto,
        DecisionCupo decision,
        FechaNegocio fecha,
        DateTimeOffset ahora)
    {
        var decisionId = Guid.NewGuid();
        var eventoId = Guid.NewGuid();
        var ahoraTexto = FormatoInstante.Texto(ahora);
        var motivo = CodigoDeMotivo(decision.Motivo!.Value);
        var resultado = new ResultadoConfirmacion(TipoResultado.Denegada, decisionId, ReasonCode: motivo);

        var evento = new ConsumptionDeniedEvento(
            eventoId, "CONSUMPTION_DENIED", 1, solicitud.IntentId, decisionId, motivo, ahoraTexto, puesto.VersionConfiguracion);
        var contenidoEvento = JsonSerializer.Serialize(evento);

        InsertarIntencion(conexion, transaccion, solicitud, hash, "DENIED", decisionId, resultado, ahoraTexto);
        InsertarEvento(conexion, transaccion, eventoId, solicitud.IntentId, null, "CONSUMPTION_DENIED", contenidoEvento, HashIntencion.DeTexto(contenidoEvento));
        InsertarBitacora(conexion, transaccion, puesto, ahora, "CONSUMPTION_DENIED",
            new { solicitud.IntentId, DecisionId = decisionId, solicitud.PersonId, ReasonCode = motivo, BusinessDate = Texto(fecha) });

        return resultado;
    }

    private static void InsertarIntencion(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        SolicitudConfirmacion solicitud,
        byte[] hash,
        string estado,
        Guid decisionId,
        ResultadoConfirmacion resultado,
        string ahoraTexto)
    {
        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorIntencionesConsumo (comicCodigo, comsidCodigo, comicHashContenido, comicEsquemaHash, comicEstado,
                comicFechaCreacionUtc, comicCodigoDecision, comicFechaConfirmacionUtc, comicContenidoResultado)
            VALUES ($ic, $sid, $hash, $esquema, $estado, $ahora, $decision, $ahora, $resultado)
            """,
            ("$ic", solicitud.IntentId), ("$sid", solicitud.IdentitySessionId), ("$hash", hash),
            ("$esquema", HashIntencion.Esquema), ("$estado", estado), ("$ahora", ahoraTexto),
            ("$decision", decisionId), ("$resultado", JsonSerializer.Serialize(resultado, OpcionesResultado)));
    }

    private static void InsertarEvento(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        Guid eventoId,
        Guid intentId,
        Guid? comprobanteId,
        string tipo,
        string contenido,
        byte[] hash)
    {
        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorEventosSalida (comevCodigo, comicCodigo, comcmpCodigo, comicmCodigo, comevsTipoEvento,
                comevsVersionContenido, comevsContenido, comevsHashContenido, comevsEstado)
            VALUES ($ev, $ic, $tk, NULL, $tipo, 1, $contenido, $hash, 'PENDING')
            """,
            ("$ev", eventoId), ("$ic", intentId), ("$tk", comprobanteId), ("$tipo", tipo),
            ("$contenido", contenido), ("$hash", hash));
    }

    private void InsertarBitacora(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        Puesto puesto,
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
            ("$componente", "Infrastructure.Sqlite.UnidadConfirmacion"), ("$contexto", JsonSerializer.Serialize(contexto)));
    }

    private static IntencionGuardada? BuscarIntencion(SqliteConnection conexion, SqliteTransaction? transaccion, Guid intentId)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comicHashContenido, comicContenidoResultado FROM tbComedorIntencionesConsumo WHERE comicCodigo = $ic",
            ("$ic", intentId));
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            return null;
        }

        return new IntencionGuardada(lector.GetFieldValue<byte[]>(0), lector.IsDBNull(1) ? null : lector.GetString(1));
    }

    private static ResultadoConfirmacion Replay(IntencionGuardada previa, byte[] hash)
    {
        if (!previa.Hash.AsSpan().SequenceEqual(hash))
        {
            return new ResultadoConfirmacion(TipoResultado.Conflicto);
        }

        var contenido = previa.ResultPayload
            ?? throw new InvalidOperationException("La intención guardada no tiene resultado: PENDING no se confirma en este corte.");
        return JsonSerializer.Deserialize<ResultadoConfirmacion>(contenido, OpcionesResultado)!;
    }

    private static Puesto LeerPuesto(SqliteConnection conexion, SqliteTransaction transaccion)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT cp.compueCodigo, cp.comvcCodigoActiva, cp.comcpZonaHoraria, cp.comcpHoraInicioDiaNegocio,
                   vc.comvcVersionConfiguracion, vc.comvcVersionPoliticas, vc.comvcVersionCatalogo
            FROM tbComedorConfiguracionPuestos cp
            JOIN tbComedorVersionesConfiguracion vc ON vc.comvcCodigo = cp.comvcCodigoActiva
            """);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            throw new InvalidOperationException("La base no tiene la configuración del puesto con un snapshot activo.");
        }

        return new Puesto(
            lector.GetGuid(0),
            lector.GetGuid(1),
            TimeZoneInfo.FindSystemTimeZoneById(lector.GetString(2)),
            TimeOnly.ParseExact(lector.GetString(3), "HH:mm", CultureInfo.InvariantCulture),
            lector.GetInt32(4),
            lector.GetInt32(5),
            lector.GetInt32(6));
    }

    private static bool SesionUtilizable(
        SqliteConnection conexion, SqliteTransaction transaccion, SolicitudConfirmacion solicitud, Puesto puesto, DateTimeOffset ahora)
    {
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   SELECT comperCodigo, comsidProposito, comsidFechaVencimientoUtc, comicCodigoConsumidora
                   FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid
                   """,
                   ("$sid", solicitud.IdentitySessionId)))
        using (var lector = comando.ExecuteReader())
        {
            if (!lector.Read())
            {
                return false;
            }

            var vencida = FormatoInstante.Leer(lector.GetString(2)) <= ahora;
            var consumida = !lector.IsDBNull(3);
            if (lector.GetGuid(0) != solicitud.PersonId || lector.GetString(1) != "CONSUMPTION" || vencida || consumida)
            {
                return false;
            }
        }

        return ComandosSqlite.EscalarLong(conexion, transaccion,
            "SELECT COUNT(*) FROM tbComedorPersonas WHERE comvcCodigo = $vc AND comperCodigo = $per",
            ("$vc", puesto.SnapshotId), ("$per", solicitud.PersonId)) == 1;
    }

    private static OpcionMenu? LeerOpcion(SqliteConnection conexion, SqliteTransaction transaccion, Puesto puesto, Guid menuItemId)
    {
        OpcionMenuLeida? leida;
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   SELECT commenVersionMenu, comsrvCodigo, commenNombre, commenMoneda, commenEscala, commenImporteEmpleadoUnidadesMenores
                   FROM tbComedorOpcionesMenu WHERE comvcCodigo = $vc AND commenCodigo = $men
                   """,
                   ("$vc", puesto.SnapshotId), ("$men", menuItemId)))
        using (var lector = comando.ExecuteReader())
        {
            leida = lector.Read()
                ? new OpcionMenuLeida(lector.GetInt32(0), lector.GetGuid(1), lector.GetString(2), lector.GetString(3), lector.GetInt32(4), lector.GetInt64(5))
                : null;
        }

        if (leida is null)
        {
            return null;
        }

        var cargas = new List<CargaMenu>();
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   SELECT comcatCodigo, commccUnidades FROM tbComedorOpcionesMenu_CategoriasCupo
                   WHERE comvcCodigo = $vc AND commenCodigo = $men ORDER BY comcatCodigo
                   """,
                   ("$vc", puesto.SnapshotId), ("$men", menuItemId)))
        using (var lector = comando.ExecuteReader())
        {
            while (lector.Read())
            {
                cargas.Add(new CargaMenu(lector.GetGuid(0), lector.GetInt32(1)));
            }
        }

        if (cargas.Count == 0)
        {
            throw new InvalidOperationException($"La opción de menú {menuItemId} no tiene cargas de cupo.");
        }

        return new OpcionMenu(leida.Version, leida.ServicioId, leida.Nombre, leida.Moneda, leida.Escala, leida.ImporteEmpleadoUnidadesMenores, cargas);
    }

    private static List<ReglaPersona> LeerReglasVigentes(
        SqliteConnection conexion, SqliteTransaction transaccion, Puesto puesto, Guid personaId, Guid servicioId, DateTimeOffset ahora)
    {
        var reglas = new List<ReglaPersona>();
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT comregCodigo, comsrvCodigo, comacCodigo, comcatCodigo, comregLimite,
                   comregFechaVigenciaDesdeUtc, comregFechaVigenciaHastaUtc
            FROM tbComedorReglasHabilitacion
            WHERE comvcCodigo = $vc AND comperCodigo = $per AND comsrvCodigo = $srv
            """,
            ("$vc", puesto.SnapshotId), ("$per", personaId), ("$srv", servicioId));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            var desde = lector.IsDBNull(5) ? (DateTimeOffset?)null : FormatoInstante.Leer(lector.GetString(5));
            var hasta = lector.IsDBNull(6) ? (DateTimeOffset?)null : FormatoInstante.Leer(lector.GetString(6));
            if ((desde is null || desde <= ahora) && (hasta is null || ahora < hasta))
            {
                reglas.Add(new ReglaPersona(lector.GetGuid(0), lector.GetGuid(1), lector.GetGuid(2), lector.GetGuid(3), lector.GetInt32(4)));
            }
        }

        return reglas;
    }

    private static Dictionary<ClaveCupo, int> LeerUsos(
        SqliteConnection conexion, SqliteTransaction transaccion, Guid personaId, FechaNegocio fecha)
    {
        var usos = new Dictionary<ClaveCupo, int>();
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comacCodigo, comcatCodigo, comucUsado FROM tbComedorUsosCupo WHERE comperCodigo = $per AND comucFechaNegocio = $fecha",
            ("$per", personaId), ("$fecha", Texto(fecha)));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            usos[new ClaveCupo(personaId, lector.GetGuid(0), lector.GetGuid(1), fecha)] = lector.GetInt32(2);
        }

        return usos;
    }

    private static string Texto(FechaNegocio fecha) => fecha.Valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string CodigoDeMotivo(MotivoDenegacion motivo) => motivo switch
    {
        MotivoDenegacion.LocalLimitReached => "LOCAL_LIMIT_REACHED",
        MotivoDenegacion.ServiceNotEnabled => "SERVICE_NOT_ENABLED",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    private sealed record IntencionGuardada(byte[] Hash, string? ResultPayload);

    private sealed record Puesto(
        Guid Id,
        Guid SnapshotId,
        TimeZoneInfo ZonaHoraria,
        TimeOnly InicioDiaNegocio,
        int VersionConfiguracion,
        int VersionPoliticas,
        int VersionCatalogo);

    private sealed record OpcionMenuLeida(int Version, Guid ServicioId, string Nombre, string Moneda, int Escala, long ImporteEmpleadoUnidadesMenores);

    private sealed record OpcionMenu(
        int Version,
        Guid ServicioId,
        string Nombre,
        string Moneda,
        int Escala,
        long ImporteEmpleadoUnidadesMenores,
        IReadOnlyList<CargaMenu> Cargas);
}
