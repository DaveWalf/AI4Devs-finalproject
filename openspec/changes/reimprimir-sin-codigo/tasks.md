# Tasks

Rama local `feature/reimprimir-sin-codigo-dw`, cortada de `origin/final-project-dw` en `7b80d45`. Gate respondido por
David el 11/11/2026 (proposal); entra antes del Typeform del 12/11. Cada grupo cierra con
`git diff --diff-filter=M --name-only 7b80d45 -- tests/` pegado, para ver que sólo cambian las pruebas que David aprobó
en 1.1. Las pruebas de base y receptor corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla
sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes que el cambio exige tocar

- [x] 1.1 David aprueba esta lista antes de que se toque una prueba existente (`AGENTS.md`). Sale de un `grep` de los
  identificadores del pedido (`#codigo-operador`, `#entrar-operador`, `#cancelar-operador`, `#pedido-operador`,
  `PedidoOperador`, `#aviso-comprobantes`, `#salir`) y de los métodos de `SesionOperador` que se van. Verifica: su
  respuesta anotada en esta tarea, y en cada grupo el `git diff --diff-filter=M` sin archivos fuera de la lista.
  - Aprobada por David el 11/11/2026 a las 17:42, textual: «apruebo la lista 1.1, D3 y D4 como están». Una prueba
    existente fuera de esta lista necesita otra aprobación suya.
  - `Kiosk.Tests/AccionesEnConsumosTests.cs`: `Reimprimir_pide_el_codigo_con_la_clave` se reescribe como «Con la clave
    del día no se pide un código»; `Recuperar_por_el_pedido`, como «Recuperar con la clave del día»;
    `Salir_no_cierra_la_sesion_adoptada`, como «Cerrar Configuración termina la sesión», sin «Salir».
    `El_pedido_dice_que_va_a_hacer`, `Un_pedido_que_nadie_atiende`, `Cancelar_despues_de_una_denegacion` y
    `Dejar_la_pestana_cierra_el_pedido` se quitan con el requisito que probaban. `Ver_un_comprobante` y
    `Un_operador_que_entro_con_su_codigo` no cambian: afirman que no hay campo de código.
  - `Kiosk.Tests/PestanaComprobantesTests.cs`: `Operador_con_permiso_vigente` se quita, porque tipeaba en Consumos y la
    entrada la cubre `AccesoOperadorTests.Un_operador_entra_con_su_codigo`. `Treinta_segundos_sin_tocar_la_pestana` y
    `La_sesion_vence_a_los_30_segundos` se quitan, porque no queda plazo de Consumos. `Cerrar_configuracion_termina_la_sesion`
    se reescribe entrando por el engranaje con `9001`; `El_permiso_vence_durante_la_sesion`, con el aviso sin pedido
    y la acción siguiente denegada (D6).
  - `Kiosk.Tests/PantallaAtencionTests.cs`: el auxiliar `EntrarAsistencia` entra por el engranaje con el código en lugar
    de tipearlo en Consumos, y así cambia la entrada de `Aviso_de_incertidumbre_antes_del_motivo`.
    `Persona_sin_permiso_sigue_en_reposo` se quita, porque la cubre `AccesoOperadorTests.El_codigo_de_una_persona_sin_operator`.
    `Salir_de_la_asistencia_vuelve_a_reposo` se quita, porque no hay «Salir».
    `Permiso_que_vence_durante_la_sesion_vuelve_a_reposo` se reescribe con D6.
  - `Kiosk.Tests/PestanaConsumosTests.cs`: `Comprobantes_sigue_pidiendo_un_operador` se reescribe: con la clave del día,
    «Reimprimir» pide el motivo.
  - `Kiosk.Tests/DatosDelComprobanteTests.cs` (`El_pedido_del_motivo_dice_el_correlativo`) y
    `Kiosk.Tests/TextosComprobanteTests.cs` (`Una_copia_lleva_los_textos_vigentes`): cambia sólo cómo llegan al motivo,
    sin código, y lo que afirman sigue igual.
  - `Kiosk.Tests/PuntoFinalTests.cs`: `PedidoOperadorPara` sale de `Los_mensajes_armados_terminan_en_punto` y de la
    clasificación de `Cada_metodo_esta_clasificado`.
  - `E2E.Tests/BuscarYExportarConsumosE2ETests.cs` (`Buscar_ver_y_exportar_a_1024_x_768`): el paso que mide y captura el
    pedido (`consumos-pedido-operador-1024x768`) pasa a medir la sección del motivo con `#aviso-sin-validez`.
  - Sin cambios: `Infrastructure.Sqlite.Tests/AsistenciaTests.cs` y `SalidaAsistenciaTests.cs`, porque los métodos con
    sesión no cambian, y `AccesoOperadorTests.cs`.

