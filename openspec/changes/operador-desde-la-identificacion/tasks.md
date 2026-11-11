# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `d6bbb24` (la
mejora 1 aplicada). Gate respondido por David el 11/11/2026 (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only d6bbb24 -- tests/` pegado, que tiene que salir vacío: este change no prevé tocar
pruebas existentes. Las pruebas de base corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla
sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes

- [ ] 1.1 Confirmar que ninguna prueba existente cambia. Incluye las que identifican a `9001` en reposo
  (`MaquinaAtencionTests`, `MaquinaSimulacionTests`, `CartelEleccionTests`) y las que agregó la mejora 1
  (`ConsumosSinCodigoTests`): ninguna mira botones de la atención, y D1 no abre sesiones. Verifica:
  `dotnet test INComedor.sln` en verde después del grupo 3, y el `git diff --diff-filter=M` del encabezado vacío. Si
  alguna prueba existente se pone roja, se para y se le pide a David la aprobación de esa prueba.

## 2. La base: leer el permiso y entrar desde la identificación

- [ ] 2.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`EntradaDesdeIdentificacionTests`), vistas fallar antes:
  - `EsOperador` da sí para `9001`, y no para `0001`, `9002` (vencido), `9003` (revocado) y `9004` (administrador sin
    `OPERATOR`), sin escribir ninguna fila;
  - `EntrarDesdeIdentificacion` con la sesión de consumo de `9001` cierra esa sesión, abre una `PRIVILEGED_ACTION` y
    deja `ASSISTANCE_GRANTED` con `Source` `IDENTIFICATION`;
  - con el permiso vencido entre identificarse y entrar, deniega con `PERMISSION_EXPIRED`, sin sesión nueva, y la de
    consumo sigue usable;
  - con una sesión de consumo vencida, deniega con `SESSION_NOT_USABLE`.

  Verifica: el filtro de la clase, en rojo antes y en verde después.
- [ ] 2.2 D1 y D2 en `Asistencia.cs`. Verifica: 2.1 en verde, y `AsistenciaTests` y `SalidaAsistenciaTests` sin cambios
  y en verde.

## 3. La pantalla y la bitácora

- [ ] 3.1 Pruebas nuevas en `Kiosk.Tests` (`OperadorDesdeLaIdentificacionTests`), vistas fallar antes, una por escenario
  de `atencion-puesto` y la de `acceso-puesto`:
  - un operador con comidas (se usa una versión sintética de prueba con `OPERATOR` y cupo de ALMUERZO);
  - `9001` sin comidas, con «Configuración» junto a «Listo» y Configuración con las tres pestañas;
  - el combo;
  - `0001` y `9004` sin el botón, y `9002` sin el botón;
  - el permiso que cae antes de tocar, con el aviso y la denegación;
  - Configuración sin consumo ni intención, y la sesión de consumo cerrada;
  - `CONFIGURATION_OPENED` con `OPERATOR_IDENTIFICATION` y sin `9001` en las dos bitácoras.

  Verifica: en rojo antes y en verde después.
- [ ] 3.2 D1 a D4 en `MaquinaAtencion.cs`, `PantallaAtencion.razor` y `MensajesAtencion.cs`. Verifica: 3.1 en verde y
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [ ] 3.3 Prolijidad. Por CDP, a 1024 × 768, se miden la selección de un operador con comidas («Configuración» y
  «Cancelar»: borde superior, alto, letra y relleno) y la pantalla sin opciones de `9001` («Configuración» y «Listo»:
  alto, ancho mínimo y letra), con tolerancia de 1 px. Quedan las capturas `operador-seleccion-1024x768` y
  `operador-sin-opciones-1024x768`, en una prueba E2E nueva, `OperadorDesdeLaIdentificacionE2ETests`. Verifica:
  `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *OperadorDesdeLaIdentificacion*`, en primer plano,
  con las medidas pegadas de `registro.txt`.

## 4. Documentación y verificación completa

- [ ] 4.1 `readme.md`: en la tabla de códigos sintéticos, que `9001` en reposo ve «Configuración», y en la demostración,
  los dos caminos del operador. Verifica: `git grep -n "OPERATOR_IDENTIFICATION\|Configuración» junto" -- readme.md`
  con las líneas nuevas.
- [ ] 4.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
- [ ] 4.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con los dos
  caminos y la bitácora que los distingue) y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`.
  Verifica: el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo,
  `enlaces rotos: 0`.

## Workflow follow-up

- `/openspec-verify-change operador-desde-la-identificacion` y su salida en la descripción del PR; el archivo del change
  en el mismo PR. Push, PR y merge a `final-project-dw` son de David.
