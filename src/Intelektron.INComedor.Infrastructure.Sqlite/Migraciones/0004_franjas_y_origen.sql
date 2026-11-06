-- Migración 0004: franjas horarias de cada opción y origen de cada versión de configuración
-- (D2 y D3 de administrar-personas-y-opciones). Ninguna tabla existente cambia.
-- Representación: la de la 0001 (UUID como TEXT); las horas, como TEXT HH:mm de la zona del puesto (D6).
-- Una franja contiene la hora t si inicio < fin e inicio <= t < fin, o si inicio > fin y (t >= inicio o t < fin):
-- cruza la medianoche. Una opción sin franjas está disponible todo el día.
-- Una versión sin fila de origen es local (LOCAL); INWEB bloquea la edición en el puesto.

CREATE TABLE tbComedorFranjasOpcionesMenu (
    comvcCodigo      TEXT NOT NULL,
    commenCodigo     TEXT NOT NULL,
    comfraCodigo     TEXT NOT NULL,
    comfraHoraInicio TEXT NOT NULL,
    comfraHoraFin    TEXT NOT NULL,
    CONSTRAINT PK_tbComedorFranjasOpcionesMenu PRIMARY KEY (comvcCodigo, comfraCodigo),
    CONSTRAINT FK_tbComedorFranjasOpcionesMenu_tbComedorOpcionesMenu
        FOREIGN KEY (comvcCodigo, commenCodigo) REFERENCES tbComedorOpcionesMenu (comvcCodigo, commenCodigo),
    CONSTRAINT CK_tbComedorFranjasOpcionesMenu_Horas CHECK (
        comfraHoraInicio GLOB '[0-2][0-9]:[0-5][0-9]' AND comfraHoraInicio < '24:00'
        AND comfraHoraFin GLOB '[0-2][0-9]:[0-5][0-9]' AND comfraHoraFin < '24:00'),
    CONSTRAINT CK_tbComedorFranjasOpcionesMenu_NoVacia CHECK (comfraHoraInicio <> comfraHoraFin)
);

CREATE INDEX IX_tbComedorFranjasOpcionesMenu_Opcion ON tbComedorFranjasOpcionesMenu (comvcCodigo, commenCodigo);

CREATE TABLE tbComedorOrigenesVersiones (
    comvcCodigo TEXT NOT NULL,
    comvoOrigen TEXT NOT NULL,
    CONSTRAINT PK_tbComedorOrigenesVersiones PRIMARY KEY (comvcCodigo),
    CONSTRAINT FK_tbComedorOrigenesVersiones_tbComedorVersionesConfiguracion
        FOREIGN KEY (comvcCodigo) REFERENCES tbComedorVersionesConfiguracion (comvcCodigo),
    CONSTRAINT CK_tbComedorOrigenesVersiones_Origen CHECK (comvoOrigen IN ('LOCAL', 'INWEB'))
);
