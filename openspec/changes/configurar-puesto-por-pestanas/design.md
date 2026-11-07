# Design

## Context

Ver `proposal.md` (Why). Lo que el código ya tiene, medido el 06/11/2026 sobre `9202347` (punta de `administrar-personas-y-opciones`, aplicado y sin archivar):

- **Configuración es un diálogo modal de una sola columna** (`PanelConfiguracion.razor:9-87`): los botones de C que abren «Personas habilitadas» y «Opciones del comedor» en lugar del contenido (`:15-38`), «Estilo visual» (`:40-54`), la impresora simulada (`:56-59`), el estado de las bitácoras (`:61-73`) y un pie con «Cancelar» y «Guardar» (`:80-83`). Escape cierra primero el panel abierto y, sin panel, cancela el diálogo (`:157-171`). La pantalla lo abre sólo en reposo (`PantallaAtencion.razor:245-248`, `:279`, `:341-352`) y «Guardar» guarda el estilo y lo cierra (`:358-376`, el cierre en `:374`).
- **Ningún acceso**: el engranaje abre sin clave y los registros de Configuración llevan `ActorPersonId` nulo y `ActorReason` `NO_AUTHENTICATION` (`PantallaAtencion.razor:349`, `:383`, `:387`). `BitacorasDelPuestoTests.SinActor` (`:295-296`) exige exactamente esos dos valores en `CONFIGURATION_OPENED`, `CONFIGURATION_CHANGED` y `CONFIGURATION_CANCELLED`.
- **La ventana** es `WindowState="Maximized"` con la barra de título de Windows y su botón de cierre (`MainWindow.xaml:5`), sin ícono (`Intelektron.INComedor.Kiosk.csproj:2-6`, sin `ApplicationIcon`). Su cierre es el cierre ordenado del puesto: `Closed` libera el servicio, que registra `KIOSK_STOPPED` (`MainWindow.xaml.cs:42`). Dos E2E cierran con `CloseMainWindow()` y esperan ese cierre ordenado (`BitacorasExternasE2ETests.cs:44`, `AdministracionE2ETests.cs:102`). Nada maneja Escape fuera del diálogo (`git grep Escape` sólo da `PanelConfiguracion.razor`).
- **«Asistencia»** es un botón de reposo (`PantallaAtencion.razor:33`) que lleva la máquina al estado `Asistencia` (`MaquinaAtencion.cs:14`, `:340-372`), con su tabla de comprobantes, la reimpresión y la recuperación (`PantallaAtencion.razor:183-236`) y su plazo de 30 s (`MaquinaAtencion.cs:309`, `:319-322`). La lógica de permisos está en `Asistencia` de `Infrastructure.Sqlite` (`Entrar` en `Asistencia.cs:57`, `ComprobantesDelDia` en `:101-133`, `Salir` en `:196-205`, que no registra nada). Cuatro pruebas de `PantallaAtencionTests.cs` usan el botón (`:146-223`, con el ayudante de `:262-267`); `MaquinaAtencionTests` no prueba la asistencia (`git grep Asistencia` no da nada en ese archivo).
- **Los paneles de C**: `PanelPersonas.razor` es una lista de tarjetas (`article.persona`, `:28-117`) con un selector de cupo que ofrece cada servicio por cada categoría (`:168`), y su guardado manda la edición entera en el orden de las filas y señala los campos rechazados por su posición (`:195-234`). `PanelOpciones.razor` edita las opciones con sus franjas. `PanelPersonasTests` busca las tarjetas en 7 de sus 8 pruebas; `PanelOpcionesTests` sólo usa `#abrir-opciones`, `#panel-opciones`, `#guardar-panel`, `#cancelar-panel`, `#aviso-panel`, `#aviso-inweb` y las clases de cada opción.
- **Los consumos**: `tbComedorConsumos` guarda la versión, la persona, la opción, la fecha de negocio, el instante y el precio aplicado en JSON (`0001_transaccion_consumo.sql:209-232`; el precio, `UnidadConfirmacion.cs:146`), y el comprobante con su último trabajo se une como en `Asistencia.ComprobantesDelDia` (`Asistencia.cs:107-121`). La persona y la opción se leen de la versión del consumo, que C conserva.
- **La impresora simulada** escribe un texto con un encabezado fijo (`ImpresoraSimulada.cs:44-50`) y guarda el último enviado (`:66`); la pantalla muestra el comprobante sólo si su `TicketId` es el del consumo (`MaquinaAtencion.cs:498-501`), y su encabezado también es fijo (`PantallaAtencion.razor:162`). Los datos del comprobante se fijan en la transacción de RB06 (`UnidadConfirmacion.cs:180-193`).
- **El receptor**: `ClienteReceptorHttp` habla TLS mutuo con el certificado del puesto y la autoridad (`ClienteReceptorHttp.cs:25-39`), sale de `certificados\` sólo si están los dos archivos (`:46-63`) y ya sabe consultar un lote (`:73-74`). La dirección viene de `INCOMEDOR_RECEPTOR_URL` o es `https://localhost:5001/` (`EntornoDemostracion.cs:15-19`, `:47-49`).
- **Los logos** se leen de `marcas\` con sus reglas (`LogosLocales.cs:19-21`) y el estilo de `estilo-visual.json` (`AlmacenEstiloVisual`, registrado en `MainWindow.xaml.cs:45-46`). Las pruebas que no registran un almacén usan los valores por defecto (`PantallaAtencion.razor:288-295`).
- **El rebote**: `puesto.css:25` deja `html, body` sin `overscroll-behavior`, y el diálogo y la lista de C se desplazan solos (`:246-249`, `:289-294`).

## Goals / Non-Goals

**Goals:**

- Que el modo y la clave decidan sólo el acceso: la atención, RB06 y lo que se registra de una atención no cambian.
- Que mover la asistencia a una pestaña no afloje RB26: el mismo servicio, las mismas denegaciones y la sesión que se cierra al salir, al cerrar Configuración o a los 30 s.
- Que las pestañas no cambien las pruebas del diálogo, de la impresora ni de las bitácoras, y que las que cambian sean sólo las del punto 4 del gate.

**Non-Goals:**

- Lo que el proposal deja en «Non-goals».
- Persistir una edición sin guardar entre aperturas de Configuración.
- Un límite de tiempo para Configuración abierta: sólo la sesión de operador vence a los 30 s.

## Decisions

### D1. Nueve pestañas, con General primero

General (modo, bitácoras y «Cerrar el puesto»), Personas, Opciones, Consumos, Comprobantes, Identidad visual (estilo y logos, el nombre de la pestaña de la maqueta), Impresora, Pantalla y Red (datos del equipo y prueba con el receptor). Las pestañas siguen el patrón de pestañas de WAI-ARIA (`role="tablist"`, `tab` y `tabpanel`, flechas, Inicio y Fin), con los colores y las superficies de `puesto.css`, en una fila de botones de 56 px con texto de 16 px, que en 1024 entra en el diálogo de 960 px.

- **Descartada:** diez pestañas, con Estilo y Logos separados. En 1024 × 768 no entran en una fila sin achicar el texto o los blancos.
- **Descartada:** copiar ahora el estilo de las pestañas de INWeb. El repo no tiene una referencia de INWeb, y la nota de pantalla y configuración (sección 4) deja la referencia concreta para contrastarla con INWeb al diseñar: se contrasta cuando David la dé.

### D2. Todas las pestañas montadas, la elegida visible

Cada `tabpanel` se dibuja siempre y los no elegidos llevan `hidden`. Así lo editado en una pestaña sobrevive al pasar a otra, y las pruebas de bUnit que buscan `#opacidad`, `#impresora-sinpapel`, `#bitacora-general`, `#guardar-configuracion` o `#cancelar-configuracion` apenas se abre Configuración los siguen encontrando: ninguna de `PanelConfiguracionTests`, `ConfiguracionModalTests`, `GuardarEstiloSinEscrituraTests` ni las de Configuración de `BitacorasDelPuestoTests` cambia por las pestañas. Los id quedan únicos: Opciones conserva los de C y Personas usa id propios. La pestaña Opciones muestra cuántas opciones hay y en qué versión, y su botón `#abrir-opciones` («Editar», o «Ver» con una versión de INWeb) abre el panel de C tal cual; su «Guardar» y su «Cancelar» vuelven al resumen. Así `PanelOpcionesTests` no cambia. Ninguna pestaña oculta dibuja un `role="alert"` mientras no tenga un aviso, porque `GuardarEstiloSinEscrituraTests` toma el primero del diálogo.

