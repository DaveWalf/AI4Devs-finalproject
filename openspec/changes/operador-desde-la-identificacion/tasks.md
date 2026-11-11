# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `d6bbb24` (la
mejora 1 aplicada). Gate respondido por David el 11/11/2026 (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only d6bbb24 -- tests/` pegado, que tiene que salir vacío: este change no prevé tocar
pruebas existentes. Las pruebas de base corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla
sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes

- [x] 1.1 Confirmar que ninguna prueba existente cambia. Incluye las que identifican a `9001` en reposo
  (`MaquinaAtencionTests`, `MaquinaSimulacionTests`, `CartelEleccionTests`) y las que agregó la mejora 1
  (`ConsumosSinCodigoTests`): ninguna mira botones de la atención, y D1 no abre sesiones. Verifica:
  `dotnet test INComedor.sln` en verde después del grupo 3, y el `git diff --diff-filter=M` del encabezado vacío. Si
  alguna prueba existente se pone roja, se para y se le pide a David la aprobación de esa prueba.
  Hecho el 11/11/2026, con los proyectos que toca el change: Kiosk.Tests 630 (629 y una roja intermitente de `EnvioDeLotesIntegradoTests`, el receptor sobre SQL Server, que pasó 5 de 5 al repetirla sola y no toca nada de este change) e Infrastructure.Sqlite.Tests 311 de 311; `git diff --diff-filter=M --name-only d6bbb24 -- tests/` vacío. La solución entera se corre en la verificación única del final.

## 2. La base: leer el permiso y entrar desde la identificación

- [x] 2.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`EntradaDesdeIdentificacionTests`), vistas fallar antes:
  - `EsOperador` da sí para `9001`, y no para `0001`, `9002` (vencido), `9003` (revocado) y `9004` (administrador sin
    `OPERATOR`), sin escribir ninguna fila;
  - `EntrarDesdeIdentificacion` con la sesión de consumo de `9001` cierra esa sesión, abre una `PRIVILEGED_ACTION` y
    deja `ASSISTANCE_GRANTED` con `Source` `IDENTIFICATION`;
  - con el permiso vencido entre identificarse y entrar, deniega con `PERMISSION_EXPIRED`, sin sesión nueva, y la de
    consumo sigue usable;
  - con una sesión de consumo vencida, deniega con `SESSION_NOT_USABLE`.
  Hecho: `EntradaDesdeIdentificacionTests`, 5 pruebas (las cuatro de la tarea más «una sesión privilegiada no sirve para entrar»), 5 en rojo antes y 5 en verde después. Los cinco casos se arman en el fixture sintético con los mismos permisos que 9001 a 9004 y 0001.

  Verifica: el filtro de la clase, en rojo antes y en verde después.
- [x] 2.2 D1 y D2 en `Asistencia.cs`. Verifica: 2.1 en verde, y `AsistenciaTests` y `SalidaAsistenciaTests` sin cambios
  y en verde.
  Hecho: `Asistencia.EsOperador` (sólo lee) y `EntrarDesdeIdentificacion` (vuelve a evaluar, cierra la de consumo, abre la PRIVILEGED_ACTION, `Source` `IDENTIFICATION`; una sesión vencida, consumida o que no es de consumo da `SESSION_NOT_USABLE`). 2.1 en verde y `AsistenciaTests` y `SalidaAsistenciaTests` 20 de 20 sin cambios.

## 3. La pantalla y la bitácora

- [x] 3.1 Pruebas nuevas en `Kiosk.Tests` (`OperadorDesdeLaIdentificacionTests`), vistas fallar antes, una por escenario
  de `atencion-puesto` y la de `acceso-puesto`:
  - un operador con comidas (se usa una versión sintética de prueba con `OPERATOR` y cupo de ALMUERZO);
  - `9001` sin comidas, con «Configuración» junto a «Listo» y Configuración con las tres pestañas;
  - el combo;
  - `0001` y `9004` sin el botón, y `9002` sin el botón;
  - el permiso que cae antes de tocar, con el aviso y la denegación;
  - Configuración sin consumo ni intención, y la sesión de consumo cerrada;
  - `CONFIGURATION_OPENED` con `OPERATOR_IDENTIFICATION` y sin `9001` en las dos bitácoras.
  Hecho: `OperadorDesdeLaIdentificacionTests`, 9 pruebas (la de «sin el botón» es una teoría con 0001, 9004 y 9002), 6 en rojo antes y 9 en verde después. El operador con comidas es «Persona sintética 01» con un OPERATOR que la prueba suma por SQL a la versión activa. `ASSISTANCE_GRANTED` y `ASSISTANCE_DENIED` van a la bitácora de interacciones (D3 de `escribir-bitacoras-externas`), así que la prueba los lee ahí, y la denegación queda además en la general como `CONFIGURATION_ACCESS_DENIED` (ajuste al aplicar en D4).

  Verifica: en rojo antes y en verde después.
- [x] 3.2 D1 a D4 en `MaquinaAtencion.cs`, `PantallaAtencion.razor` y `MensajesAtencion.cs`. Verifica: 3.1 en verde y
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
  Hecho: `MaquinaAtencion.EsOperador`, `SesionConsumo` y `TerminarPorConfiguracion` (SESSION_CANCELLED con `Cause` `CONFIGURATION`, o SESSION_CLOSED por `CONFIGURATION` sin opciones); en `PantallaAtencion.razor`, «Configuración» a la izquierda de «Cancelar» y de «Listo», el aviso del motivo (`#aviso-operador`) y la apertura con `OPERATOR_IDENTIFICATION`. No hizo falta un texto nuevo en `MensajesAtencion.cs`: el aviso usa `DeMotivo`. 3.1 en verde y Kiosk.Tests en verde (ver 1.1).
- [x] 3.3 Prolijidad. Por CDP, a 1024 × 768, se miden la selección de un operador con comidas («Configuración» y
  «Cancelar»: borde superior, alto, letra y relleno) y la pantalla sin opciones de `9001` («Configuración» y «Listo»:
  alto, ancho mínimo y letra), con tolerancia de 1 px. Quedan las capturas `operador-seleccion-1024x768` y
  `operador-sin-opciones-1024x768`, en una prueba E2E nueva, `OperadorDesdeLaIdentificacionE2ETests`. Verifica:
  `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *OperadorDesdeLaIdentificacion*`, en primer plano,
  con las medidas pegadas de `registro.txt`.
  Hecho: `OperadorDesdeLaIdentificacionE2ETests` en verde, en primer plano. Medidas de `registro.txt`: en la selección, «Configuración» de x 651,1 a 825,0 y «Cancelar» de x 841,0 a 981,0, los dos arriba en 85,0, 56,0 px de alto, letra 20 px, relleno 24 px y ancho mínimo 140 px; sin opciones, «Configuración» de x 217,0 a 390,9 y «Listo» de x 402,9 a 494,1, los dos arriba en 181,0, 56,0 px, letra 20 px y ancho mínimo `auto`; tocarlo abre `pestana-consumos`, `pestana-personas` y `pestana-impresora`. Capturas `operador-seleccion-1024x768` y `operador-sin-opciones-1024x768`, miradas.

## 4. Documentación y verificación completa

- [x] 4.1 `readme.md`: en la tabla de códigos sintéticos, que `9001` en reposo ve «Configuración», y en la demostración,
  los dos caminos del operador. Verifica: `git grep -n "OPERATOR_IDENTIFICATION\|Configuración» junto" -- readme.md`
  con las líneas nuevas.
  Hecho: `readme.md` suma en la tabla de códigos que 9001 en reposo o en el combo ve «Configuración» junto a «Listo», y en el paso 7 de la demostración los dos caminos con `OPERATOR` y `OPERATOR_IDENTIFICATION`. `git grep` da las líneas 223, 225 y 315.
- [ ] 4.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
  Se cierra con la corrida única del final (§6.6 del plan del goal): pendiente hasta después del quinto change.
- [x] 4.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con los dos
  caminos y la bitácora que los distingue) y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`.
  Verifica: el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo,
  `enlaces rotos: 0`.
  Hecho: la fila «No reemplazar la revisión humana por el revisor del agente» de `docs/aplicacion-del-curso.md` suma los dos caminos y la bitácora que los distingue. No deja un workflow ni un ajuste humano nuevo para `prompts.md`. Chequeo de D8: `enlaces rotos: 0`; control negativo `enlaces rotos: 2`.

## Workflow follow-up

- `/openspec-verify-change operador-desde-la-identificacion` y su salida en la descripción del PR; el archivo del change
  en el mismo PR. Push, PR y merge a `final-project-dw` son de David.