## 2. La base: reimprimir y recuperar con la clave del día

- [x] 2.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`AsistenciaConClaveTests`), vistas fallar antes. Primero,
  `ReimprimirConClave` crea la copia siguiente con un `TICKET_REPRINTED` que lleva `ActorPersonId` nulo,
  `NO_AUTHENTICATION`, `Access` `DAILY_KEY`, el motivo, el comprobante y la copia, sin `PersonId` ni `GrantId`, y deja
  consumo, cupo e importe iguales. Después, `RecuperarConClave` reenvía el mismo trabajo, sin copia nueva, con un
  `TICKET_RECOVERED` igual. Bordes: un original que no está en `FAILED` da `PRINT_JOB_NOT_RECOVERABLE` sin escribir, y
  un comprobante inexistente da `TICKET_NOT_FOUND`. Verifica:
  `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter-class *AsistenciaConClave*`, en rojo
  antes y en verde después.
  - Hecho el 11/11: rojo 4 de 4 por `NotImplementedException` de los dos métodos vacíos, antes de implementarlos.
- [x] 2.2 Los dos métodos de D1 en `Asistencia.cs`, con el cuerpo común extraído de `Actuar`. Verifica: 2.1 en verde, y
  `AsistenciaTests` y `SalidaAsistenciaTests` sin cambios y en verde.
  - Hecho: `CuerpoRecuperar` y `CuerpoReimprimir` comunes, `ActuarConClave` sin persona ni permiso. Las tres clases en
    verde, 24 de 24.
- [x] 2.3 Pruebas nuevas de `TrabajosImpresion`, vistas fallar antes: la copia hecha con la clave del día llega con
  `SinValidez` y sin `Habilito`; la del operador, con `Habilito` y sin `SinValidez`; el original recuperado con la clave,
  sin ninguno de los dos (D2 y D3). Verifica: el filtro de la clase, en rojo antes y en verde después.
  - Hecho en `MarcaDeLaCopiaTests` (4 pruebas, con una copia de cada acceso sobre el mismo comprobante): con el campo
    agregado y la consulta sin cambiar, rojo 2 de 4, las dos que exigen la marca; las dos de resguardo ya pasaban.
- [x] 2.4 D2 en `TrabajosImpresion.cs`, con `SinValidez` al final de `ComprobanteAImprimir` y con valor por defecto.
  Verifica: 2.3 en verde y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` entero en verde, con el
  `git diff --diff-filter=M` del encabezado pegado.
  - Hecho: `MarcaDeLaCopiaTests` 4 de 4 y el proyecto entero 306 de 306. `git diff --diff-filter=M --name-only 7b80d45
    -- tests/` sale vacío: ninguna prueba existente cambió en este grupo.

## 3. El papel: la marca en lugar de «Habilitó:»

- [x] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: con `SinValidez`, `ImpresoraSimulada` escribe la línea
  `SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD` donde iría «Habilitó:» y no escribe «Habilitó:»; `ComprobanteEnPantalla` hace
  lo mismo; con un operador, las dos siguen con «Habilitó:». Verifica: el filtro de las clases nuevas, en rojo antes y
  en verde después.
  - Hecho en `MarcaSinValidezTests` (3 pruebas): con los campos agregados y sin escribir la línea, rojo 2 de 3; la del
    operador ya pasaba. La marca es la constante `ComprobanteEnPantalla.MarcaSinValidez` y no un mensaje de
    `MensajesAtencion`, porque no es una oración y `PuntoFinalTests` exige punto a los mensajes.
- [x] 3.2 La línea en `ImpresoraSimulada.cs` y `ComprobanteEnPantalla.cs`, y en `PantallaAtencion.razor` si muestra el
  papel. Verifica: 3.1 en verde.
  - Hecho en los tres: la marca va después de «Copia: N», en lugar de «Habilitó:». `MarcaSinValidezTests` y
    `ComprobanteDelPuestoTests`, 12 de 12; el Kiosk compila con `-warnaserror`. «Ver» no cambia: arma su comprobante
    sin `Habilito` ni marca.

## 4. Consumos sin pedido de código

- [x] 4.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes, una por escenario nuevo del delta: «Con la clave del día
  no se pide un código», «Reimpresión con la clave del día» (el archivo de la impresora simulada tiene la marca y no
  tiene «Habilitó:»), «El aviso de una copia sin validez» (compara `#aviso-sin-validez` con el mensaje entero, con su
  punto final, y afirma que la impresora no recibió nada) y «Recuperar con la clave del día». Verifica: en rojo antes y
  en verde después.
  - Hecho en `ConsumosSinCodigoTests` (4 pruebas): rojo 4 de 4 con el pedido de código todavía en la pestaña; verdes
    después de 4.3.
