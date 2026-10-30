-- Migración 0003: lotes de sincronización del puesto (SyncBatch y SyncBatchItem), con el cuerpo canónico
-- que se envió, para reenviarlo idéntico (D9 de recibir-lotes-idempotente).
-- Nombres físicos: mapa D-C18 (docs/nomenclatura-campos-er.csv, L_SyncBatch y L_SyncBatchItem);
-- comlotContenidoLote es una columna nueva del mapa en este change.
-- Representación: la de la 0001 (UUID como TEXT, JSON como TEXT y hash SHA-256 como BLOB de 32 bytes).
-- Los estados del evento del outbox (comevsEstado) los valida el código: agregar un CHECK a una tabla
-- existente obliga en SQLite a reconstruirla.

CREATE TABLE tbComedorLotesSincronizacion (
    comlotCodigo            TEXT NOT NULL,
    comlotContenidoLote     TEXT NOT NULL,
    comlotHashContenidoLote BLOB NOT NULL,
    comlotEstado            TEXT NOT NULL,
    comlotContenidoAcuse    TEXT NULL,
    CONSTRAINT PK_tbComedorLotesSincronizacion PRIMARY KEY (comlotCodigo),
    CONSTRAINT CK_tbComedorLotesSincronizacion_Estado CHECK (comlotEstado IN ('PENDING', 'COMPLETED'))
);

CREATE TABLE tbComedorLotesSincronizacion_Eventos (
    comlotCodigo TEXT NOT NULL,
    comevCodigo  TEXT NOT NULL,
    CONSTRAINT PK_tbComedorLotesSincronizacion_Eventos PRIMARY KEY (comlotCodigo, comevCodigo),
    CONSTRAINT FK_tbComedorLotesSincronizacion_Eventos_tbComedorLotesSincronizacion
        FOREIGN KEY (comlotCodigo) REFERENCES tbComedorLotesSincronizacion (comlotCodigo),
    CONSTRAINT FK_tbComedorLotesSincronizacion_Eventos_tbComedorEventosSalida
        FOREIGN KEY (comevCodigo) REFERENCES tbComedorEventosSalida (comevCodigo)
);