- **Descartada:** montar sólo la pestaña elegida. Perdería lo editado al cambiar de pestaña y obligaría a cada una de esas pruebas a tocar primero su pestaña.

### D3. Cada «Guardar» guarda su pestaña y Configuración sigue abierta

El pie del diálogo conserva «Cancelar» (`#cancelar-configuracion`): cierra, descarta lo no guardado y registra `CONFIGURATION_CANCELLED`, como hoy. Escape hace lo mismo, también con un panel de edición abierto. «Guardar» del estilo conserva su id y deja de cerrar el diálogo (cambia la línea 87 de `PanelConfiguracionTests.Guardar_y_volver_a_abrir`); los demás «Guardar» están dentro de su pestaña.

- **Descartada:** que «Guardar» del estilo siga cerrando el diálogo. Ninguna prueba cambiaría, pero con nueve pestañas cerrar al guardar una esconde las otras y se comporta distinto que Personas y Opciones.
- **Descartada:** un solo «Guardar» para todo. Es la mezcla que D12 de C descartó: un estilo que no se puede escribir dejaría a medias una versión de la base.

### D4. Personas: una tabla con «Editar», columnas por cupo y orden en la vista

La pestaña muestra la tabla de lectura y «Editar» (`#abrir-personas`) la pasa a edición en el mismo lugar (`#panel-personas`): el nombre y cada límite son campos, una fila nueva suma el código enmascarado de C, y «Quitar», «Guardar» y «Cancelar» actúan como en C. Las columnas de cupo son los pares de servicio y categoría que usan las cargas de las opciones o alguna regla de una persona, en el orden del catálogo, no el producto de todos los servicios por todas las categorías de `PanelPersonas.razor:168`. Una celda vacía es «sin cupo»; 0 es la denegación explícita de RB02. Las filas miden 44 px con campos de 44 px, el encabezado es fijo (`position: sticky`) y la tabla se desplaza dentro de su caja: en 1024 × 768 entran unas diez filas. Ordenar cambia sólo la vista: la edición se arma y se manda en el orden de lectura, y los campos rechazados vuelven a su fila por esa posición, así que el orden no los mueve.