- [x] 4.2 Las reescrituras y las bajas aprobadas en 1.1, ni una más. Verifica: el `git diff --diff-filter=M` del
  encabezado, igual a la lista de 1.1.
  - Hecho: las reescrituras y bajas de 1.1, más el auxiliar `Consumir` de `PantallaAtencionTests`, que quedó sin uso con
    las dos bajas de ese archivo. `git diff --diff-filter=M --name-only 7b80d45 -- tests/` da los ocho archivos de 1.1,
    con el E2E de 4.4: `BuscarYExportarConsumosE2ETests`, `AccionesEnConsumosTests`, `DatosDelComprobanteTests`,
    `PantallaAtencionTests`, `PestanaComprobantesTests`, `PestanaConsumosTests`, `PuntoFinalTests` y
    `TextosComprobanteTests`.
- [x] 4.3 D5, D6 y D7. En `SesionOperador.cs`: `ConClaveDelDia()`, sin `Pedir`, `TomarPendiente`, `DescartarPendiente`,
  `Entrar` ni `RevisarInactividad`, y la denegación que deja el aviso sin pedido. En `PestanaConsumos.razor`: sin el
  bloque del pedido ni «Salir», y con `#aviso-sin-validez`. En `PantallaAtencion.razor`: `ConClaveDelDia()` al abrir
  con la clave, y sin la revisión de inactividad de Consumos. En `MensajesAtencion.cs`: `CopiaSinValidez`, y se quitan
  `PedidoOperador` y `PedidoOperadorPara`. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde, y
  `git grep -n "codigo-operador" -- src` vacío.
  - Hecho: `SesionOperador` sin reloj ni plazo, con `Habilitada`, `ConClave` y `ConClaveDelDia()`; Reimprimir y
    Recuperar sólo se ofrecen con un acceso que los habilite. Se borraron las reglas de `puesto.css` de
    `.entrada-operador`, que quedaron sin uso. `Kiosk.Tests` 602 de 602 y `git grep -n "codigo-operador" -- src` vacío.
- [x] 4.4 Prolijidad: por CDP, a 1024 × 768, se miden en la sección del motivo con la clave del día los bordes izquierdo
  y derecho de `#aviso-sin-validez` y de `#aviso-incertidumbre`, y el alto, el ancho y el tamaño de letra de los botones
  de motivo y de «Cancelar» (tolerancia de 1 px). La captura `consumos-motivo-sin-validez-1024x768` queda para la
  verificación, en el E2E de 1.1. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class
  *BuscarYExportarConsumos*` en verde, con las medidas pegadas de `registro.txt`.
  - Hecho: la impresora pasa a «Incierta» en su pestaña y una copia deja el último trabajo en `UNKNOWN`, para ver los
    dos avisos juntos. Medidas de `registro.txt` (11/11, 18:11:57): `#aviso-incertidumbre` de 49,0 a 975,0 px y
    `#aviso-sin-validez` de 49,0 a 975,0 px, los dos con letra 16px; los cuatro `.motivo` y `#cancelar-reimpresion`, de
    56,0 px de alto, letra 16px y arriba en 418,0 px, con anchos de 147,4, 135,6, 212,9, 120,0 y 120,0 px (no cambian:
    cada uno el de su texto, D5). Seis corridas en primer plano: cinco en verde y la primera roja sólo por el control de
    `EscenarioE2E.cs:410`, porque `%LOCALAPPDATA%\INComedor\demo\` cambió durante la corrida, una carpeta que esta prueba
    no usa (corría otra sesión en paralelo); las cuatro siguientes dieron la foto igual.

## 5. Documentación, decisión y verificación completa

- [x] 5.1 `readme.md` (la reimpresión en Consumos, hoy en las líneas 324 a 326): sin código, con «Habilitó:» para el
  operador y «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» con la clave del día. Verifica: `git grep -n "pide el código de un
  operador" -- readme.md` vacío.
  - Hecho: el párrafo de la reimpresión, la línea del comprobante y los pasos 5 y 6 de la demostración, que tipeaban
    `9001` y `9002` en Consumos (ahora el paso 6 entra por el engranaje con `9001`). El `git grep` sale vacío.
