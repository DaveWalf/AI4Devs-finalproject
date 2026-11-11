using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intelektron.INComedor.Contracts.Eventos;
using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;
using static Intelektron.INComedor.Infrastructure.Sqlite.LecturaDecision;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Confirmación atómica de una intención de consumo (RB06) con replay por IntentId (RB07).</summary>
public sealed class UnidadConfirmacion
{
    private static readonly JsonSerializerOptions OpcionesResultado = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Los datos de impresión sin las propiedades nulas: sin correlativo, no hay TicketNumber.</summary>
    private static readonly JsonSerializerOptions SinNulos = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly IPuntosDeFalla _puntosDeFalla;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.UnidadConfirmacion");

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

    /// <summary>
    /// El «desde» de la numeración correlativa, leído en cada confirmación; null con el código de siempre (D2 de
    /// enriquecer-comprobante).
    /// </summary>
    public Func<long?> CorrelativoDesde { get; init; } = () => null;

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
        var puesto = PuestoLocal.Leer(conexion, transaccion);

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
        var candidatas = LeerReglasCandidatas(conexion, transaccion, puesto, solicitud.PersonId, opcion.ServicioId);
        var evaluacion = EvaluadorDisponibilidad.Evaluar(
            LeerPersonaActiva(conexion, transaccion, puesto, solicitud.PersonId),
            LeerCredencialDeSesion(conexion, transaccion, puesto, solicitud.IdentitySessionId),
            ahora, opcion.ServicioId, opcion.Cargas, candidatas, solicitud.PersonId, fecha,
            LeerUsos(conexion, transaccion, solicitud.PersonId, fecha),
            opcion.Franjas, HoraLocal(puesto, ahora));
        var decision = evaluacion.Decision;

        // 6. Persistencia de la decisión según RB06.
        var resultado = decision.Autorizada
            ? PersistirAutorizacion(conexion, transaccion, solicitud, hash, puesto, opcion, fecha, decision, evaluacion.Reglas, ahora)
            : PersistirRechazo(conexion, transaccion, solicitud, hash, puesto, opcion, decision, fecha, ahora);

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
        PuestoLocal puesto,
        OpcionMenu opcion,
        FechaNegocio fecha,
        DecisionCupo decision,
        IReadOnlyList<ReglaPersona> reglas,
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

        // El nombre visible del comensal y, con la numeración correlativa, el número, en la misma transacción: dos
        // confirmaciones no pueden tomar el mismo, y una copia repite los de su original (D1 y D2 de enriquecer-comprobante).
        string? nombre;
        using (var comandoNombre = ComandosSqlite.Crear(conexion, transaccion,
                   "SELECT comperNombreVisible FROM tbComedorPersonas WHERE comvcCodigo = $vc AND comperCodigo = $per",
                   ("$vc", puesto.SnapshotId), ("$per", solicitud.PersonId)))
        {
            nombre = comandoNombre.ExecuteScalar() as string;
        }

        // El mayor emitido recorre los comprobantes: unos 95 ms cada 100.000, medido el 11/11 con SQLite 3.50; leer sólo
        // el último pediría un índice, una migración y otro esquema (deuda de enriquecer-comprobante).
        long? numero = CorrelativoDesde() is { } desde
            ? Math.Max(desde, ComandosSqlite.EscalarLong(conexion, transaccion,
                "SELECT COALESCE(MAX(json_extract(comcmpDatosImpresion, '$.TicketNumber')), 0) + 1 FROM tbComedorComprobantes"))
            : null;
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
            PersonName = nombre,
            TicketNumber = numero,
        }, SinNulos);
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

        // Lo mostrado se suma a los campos de antes, que no cambian: la opción, su precio y los cargos con su límite (D10 de
        // escribir-bitacoras-externas), para reconstruir la decisión sin consultar la base.
        var nombres = decision.Cargas.Count == 0 ? [] : ConsultaDisponibilidad.NombresDeCategorias(conexion, transaccion, puesto);
        _bitacora.Insertar(conexion, transaccion, puesto, ahora, "CONSUMPTION_AUTHORIZED", new
        {
            solicitud.IntentId,
            DecisionId = decisionId,
            ConsumptionId = consumoId,
            TicketId = comprobanteId,
            solicitud.PersonId,
            BusinessDate = fechaTexto,
            solicitud.IdentitySessionId,
            solicitud.MenuItemId,
            solicitud.MenuVersion,
            MenuLabel = opcion.Nombre,
            Price = precio,
            Charges = decision.Cargas.Select(c => new
            {
                ScopeId = c.Clave.AmbitoCupoId,
                CategoryId = c.Clave.CategoriaCupoId,
                CategoryName = nombres.GetValueOrDefault(c.Clave.CategoriaCupoId),
                Units = c.Unidades,
                Limit = reglas.FirstOrDefault(r => r.AmbitoCupoId == c.Clave.AmbitoCupoId && r.CategoriaCupoId == c.Clave.CategoriaCupoId)?.Limite,
            }).ToList(),
        });

        return resultado;
    }

    private ResultadoConfirmacion PersistirRechazo(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        SolicitudConfirmacion solicitud,
        byte[] hash,
        PuestoLocal puesto,
        OpcionMenu opcion,
        DecisionCupo decision,
        FechaNegocio fecha,
        DateTimeOffset ahora)
    {
        var decisionId = Guid.NewGuid();
        var eventoId = Guid.NewGuid();
        var ahoraTexto = FormatoInstante.Texto(ahora);
        var motivo = CodigoDeMotivo(decision.Motivo!.Value);
        var causa = CausaDeMotivo(decision.Motivo!.Value);
        var resultado = new ResultadoConfirmacion(TipoResultado.Denegada, decisionId, ReasonCode: motivo, ReasonCause: causa);

        var evento = new ConsumptionDeniedEvento(
            eventoId, "CONSUMPTION_DENIED", 1, solicitud.IntentId, decisionId, motivo, ahoraTexto, puesto.VersionConfiguracion);
        var contenidoEvento = JsonSerializer.Serialize(evento);

        InsertarIntencion(conexion, transaccion, solicitud, hash, "DENIED", decisionId, resultado, ahoraTexto);
        InsertarEvento(conexion, transaccion, eventoId, solicitud.IntentId, null, "CONSUMPTION_DENIED", contenidoEvento, HashIntencion.DeTexto(contenidoEvento));
        _bitacora.Insertar(conexion, transaccion, puesto, ahora, "CONSUMPTION_DENIED",
            new
            {
                solicitud.IntentId,
                DecisionId = decisionId,
                solicitud.PersonId,
                ReasonCode = motivo,
                Cause = causa,
                BusinessDate = Texto(fecha),
                solicitud.IdentitySessionId,
                solicitud.MenuItemId,
                solicitud.MenuVersion,
                MenuLabel = opcion.Nombre,
                Price = new PriceSnapshotEvento(opcion.Moneda, opcion.Escala, opcion.ImporteEmpleadoUnidadesMenores),
            });

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

    private static bool SesionUtilizable(
        SqliteConnection conexion, SqliteTransaction transaccion, SolicitudConfirmacion solicitud, PuestoLocal puesto, DateTimeOffset ahora)
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

    private sealed record IntencionGuardada(byte[] Hash, string? ResultPayload);
}
