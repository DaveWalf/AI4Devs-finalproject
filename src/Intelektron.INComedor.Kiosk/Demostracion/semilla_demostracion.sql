-- Semilla de la demostración (D3): un puesto, una persona habilitada, ALMUERZO con una categoría y un ámbito,
-- una opción de menú y una regla de límite 1. Todos los datos son sintéticos y no hay credenciales.
-- Las columnas siguen el orden de la migración 0001, el mismo que usa el fixture de las pruebas del dominio.
-- UUID fijos y legibles; sin letras, así que el texto coincide con el que guarda Microsoft.Data.Sqlite.
--   ...0001 snapshot     ...0002 puesto       ...0003 instalación   ...0004 organización
--   ...0005 ALMUERZO     ...0006 ámbito       ...0007 categoría     ...0008 persona
--   ...0009 opción       ...0010 regla

INSERT INTO tbComedorVersionesConfiguracion VALUES
    ('00000000-0000-4000-8000-000000000001', 1, 1, 1, 1, 3, zeroblob(32), 'ACTIVE',
     '2026-10-01T00:00:00.000Z', '2026-10-01T00:00:00.000Z');

INSERT INTO tbComedorConfiguracionPuestos VALUES
    ('00000000-0000-4000-8000-000000000002', '00000000-0000-4000-8000-000000000003',
     '00000000-0000-4000-8000-000000000004', '00000000-0000-4000-8000-000000000001',
     'America/Argentina/Buenos_Aires', '00:00', 'SINGLE_KIOSK', 'CONTINUE_LOCAL');

INSERT INTO tbComedorTiposServicio VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000005', 'ALMUERZO');

INSERT INTO tbComedorAmbitosCupo VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000006', 'Por día en el puesto');

INSERT INTO tbComedorCategoriasCupo VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000007', 'ALMUERZO');

INSERT INTO tbComedorPersonas VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000008', 'Persona de demostración', 1, 1);

INSERT INTO tbComedorOpcionesMenu VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000009', 1,
     '00000000-0000-4000-8000-000000000005', 'Almuerzo de demostración', 'ARS', 2, 150000);

INSERT INTO tbComedorOpcionesMenu_CategoriasCupo VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000009',
     '00000000-0000-4000-8000-000000000007', 1);

INSERT INTO tbComedorReglasHabilitacion VALUES
    ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000010',
     '00000000-0000-4000-8000-000000000008', NULL, '00000000-0000-4000-8000-000000000005',
     '00000000-0000-4000-8000-000000000006', '00000000-0000-4000-8000-000000000007', 1, 1, NULL, NULL);
