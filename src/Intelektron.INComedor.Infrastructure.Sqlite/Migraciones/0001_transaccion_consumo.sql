-- Migración 0001: las 20 tablas que la transacción de confirmación (RB06) escribe o referencia.
-- Nombres físicos: mapa D-C18 (docs/nomenclatura-integracion.md y docs/nomenclatura-campos-er.csv).
-- Representación: UUID y token como TEXT; instantes UTC como TEXT ISO 8601 con Z; fechas de negocio
-- como TEXT yyyy-MM-dd; hashes SHA-256 como BLOB de 32 bytes; JSON como TEXT.
--
-- Columnas sin fila en el mapa CSV (los campos que el modelo dejaba para M0), nombradas según D-C18:
--   comvcFechaActivacionUtc      ConfigurationSnapshot.ActivatedAtUtc
--   comsidFechaEmisionUtc        IdentitySession.IssuedAtUtc
--   comicCodigoConsumidora       IdentitySession.ConsumedByIntentId
--   comicEsquemaHash             ConsumptionIntent.HashSchema
--   comicFechaCreacionUtc        ConsumptionIntent.CreatedAtUtc
--   comccFechaObservacionUtc     ObservedConsumption.ObservedAtUtc
--   comcmpFechaCreacionUtc       Ticket.CreatedAtUtc
--   comimpCodigoUltimoError      PrintJob.LastErrorCode
--   combitZonaHoraria            AuditRecord.TimeZone
--   combitDesplazamientoUtc      AuditRecord.Offset
--   combitSeveridad              AuditRecord.Severity
--   combitComponente             AuditRecord.Component
--   combitVersionEsquema         AuditRecord.SchemaVersion
--
-- Referencias sin clave foránea porque su tabla no está en esta migración (segunda ola, D-C24):
--   comgrpCodigo (grupos) y comicmCodigo (incidencias de comunicación).
-- Una FK con alguna columna NULL no se comprueba (MATCH SIMPLE): así funcionan las FK opcionales.

CREATE TABLE tbComedorVersionesConfiguracion (
    comvcCodigo               TEXT    NOT NULL,
    comvcVersionConfiguracion INTEGER NOT NULL,
    comvcVersionPoliticas     INTEGER NOT NULL,
    comvcVersionCatalogo      INTEGER NOT NULL,
    comvcVersionPadron        INTEGER NOT NULL,
    comvcVersionEsquema       INTEGER NOT NULL,
    comvcHashContenido        BLOB    NOT NULL,
    comvcEstado               TEXT    NOT NULL,
    comvcFechaPublicacionUtc  TEXT    NOT NULL,
    comvcFechaActivacionUtc   TEXT    NOT NULL,
    CONSTRAINT PK_tbComedorVersionesConfiguracion PRIMARY KEY (comvcCodigo),
    CONSTRAINT UQ_tbComedorVersionesConfiguracion_VersionConfiguracion UNIQUE (comvcVersionConfiguracion)
);

CREATE TABLE tbComedorConfiguracionPuestos (
    compueCodigo                       TEXT NOT NULL,
    comInstalacionCodigo               TEXT NOT NULL,
    comOrganizacionCodigo              TEXT NOT NULL,
    comvcCodigoActiva                  TEXT NOT NULL,
    comcpZonaHoraria                   TEXT NOT NULL,
    comcpHoraInicioDiaNegocio          TEXT NOT NULL,
    comcpModoOperacion                 TEXT NOT NULL,
    comcpPoliticaSinConexionMultipuesto TEXT NOT NULL,
    CONSTRAINT PK_tbComedorConfiguracionPuestos PRIMARY KEY (compueCodigo),
    CONSTRAINT FK_tbComedorConfiguracionPuestos_tbComedorVersionesConfiguracion_Activa
        FOREIGN KEY (comvcCodigoActiva) REFERENCES tbComedorVersionesConfiguracion (comvcCodigo),
    CONSTRAINT CK_tbComedorConfiguracionPuestos_ModoOperacion
        CHECK (comcpModoOperacion IN ('SINGLE_KIOSK', 'MULTI_KIOSK_AUDITED')),
    CONSTRAINT CK_tbComedorConfiguracionPuestos_PoliticaSinConexion
        CHECK (comcpPoliticaSinConexionMultipuesto IN ('CONTINUE_LOCAL', 'SUSPEND_NEW_CONSUMPTIONS'))
);