- **Descartada:** tarjetas más chicas. No dejan comparar cupos entre personas, que es lo que pide una columna por elemento.
- **Descartada:** filas de 56 px. Entran unas siete personas; 44 px es el objetivo de la nota de experiencia para controles que no son la acción principal.

### D5. Mantenimiento por defecto, el modo en un archivo y la atención igual en los dos

`AlmacenModoPuesto` guarda `{"modo":"OPERATIVO"}` o `{"modo":"MANTENIMIENTO"}` en `modo-puesto.json` de la carpeta de datos, con escritura atómica como el estilo. Sin archivo, ilegible o con otro valor, mantenimiento. Sin almacén registrado (las pruebas que no lo usan), mantenimiento en memoria, como el estilo: por eso ninguna prueba existente cambia por el modo. La máquina de la atención no conoce el modo.

- **Descartada:** operativo por defecto. Una carpeta nueva pediría la clave antes de poder configurar nada y cambiarían todas las pruebas que abren Configuración.
- **Descartada:** el modo en la base. No es configuración del snapshot ni de INWeb, sino del equipo, como el estilo (RB21).

### D6. La clave del día detrás de un validador, con el de desarrollo en este corte

`IValidadorClaveDelDia.Validar(string clave, DateOnly fecha)` decide; `ClaveDelDiaDeDesarrollo` acepta de 1 a 12 dígitos ASCII. El diálogo (`DialogoClaveDelDia.razor`) es un campo `type="password"` con `inputmode="numeric"`, `autocomplete="off"` y sin `value` dibujado, «Aceptar», «Cancelar» y el aviso de desarrollo; lo usan el engranaje, «Pasar a operativo» y el cierre. La clave nunca sale del diálogo más que hacia el validador.

- **Descartada:** integrar `Intelektron.ClaveDelDia` (la referencia de la nota de modos). Publicaría en un repo público código de la empresa y el algoritmo de la clave; el validador real entra por la misma interfaz fuera de este corte.
- **Descartada:** un límite de intentos ya. Con un validador que acepta todo no protege nada; va con el validador real.

### D7. Sin botones en la barra de título y el cierre que pasa por el modo