- [x] 5.2 `docs/decisiones.md`: D-C29, la desviación de RB26 y de HU03-C con la clave del día, decidida por David el
  11/11/2026, que enlaza este change. Verifica: el chequeo de enlaces relativos con su control negativo,
  `enlaces rotos: 0`.
  - Hecho: la fila D-C29, que enlaza `proposal.md` en su ruta activa; al archivar el change, ese enlace pasa a la carpeta
    de `archive/`. El chequeo de D8 sobre `docs/decisiones.md` y `readme.md`: `enlaces rotos: 0`; su control negativo, un
    `.md` con un archivo inexistente, un ancla falsa y una válida, dio `ROTO`, `ANCLA` y `enlaces rotos: 2`.
- [x] 5.3 `docs/guion-video.md`: confirmar que ningún paso muestra el pedido de código en Consumos (el paso 6 entra con
  `9001`, que no cambia) y, si alguno lo muestra, avisarle a David, porque el video es suyo. Verifica: el paso citado.
  - Hecho: `docs/guion-video.md:39`, el paso 6, entra con «El engranaje con el código `9001`» y reimprime y recupera sin
    tipear otro código; ningún paso muestra el pedido. No hay nada que avisarle a David.
- [x] 5.4 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
  - Hecho el 11/11 de 18:14 a 18:19, con `INCOMEDOR_SQLSERVER=INMO-DW-AR` y el E2E en primer plano:
    `dotnet build INComedor.sln -warnaserror`: «Compilación correcta. 0 Advertencia(s) 0 Errores».
    `dotnet test INComedor.sln`: exit 0, «total: 1128 error: 0 correcto: 1128 omitido: 0» (Contracts 23, Domain 91,
    Sync.Api 62, Infrastructure.Sqlite 306, Kiosk 602 y E2E 44): 1122 de `7b80d45`, más 15 nuevas, menos las 9 bajas
    aprobadas en 1.1.
    `openspec validate --all --strict`: «Totals: 20 passed, 0 failed (20 items)».
- [x] 5.5 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (un change desde el pedido textual
  de David, con su gate respondido y las pruebas existentes aprobadas antes de tocarlas) y, si deja un workflow o un
  ajuste humano nuevo, su entrada en `prompts.md`. Verifica: el chequeo de enlaces relativos con su control negativo,
  `enlaces rotos: 0`.
  - Hecho: la fila «No reemplazar la revisión humana por el revisor del agente» de `docs/aplicacion-del-curso.md` suma el
    change, y `prompts.md` suma el punto 17 de «Criterio humano», el gate de David con la desviación de RB26. Sin workflow
    nuevo. El chequeo de enlaces de D8 sobre `docs/aplicacion-del-curso.md`, `prompts.md`, `docs/decisiones.md` y
    `readme.md`: `enlaces rotos: 0` (el control negativo, en 5.2); el del mapa: `filas del mapa: 82 · defectuosas: 0`.
    Los enlaces a `openspec/changes/reimprimir-sin-codigo/` pasan a `archive/` al archivar el change.

## Workflow follow-up

- `/openspec-verify-change reimprimir-sin-codigo` y su salida en la descripción del PR; el archivo del change en el mismo
  PR. Push, PR y merge a `final-project-dw` son de David.