CREATE TABLE tbComedorPersonas (
    comvcCodigo                    TEXT    NOT NULL,
    comperCodigo                   TEXT    NOT NULL,
    comperNombreVisible            TEXT    NOT NULL,
    comperActivo                   INTEGER NOT NULL,
    comperVersionMaterialIdentidad INTEGER NOT NULL,
    CONSTRAINT PK_tbComedorPersonas PRIMARY KEY (comvcCodigo, comperCodigo),
    CONSTRAINT FK_tbComedorPersonas_tbComedorVersionesConfiguracion
        FOREIGN KEY (comvcCodigo) REFERENCES tbComedorVersionesConfiguracion (comvcCodigo)
);

CREATE TABLE tbComedorCredencialesPersonas (
    comvcCodigo                 TEXT NOT NULL,
    comcreCodigo                TEXT NOT NULL,
    comperCodigo                TEXT NOT NULL,
    comcreTipo                  TEXT NOT NULL,
    comcreValorNormalizado      TEXT NOT NULL,
    comcreFechaVigenciaDesdeUtc TEXT NULL,
    comcreFechaVigenciaHastaUtc TEXT NULL,
    comcreFechaRevocacionUtc    TEXT NULL,
    CONSTRAINT PK_tbComedorCredencialesPersonas PRIMARY KEY (comvcCodigo, comcreCodigo),
    CONSTRAINT FK_tbComedorCredencialesPersonas_tbComedorPersonas
        FOREIGN KEY (comvcCodigo, comperCodigo) REFERENCES tbComedorPersonas (comvcCodigo, comperCodigo),
    CONSTRAINT UQ_tbComedorCredencialesPersonas_Valor UNIQUE (comvcCodigo, comcreTipo, comcreValorNormalizado),
    CONSTRAINT CK_tbComedorCredencialesPersonas_Tipo CHECK (comcreTipo IN ('SERIAL_CODE'))
);

CREATE TABLE tbComedorTiposServicio (
    comvcCodigo TEXT NOT NULL,
    comsrvCodigo TEXT NOT NULL,
    comsrvNombre TEXT NOT NULL,
    CONSTRAINT PK_tbComedorTiposServicio PRIMARY KEY (comvcCodigo, comsrvCodigo),
    CONSTRAINT FK_tbComedorTiposServicio_tbComedorVersionesConfiguracion
        FOREIGN KEY (comvcCodigo) REFERENCES tbComedorVersionesConfiguracion (comvcCodigo)
);

CREATE TABLE tbComedorAmbitosCupo (
    comvcCodigo     TEXT NOT NULL,
    comacCodigo     TEXT NOT NULL,
    comacDefinicion TEXT NOT NULL,
    CONSTRAINT PK_tbComedorAmbitosCupo PRIMARY KEY (comvcCodigo, comacCodigo),
    CONSTRAINT FK_tbComedorAmbitosCupo_tbComedorVersionesConfiguracion
        FOREIGN KEY (comvcCodigo) REFERENCES tbComedorVersionesConfiguracion (comvcCodigo)
);

CREATE TABLE tbComedorCategoriasCupo (
    comvcCodigo TEXT NOT NULL,
    comcatCodigo TEXT NOT NULL,
    comcatNombre TEXT NOT NULL,
    CONSTRAINT PK_tbComedorCategoriasCupo PRIMARY KEY (comvcCodigo, comcatCodigo),
    CONSTRAINT FK_tbComedorCategoriasCupo_tbComedorVersionesConfiguracion
        FOREIGN KEY (comvcCodigo) REFERENCES tbComedorVersionesConfiguracion (comvcCodigo)
);