En `SourceInitialized`, `MainWindow` quita `WS_SYSMENU` del estilo de la ventana (`SetWindowLongPtr`), lo que saca cerrar, minimizar y restaurar sin dejar de estar maximizada. `Closing` se cancela si el puesto está en operativo y el cierre no viene autorizado por la clave; entonces pide a la pantalla, por un servicio `CierrePuesto`, que muestre la clave. Un cierre autorizado, uno en mantenimiento y el fin de la sesión de Windows (`Application.SessionEnding`) pasan sin pedir nada, y todos siguen siendo el cierre ordenado de hoy. Escape se escucha en la raíz de la pantalla y sólo actúa en reposo sin diálogos.

- **Descartada:** `WindowStyle="None"`. Con la ventana maximizada tapa la barra de tareas, que es la pantalla completa que la nota distingue de «maximizada».
- **Descartada:** dejar el botón y frenarlo en `Closing`. La decisión del 03/11 pide que no esté.
- **Descartada:** sólo Escape. Una All-in-One táctil no tiene teclado; «Cerrar el puesto» en General es su camino.

### D8. La asistencia pasa a la pestaña Comprobantes, fuera de la máquina

`PestanaComprobantes.razor` usa el mismo `Asistencia` (`Entrar`, `ComprobantesDelDia`, `Reimprimir`, `Recuperar`, `Salir`) con la misma tabla, el aviso de incertidumbre y los motivos de hoy. La sesión vive en el diálogo, en un `SesionOperador` que comparten Comprobantes y Consumos, y termina con «Salir», al cerrar Configuración, al salir de reposo, cuando una acción vuelve sin privilegio (como `DespuesDeAccion` hoy) o a los 30 s sin un toque dentro de esas dos pestañas, con el temporizador de la pantalla. `Asistencia.Salir` suma el registro `PRIVILEGED_SESSION_CLOSED` con la causa. `EstadoAtencion.Asistencia` y los miembros de asistencia de `MaquinaAtencion` (`:14`, `:48`, `:120-129`, `:309`, `:319-322` y `:340-443`) quedan sin uso y se borran.

- **Descartada:** mantener el estado `Asistencia` de la máquina y abrirlo desde Configuración. Configuración sólo existe en reposo (`PantallaAtencion.razor:279`): entrar a ese estado la cerraría.

### D9. Consumos: una consulta propia, con orden en SQL y el operador de Comprobantes

`ConsultaConsumos.Leer(FiltroConsumos)` en `Infrastructure.Sqlite` une consumo, persona y opción de la versión del consumo, servicio, comprobante y último trabajo, con `comconFechaNegocio BETWEEN $desde AND $hasta` y los filtros opcionales por persona, servicio y opción, todos parametrizados. El orden sale de una lista cerrada (`Hora`, `Persona`, `Opcion`, `Servicio`, `Cargo`, `Estado`) que se traduce a una expresión fija, nunca de un texto de la pantalla (OWASP A03); el cargo se ordena por `json_extract(comconPrecioAplicado, '$.EmployeeChargeMinor')`. Pide 501 filas para saber si hay más y muestra 500. El filtro de persona lista los `PersonId` de la versión activa y de las anteriores, con el nombre de la más reciente y la marca de baja. Cada consulta registra `CONSUMPTIONS_QUERIED` con el `PersonId` y el `GrantId` del operador, comprobados de nuevo como en cada acción de RB26.

- **Descartada:** ordenar en la pantalla. Con el tope de 500, ordenaría sólo lo que entró, y el escenario «El orden decide qué entra en el tope» fallaría.
- **Descartada:** abrir Consumos con sólo entrar a Configuración. Es la alternativa del punto 3 del gate: la clave del día no identifica a nadie y la tabla muestra qué comió cada persona.

### D10. El monitor por su identificador de dispositivo, en un archivo

`Monitores` enumera con `EnumDisplayMonitors`, `GetMonitorInfo` y `EnumDisplayDevices` (el nombre de interfaz del monitor, que sobrevive a un reinicio y a un cambio de orden). `pantalla.json` guarda ese identificador, el nombre de dispositivo y el área. Al abrir y al guardar, `MainWindow` pone la ventana en el área de trabajo de ese monitor y la maximiza; si no está, usa el principal y deja el aviso para la pestaña. Elegir el monitor es una función pura (`ElegirMonitor(monitores, guardado)`) con sus pruebas.

