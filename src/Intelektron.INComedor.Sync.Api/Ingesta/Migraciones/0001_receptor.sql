-- Migración 0001 del receptor: las tablas centrales de la ingesta de lotes (D1 de recibir-lotes-idempotente).
-- Nombres físicos: mapa D-C18 (docs/nomenclatura-campos-er.csv: C_KioskRegistry, C_SyncBatchReceipt, C_InboxEvent,
-- C_ConsolidatedConsumption y C_ReconciliationCase); comcasEvidencia es una columna nueva del mapa en este change.
-- Idempotente: cada tabla se crea sólo si falta, y el script corre en cada arranque con una cadena de conexión.
-- Representación: UUID como UNIQUEIDENTIFIER, hash SHA-256 como BINARY(32), JSON canónico como NVARCHAR(MAX),
-- instantes UTC como DATETIME2(3) y la identidad del certificado como su huella SHA-256 en hexadecimal.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.tbComedorPuestos', N'U') IS NULL
    CREATE TABLE dbo.tbComedorPuestos (
        compueCodigo               UNIQUEIDENTIFIER NOT NULL,
        comInstalacionCodigo       UNIQUEIDENTIFIER NOT NULL,
        comOrganizacionCodigo      UNIQUEIDENTIFIER NOT NULL,
        compueIdentidadCertificado CHAR(64)         NOT NULL,
        CONSTRAINT PK_tbComedorPuestos PRIMARY KEY (compueCodigo),
        CONSTRAINT UQ_tbComedorPuestos_IdentidadCertificado UNIQUE (compueIdentidadCertificado)
    );

IF OBJECT_ID(N'dbo.tbComedorAcusesLotes', N'U') IS NULL
    CREATE TABLE dbo.tbComedorAcusesLotes (
        compueCodigo         UNIQUEIDENTIFIER NOT NULL,
        comlotCodigo         UNIQUEIDENTIFIER NOT NULL,
        comaclHashContenido  BINARY(32)       NOT NULL,
        comaclEstado         VARCHAR(20)      NOT NULL,
        comaclContenidoAcuse NVARCHAR(MAX)    NOT NULL,
        CONSTRAINT PK_tbComedorAcusesLotes PRIMARY KEY (compueCodigo, comlotCodigo),
        CONSTRAINT FK_tbComedorAcusesLotes_tbComedorPuestos FOREIGN KEY (compueCodigo) REFERENCES dbo.tbComedorPuestos (compueCodigo),
        CONSTRAINT CK_tbComedorAcusesLotes_Estado CHECK (comaclEstado IN ('PENDING', 'COMPLETED'))
    );

IF OBJECT_ID(N'dbo.tbComedorEventosEntrada', N'U') IS NULL
    CREATE TABLE dbo.tbComedorEventosEntrada (
        comevCodigo             UNIQUEIDENTIFIER NOT NULL,
        compueCodigo            UNIQUEIDENTIFIER NOT NULL,
        comeveHashContenido     BINARY(32)       NOT NULL,
        comeveContenido         NVARCHAR(MAX)    NOT NULL,
        comeveFechaRecepcionUtc DATETIME2(3)     NOT NULL,
        comeveEstadoProyeccion  VARCHAR(20)      NOT NULL,
        CONSTRAINT PK_tbComedorEventosEntrada PRIMARY KEY (comevCodigo),
        CONSTRAINT FK_tbComedorEventosEntrada_tbComedorPuestos FOREIGN KEY (compueCodigo) REFERENCES dbo.tbComedorPuestos (compueCodigo),
        CONSTRAINT CK_tbComedorEventosEntrada_EstadoProyeccion CHECK (comeveEstadoProyeccion IN ('PROJECTED', 'REJECTED', 'PENDING_REFERENCE', 'IN_REVIEW'))
    );

IF OBJECT_ID(N'dbo.tbComedorConsumosConsolidados', N'U') IS NULL
    CREATE TABLE dbo.tbComedorConsumosConsolidados (
        comconCodigo          UNIQUEIDENTIFIER NOT NULL,
        comevCodigoOrigen     UNIQUEIDENTIFIER NOT NULL,
        compueCodigo          UNIQUEIDENTIFIER NOT NULL,
        comccoConsumoOriginal NVARCHAR(MAX)    NOT NULL,
        CONSTRAINT PK_tbComedorConsumosConsolidados PRIMARY KEY (comconCodigo),
        CONSTRAINT UQ_tbComedorConsumosConsolidados_EventoOrigen UNIQUE (comevCodigoOrigen),
        CONSTRAINT FK_tbComedorConsumosConsolidados_tbComedorEventosEntrada FOREIGN KEY (comevCodigoOrigen) REFERENCES dbo.tbComedorEventosEntrada (comevCodigo)
    );

IF OBJECT_ID(N'dbo.tbComedorCasosConciliacion', N'U') IS NULL
    CREATE TABLE dbo.tbComedorCasosConciliacion (
        comcasCodigo    UNIQUEIDENTIFIER NOT NULL,
        comevCodigo     UNIQUEIDENTIFIER NOT NULL,
        comcasTipo      VARCHAR(40)      NOT NULL,
        comcasEstado    VARCHAR(20)      NOT NULL,
        comcasEvidencia NVARCHAR(MAX)    NOT NULL,
        CONSTRAINT PK_tbComedorCasosConciliacion PRIMARY KEY (comcasCodigo),
        CONSTRAINT FK_tbComedorCasosConciliacion_tbComedorEventosEntrada FOREIGN KEY (comevCodigo) REFERENCES dbo.tbComedorEventosEntrada (comevCodigo),
        CONSTRAINT CK_tbComedorCasosConciliacion_Tipo CHECK (comcasTipo IN ('EVENT_PAYLOAD_CONFLICT', 'CONSUMPTION_ID_CONFLICT')),
        CONSTRAINT CK_tbComedorCasosConciliacion_Estado CHECK (comcasEstado IN ('OPEN', 'RESOLVED'))
    );

COMMIT TRANSACTION;