-- La unicidad de la versión del menú dentro de su publicación es la propia PK (comvcCodigo, commenCodigo).
CREATE TABLE tbComedorOpcionesMenu (
    comvcCodigo                           TEXT    NOT NULL,
    commenCodigo                          TEXT    NOT NULL,
    commenVersionMenu                     INTEGER NOT NULL,
    comsrvCodigo                          TEXT    NOT NULL,
    commenNombre                          TEXT    NOT NULL,
    commenMoneda                          TEXT    NOT NULL,
    commenEscala                          INTEGER NOT NULL,
    commenImporteEmpleadoUnidadesMenores  INTEGER NOT NULL,
    CONSTRAINT PK_tbComedorOpcionesMenu PRIMARY KEY (comvcCodigo, commenCodigo),
    CONSTRAINT FK_tbComedorOpcionesMenu_tbComedorTiposServicio
        FOREIGN KEY (comvcCodigo, comsrvCodigo) REFERENCES tbComedorTiposServicio (comvcCodigo, comsrvCodigo),
    CONSTRAINT CK_tbComedorOpcionesMenu_ImporteNoNegativo CHECK (commenImporteEmpleadoUnidadesMenores >= 0)
);

CREATE TABLE tbComedorOpcionesMenu_CategoriasCupo (
    comvcCodigo    TEXT    NOT NULL,
    commenCodigo   TEXT    NOT NULL,
    comcatCodigo   TEXT    NOT NULL,
    commccUnidades INTEGER NOT NULL,
    CONSTRAINT PK_tbComedorOpcionesMenu_CategoriasCupo PRIMARY KEY (comvcCodigo, commenCodigo, comcatCodigo),
    CONSTRAINT FK_tbComedorOpcionesMenu_CategoriasCupo_tbComedorOpcionesMenu
        FOREIGN KEY (comvcCodigo, commenCodigo) REFERENCES tbComedorOpcionesMenu (comvcCodigo, commenCodigo),
    CONSTRAINT FK_tbComedorOpcionesMenu_CategoriasCupo_tbComedorCategoriasCupo
        FOREIGN KEY (comvcCodigo, comcatCodigo) REFERENCES tbComedorCategoriasCupo (comvcCodigo, comcatCodigo),
    CONSTRAINT CK_tbComedorOpcionesMenu_CategoriasCupo_Unidades CHECK (commccUnidades > 0)
);

-- Exactamente uno entre persona y grupo (diccionario). comgrpCodigo no tiene FK: los grupos son segunda ola.
CREATE TABLE tbComedorReglasHabilitacion (
    comvcCodigo                 TEXT    NOT NULL,
    comregCodigo                TEXT    NOT NULL,
    comperCodigo                TEXT    NULL,
    comgrpCodigo                TEXT    NULL,
    comsrvCodigo                TEXT    NOT NULL,
    comacCodigo                 TEXT    NOT NULL,
    comcatCodigo                TEXT    NOT NULL,
    comregLimite                INTEGER NOT NULL,
    comregPrioridad             INTEGER NOT NULL,
    comregFechaVigenciaDesdeUtc TEXT    NULL,
    comregFechaVigenciaHastaUtc TEXT    NULL,
    CONSTRAINT PK_tbComedorReglasHabilitacion PRIMARY KEY (comvcCodigo, comregCodigo),
    CONSTRAINT FK_tbComedorReglasHabilitacion_tbComedorPersonas
        FOREIGN KEY (comvcCodigo, comperCodigo) REFERENCES tbComedorPersonas (comvcCodigo, comperCodigo),
    CONSTRAINT FK_tbComedorReglasHabilitacion_tbComedorTiposServicio
        FOREIGN KEY (comvcCodigo, comsrvCodigo) REFERENCES tbComedorTiposServicio (comvcCodigo, comsrvCodigo),
    CONSTRAINT FK_tbComedorReglasHabilitacion_tbComedorAmbitosCupo
        FOREIGN KEY (comvcCodigo, comacCodigo) REFERENCES tbComedorAmbitosCupo (comvcCodigo, comacCodigo),
    CONSTRAINT FK_tbComedorReglasHabilitacion_tbComedorCategoriasCupo
        FOREIGN KEY (comvcCodigo, comcatCodigo) REFERENCES tbComedorCategoriasCupo (comvcCodigo, comcatCodigo),
    CONSTRAINT CK_tbComedorReglasHabilitacion_LimiteNoNegativo CHECK (comregLimite >= 0),
    CONSTRAINT CK_tbComedorReglasHabilitacion_PersonaOGrupo
        CHECK ((comperCodigo IS NULL) <> (comgrpCodigo IS NULL))
);