- **Descartada:** `System.Windows.Forms.Screen`. Suma Windows Forms al host para leer una lista que dan tres llamadas de `user32`.
- **Descartada:** guardar sólo `\\.\DISPLAY2`. Cambia de número cuando cambia el orden de los monitores.

### D11. Red: `System.Net.NetworkInformation`, sólo lectura

`DatosDeRed` lee el nombre del equipo y los adaptadores con `OperationalStatus.Up`, sin el de loopback, con sus IPv4, máscaras, puertas y DNS, detrás de una interfaz para que la prueba use un doble. No hay campos ni «Guardar».

- **Descartada:** cambiar la IP con `netsh` o WMI. Pide permisos de administrador, puede cortar la conexión con el receptor en medio de una atención y es del sistema operativo, no del puesto.

### D12. La prueba de conexión, por etapas, sobre el camino del envío

`PruebaConexion` resuelve el nombre (`Dns.GetHostAddressesAsync`), abre un socket al puerto con 5 s de espera y, si los dos funcionan, hace con `ClienteReceptorHttp` la consulta de un lote que no existe: una respuesta 404 del receptor prueba el TLS mutuo y que el receptor responde. Un error de TLS se distingue de uno HTTP por la excepción. No se envía ningún lote ni se toca la cola. Cada prueba registra `CONNECTIVITY_TESTED` con la dirección, las etapas y sus resultados.

- **Descartada:** una ruta de salud nueva en el receptor. Cambiaría el receptor, que este change no toca.
- **Descartada:** probar sólo con un `ping`. No dice nada del TLS ni del receptor.

### D13. Impresora: la prueba sin base y los textos en un archivo

«Imprimir una prueba» manda directo a `IImpresoraComprobantes.Enviar` un `ComprobanteAImprimir` con un `TicketId` nuevo, la leyenda PRUEBA y datos sin persona ni importe; no pasa por `TrabajosImpresion` ni por la base. Como su `TicketId` no es el de ningún consumo, `MaquinaAtencion.cs:498-501` nunca lo muestra como comprobante de una atención. Los textos van en `impresora.json` (`AlmacenImpresora`), validados al leer y al guardar; `ImpresoraSimulada` y el comprobante en pantalla los leen al imprimir. Registros: `PRINTER_TESTED` y `CONFIGURATION_CHANGED` con `TICKET_TEXTS`.

- **Descartada:** guardar los textos dentro de los datos del comprobante, en la transacción de RB06. Cambiaría lo que se confirma por algo que es de la impresora, y una copia no podría corregir un pie mal escrito.
- **Descartada:** activar o parametrizar una impresora física. No hay dispositivo elegido (nota de modos e impresión: «se detallarán con el dispositivo elegido»).

### D14. Logos: `InputFile` con las reglas de `LogosLocales`

La sección de logos usa `InputFile` (`accept=".png,.webp,.svg"`), lee hasta 2 MB más un byte para saber si se pasó, y valida con las mismas reglas de `LogosLocales` (extensión, firma del contenido y tamaño). La vista previa es un `img` con un `data:` URI sobre la cuadrícula, nunca el SVG insertado. Guardar escribe `marcas\empresa.<ext>` o `marcas\concesionaria.<ext>` por un temporal, borra las otras extensiones del mismo logo y vuelve a leer los logos; «Volver al de ejemplo» los borra. Registra el SHA-256, el tamaño y el formato.

- **Descartada:** copiar el archivo sin validarlo y dejar que el arranque lo descarte. El logo vigente desaparecería sin aviso hasta el próximo arranque.

### D15. El rebote, con CSS y medido en el monitor

`overscroll-behavior: none` en `html` y `body`, y `overscroll-behavior: contain` en el diálogo, la selección de la ventana angosta y las dos tablas. El E2E lo comprueba en el estilo calculado; que alcance para el WebView2 de la All-in-One lo dice la prueba de David en su monitor.

- **Descartada:** apagar ya el rebote elástico de Chromium con un argumento del WebView2. Sin medirlo en el monitor no se sabe si es la causa, y cambia el entorno del WebView2 para todo el puesto (punto 14 del gate).

### D16. El ícono, generado y con su procedencia

