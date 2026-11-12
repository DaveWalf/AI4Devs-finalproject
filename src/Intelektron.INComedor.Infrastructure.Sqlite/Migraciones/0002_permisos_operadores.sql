-- Migración 0002: permisos de operador (LocalOperatorGrant), que la reimpresión con permiso necesita (RB26).
-- Nombres físicos: mapa D-C18 (docs/nomenclatura-campos-er.csv:44-50).
-- Representación: la de la 0001 (UUID como TEXT e instantes UTC como TEXT ISO 8601 con Z).
-- Los extremos de vigencia ausentes son abiertos, como en las credenciales y las reglas.

CREATE TABLE tbComedorPermisosOperadores (
    comvcCodigo                 TEXT NOT NULL,
    compopCodigo                TEXT NOT NULL,
    comperCodigo                TEXT NOT NULL,
    compopRol                   TEXT NOT NULL,
    compopFechaVigenciaDesdeUtc TEXT NULL,
    compopFechaVigenciaHastaUtc TEXT NULL,
    compopFechaRevocacionUtc    TEXT NULL,
    CONSTRAINT PK_tbComedorPermisosOperadores PRIMARY KEY (comvcCodigo, compopCodigo),
    CONSTRAINT FK_tbComedorPermisosOperadores_tbComedorPersonas
        FOREIGN KEY (comvcCodigo, comperCodigo) REFERENCES tbComedorPersonas (comvcCodigo, comperCodigo),
    CONSTRAINT UQ_tbComedorPermisosOperadores_PersonaRol UNIQUE (comvcCodigo, comperCodigo, compopRol),
    CONSTRAINT CK_tbComedorPermisosOperadores_Rol CHECK (compopRol IN ('OPERATOR', 'SYSTEM_ADMINISTRATOR'))
);