-- comicCodigoConsumidora no tiene FK a la intención: la intención ya apunta a la sesión y se evita el ciclo.
CREATE TABLE tbComedorSesionesIdentidad (
    comsidCodigo              TEXT NOT NULL,
    comvcCodigo               TEXT NOT NULL,
    comperCodigo              TEXT NOT NULL,
    comcreCodigo              TEXT NULL,
    comsidMetodo              TEXT NOT NULL,
    comsidProposito           TEXT NOT NULL,
    comsidFechaEmisionUtc     TEXT NOT NULL,
    comsidFechaVencimientoUtc TEXT NOT NULL,
    comicCodigoConsumidora    TEXT NULL,
    CONSTRAINT PK_tbComedorSesionesIdentidad PRIMARY KEY (comsidCodigo),
    CONSTRAINT FK_tbComedorSesionesIdentidad_tbComedorPersonas
        FOREIGN KEY (comvcCodigo, comperCodigo) REFERENCES tbComedorPersonas (comvcCodigo, comperCodigo),
    CONSTRAINT FK_tbComedorSesionesIdentidad_tbComedorCredencialesPersonas
        FOREIGN KEY (comvcCodigo, comcreCodigo) REFERENCES tbComedorCredencialesPersonas (comvcCodigo, comcreCodigo),
    CONSTRAINT CK_tbComedorSesionesIdentidad_Proposito
        CHECK (comsidProposito IN ('CONSUMPTION', 'PRIVILEGED_ACTION'))
);

-- AUTHORIZED exige sesión (diccionario).
CREATE TABLE tbComedorIntencionesConsumo (
    comicCodigo               TEXT NOT NULL,
    comsidCodigo              TEXT NULL,
    comicHashContenido        BLOB NOT NULL,
    comicEsquemaHash          TEXT NOT NULL,
    comicEstado               TEXT NOT NULL,
    comicFechaCreacionUtc     TEXT NOT NULL,
    comicCodigoDecision       TEXT NULL,
    comicFechaConfirmacionUtc TEXT NULL,
    comicContenidoResultado   TEXT NULL,
    CONSTRAINT PK_tbComedorIntencionesConsumo PRIMARY KEY (comicCodigo),
    CONSTRAINT FK_tbComedorIntencionesConsumo_tbComedorSesionesIdentidad
        FOREIGN KEY (comsidCodigo) REFERENCES tbComedorSesionesIdentidad (comsidCodigo),
    CONSTRAINT UQ_tbComedorIntencionesConsumo_Decision UNIQUE (comicCodigoDecision),
    CONSTRAINT CK_tbComedorIntencionesConsumo_Estado CHECK (comicEstado IN ('PENDING', 'AUTHORIZED', 'DENIED')),
    CONSTRAINT CK_tbComedorIntencionesConsumo_AutorizadaConSesion
        CHECK (comicEstado <> 'AUTHORIZED' OR comsidCodigo IS NOT NULL)
);