`Recursos\incomedor-puesto.ico`, generado con la paleta de `puesto.css` por el script que ya hizo el ícono del acceso directo (fuera del repo, en `C:\INComedor\herramientas\icono\`), con 16, 32, 48 y 256 px; `ApplicationIcon` en el `.csproj` y `Icon` en `MainWindow.xaml`; su procedencia en `wwwroot\img\PROCEDENCIA.md`.

- **Descartada:** el logo de la empresa del cliente como ícono. Es la marca que la presentación académica tiene que anonimizar.

### D17. El actor sigue nulo; `Access` dice cómo se entró

Los registros de Configuración siguen con `ActorPersonId` nulo y `ActorReason` `NO_AUTHENTICATION`, y suman `Access`: `MAINTENANCE_MODE` o `DAILY_KEY`. La clave genera `DAILY_KEY_ACCEPTED` o `DAILY_KEY_REJECTED` con la acción (`OPEN_CONFIGURATION`, `CLOSE_KIOSK`, `ACTIVATE_OPERATIVE`) y, al rechazar, la causa (`INVALID_FORMAT`), nunca lo tipeado. El modo, el monitor, los logos y los textos usan `CONFIGURATION_CHANGED` con `KIOSK_MODE`, `DISPLAY_MONITOR`, `COMPANY_LOGO`, `CONCESSIONAIRE_LOGO` y `TICKET_TEXTS`. Todos los tipos nuevos van a la general, como cualquier tipo que el escritor de B no reconoce como interacción.

- **Descartada:** cambiar `ActorReason` a `DAILY_KEY`. Cambiaría lo que exige `BitacorasDelPuestoTests.SinActor` en tres pruebas (cinco afirmaciones: `:123`, `:129`, `:147`, `:165` y `:166`), y mezclaría quién actuó con cómo entró.

### D18. Las pruebas existentes que cambian, y nada más

Las 13 del punto 4 del gate, medidas sobre `9202347`: 4 de `PantallaAtencionTests` (y su ayudante), 7 de `PanelPersonasTests`, la línea 87 de `PanelConfiguracionTests.Guardar_y_volver_a_abrir` y `AdministracionE2ETests.Agregar_una_persona_y_atenderla`. Las de la asistencia cambian sólo la entrada (la pestaña en lugar del botón) y el id del aviso; sus afirmaciones de RB26 quedan.

- **Descartada:** borrar las cuatro de la asistencia y escribir otras. Perdería la historia de RB26 en el mismo archivo y es tan cambio como reescribir su entrada.

## Risks / Trade-offs

- [En mantenimiento, cualquiera que toque el engranaje edita personas y cupos y cierra el puesto] → es el punto 11 del gate: un piloto arranca en operativo, y cada acción queda en la general con `Access`.
- [La clave de desarrollo acepta cualquier número] → el diálogo lo dice; el validador real entra por `IValidadorClaveDelDia` (D6).
- [Quitar `WS_SYSMENU` deja a Alt+F4 sin efecto o lo convierte en un pedido de cierre según la versión de Windows] → `Closing` cubre los dos caminos; se mide en el E2E con `WM_CLOSE` y en el monitor.
- [El rebote puede no ser del CSS] → D15 y el punto 14 del gate: lo dice el monitor de David.
- [La tabla con cientos de personas] → se mide con la semilla de quince y 200 personas en el E2E y en una captura; si el dibujado pasa de 500 ms, queda como riesgo para la verificación.
- [El delta de `administracion-local` modifica un requisito de C, que no está archivado] → `openspec validate` lo informa («Archive would refuse this delta … target spec does not exist»): C se archiva antes que D (punto 13 del gate).
- [Varios monitores no se prueban en la CI] → `ElegirMonitor` se prueba con dobles y el E2E cubre el caso del monitor guardado que no está; el cambio real de monitor lo prueba David.
- [El alcance completo con el corte del 11/11] → el punto 1 del gate y el orden de los grupos de `tasks.md`: lo que el recorte deja afuera va al final.

## Migration Plan

Sin migración de la base: la versión de esquema sigue en 4. Tres archivos nuevos en la carpeta de datos (`modo-puesto.json`, `impresora.json` y `pantalla.json`), todos opcionales: sin ellos, el puesto arranca como hoy, en mantenimiento, con los textos y el monitor por defecto. Un puesto de C que abre la misma carpeta los ignora. Volver atrás es volver a la punta de C; los logos que se hayan guardado desde Configuración quedan en `marcas\`, donde C ya los leía.