-- comicmCodigo no tiene FK: las incidencias de comunicación son segunda ola.
CREATE TABLE tbComedorConsumos (
    comconCodigo                   TEXT    NOT NULL,
    comicCodigo                    TEXT    NOT NULL,
    comvcCodigo                    TEXT    NOT NULL,
    comperCodigo                   TEXT    NOT NULL,
    commenCodigo                   TEXT    NOT NULL,
    comicmCodigo                   TEXT    NULL,
    comconFechaNegocio             TEXT    NOT NULL,
    comconCantidad                 INTEGER NOT NULL,
    comconVersionMenu              INTEGER NOT NULL,
    comconVersionPoliticas         INTEGER NOT NULL,
    comconVersionCatalogo          INTEGER NOT NULL,
    comconSecuenciaCliente         INTEGER NOT NULL,
    comconFechaConfirmacionLocalUtc TEXT   NOT NULL,
    comconPrecioAplicado           TEXT    NOT NULL,
    CONSTRAINT PK_tbComedorConsumos PRIMARY KEY (comconCodigo),
    CONSTRAINT UQ_tbComedorConsumos_Intencion UNIQUE (comicCodigo),
    CONSTRAINT FK_tbComedorConsumos_tbComedorIntencionesConsumo
        FOREIGN KEY (comicCodigo) REFERENCES tbComedorIntencionesConsumo (comicCodigo),
    CONSTRAINT FK_tbComedorConsumos_tbComedorPersonas
        FOREIGN KEY (comvcCodigo, comperCodigo) REFERENCES tbComedorPersonas (comvcCodigo, comperCodigo),
    CONSTRAINT FK_tbComedorConsumos_tbComedorOpcionesMenu
        FOREIGN KEY (comvcCodigo, commenCodigo) REFERENCES tbComedorOpcionesMenu (comvcCodigo, commenCodigo)
);

CREATE TABLE tbComedorUsosCupo (
    comperCodigo      TEXT    NOT NULL,
    comacCodigo       TEXT    NOT NULL,
    comcatCodigo      TEXT    NOT NULL,
    comucFechaNegocio TEXT    NOT NULL,
    comucUsado        INTEGER NOT NULL,
    comucVersion      INTEGER NOT NULL,
    CONSTRAINT PK_tbComedorUsosCupo PRIMARY KEY (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio),
    CONSTRAINT CK_tbComedorUsosCupo_UsadoNoNegativo CHECK (comucUsado >= 0)
);

-- El ledger de hechos propios y de pares: comconCodigo no tiene FK a tbComedorConsumos porque un consumo
-- de par no existe en esa tabla.
CREATE TABLE tbComedorConsumosConocidos (
    comconCodigo             TEXT NOT NULL,
    compueCodigoOrigen       TEXT NOT NULL,
    comevCodigoOrigen        TEXT NOT NULL,
    comperCodigo             TEXT NOT NULL,
    comccFechaNegocio        TEXT NOT NULL,
    comccFechaObservacionUtc TEXT NOT NULL,
    comccOrigen              TEXT NOT NULL,
    comccHashContenido       BLOB NOT NULL,
    CONSTRAINT PK_tbComedorConsumosConocidos PRIMARY KEY (comconCodigo),
    CONSTRAINT UQ_tbComedorConsumosConocidos_EventoOrigen UNIQUE (comevCodigoOrigen),
    CONSTRAINT CK_tbComedorConsumosConocidos_Origen CHECK (comccOrigen IN ('OWN', 'PEER'))
);

CREATE TABLE tbComedorCargosCupoConocidos (
    comconCodigo      TEXT    NOT NULL,
    comacCodigo       TEXT    NOT NULL,
    comcatCodigo      TEXT    NOT NULL,
    comcccFechaNegocio TEXT   NOT NULL,
    comcccUnidades    INTEGER NOT NULL,
    CONSTRAINT PK_tbComedorCargosCupoConocidos PRIMARY KEY (comconCodigo, comacCodigo, comcatCodigo, comcccFechaNegocio),
    CONSTRAINT FK_tbComedorCargosCupoConocidos_tbComedorConsumosConocidos
        FOREIGN KEY (comconCodigo) REFERENCES tbComedorConsumosConocidos (comconCodigo),
    CONSTRAINT CK_tbComedorCargosCupoConocidos_Unidades CHECK (comcccUnidades > 0)
);

CREATE TABLE tbComedorComprobantes (
    comcmpCodigo           TEXT NOT NULL,
    comconCodigo           TEXT NOT NULL,
    comcmpDatosImpresion   TEXT NOT NULL,
    comcmpFechaCreacionUtc TEXT NOT NULL,
    CONSTRAINT PK_tbComedorComprobantes PRIMARY KEY (comcmpCodigo),
    CONSTRAINT UQ_tbComedorComprobantes_Consumo UNIQUE (comconCodigo),
    CONSTRAINT FK_tbComedorComprobantes_tbComedorConsumos
        FOREIGN KEY (comconCodigo) REFERENCES tbComedorConsumos (comconCodigo)
);

CREATE TABLE tbComedorTrabajosImpresion (
    comimpCodigo                        TEXT    NOT NULL,
    comcmpCodigo                        TEXT    NOT NULL,
    comimpNumeroCopia                   INTEGER NOT NULL,
    comimpEstado                        TEXT    NOT NULL,
    comimpIdentificadorTrabajoSpooler   TEXT    NULL,
    comimpCodigoUltimoError             TEXT    NULL,
    comimpFechaActualizacionUtc         TEXT    NOT NULL,
    CONSTRAINT PK_tbComedorTrabajosImpresion PRIMARY KEY (comimpCodigo),
    CONSTRAINT FK_tbComedorTrabajosImpresion_tbComedorComprobantes
        FOREIGN KEY (comcmpCodigo) REFERENCES tbComedorComprobantes (comcmpCodigo),
    CONSTRAINT UQ_tbComedorTrabajosImpresion_ComprobanteCopia UNIQUE (comcmpCodigo, comimpNumeroCopia)
);

-- comicmCodigo no tiene FK: las incidencias de comunicación son segunda ola.
CREATE TABLE tbComedorEventosSalida (
    comevCodigo           TEXT    NOT NULL,
    comicCodigo           TEXT    NULL,
    comcmpCodigo          TEXT    NULL,
    comicmCodigo          TEXT    NULL,
    comevsTipoEvento      TEXT    NOT NULL,
    comevsVersionContenido INTEGER NOT NULL,
    comevsContenido       TEXT    NOT NULL,
    comevsHashContenido   BLOB    NOT NULL,
    comevsEstado          TEXT    NOT NULL,
    CONSTRAINT PK_tbComedorEventosSalida PRIMARY KEY (comevCodigo),
    CONSTRAINT FK_tbComedorEventosSalida_tbComedorIntencionesConsumo
        FOREIGN KEY (comicCodigo) REFERENCES tbComedorIntencionesConsumo (comicCodigo),
    CONSTRAINT FK_tbComedorEventosSalida_tbComedorComprobantes
        FOREIGN KEY (comcmpCodigo) REFERENCES tbComedorComprobantes (comcmpCodigo),
    CONSTRAINT CK_tbComedorEventosSalida_TipoEvento CHECK (comevsTipoEvento IN (
        'CONSUMPTION_RECORDED', 'CONSUMPTION_DENIED', 'PRINT_EVENT_RECORDED', 'CONNECTIVITY_INCIDENT_RECORDED'))
);

CREATE TABLE tbComedorBitacora (
    combitCodigo            TEXT    NOT NULL,
    compueCodigo            TEXT    NOT NULL,
    combitCodigoGeneracion  TEXT    NOT NULL,
    combitSecuencia         INTEGER NOT NULL,
    combitCodigoArranque    TEXT    NOT NULL,
    combitFechaHechoUtc     TEXT    NOT NULL,
    combitFechaRegistroUtc  TEXT    NOT NULL,
    combitZonaHoraria       TEXT    NOT NULL,
    combitDesplazamientoUtc TEXT    NOT NULL,
    combitMesBitacora       TEXT    NOT NULL,
    combitTipoEvento        TEXT    NOT NULL,
    combitSeveridad         TEXT    NOT NULL,
    combitComponente        TEXT    NOT NULL,
    combitVersionEsquema    INTEGER NOT NULL,
    combitContexto          TEXT    NOT NULL,
    CONSTRAINT PK_tbComedorBitacora PRIMARY KEY (combitCodigo),
    CONSTRAINT UQ_tbComedorBitacora_PuestoGeneracionSecuencia
        UNIQUE (compueCodigo, combitCodigoGeneracion, combitSecuencia)
);
