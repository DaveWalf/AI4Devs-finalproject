# Design

## Context

Ver `proposal.md` (Why). Hoy la solución tiene `Domain`, `Infrastructure.Sqlite` y sus dos proyectos de pruebas (48 en verde). `Infrastructure.Sqlite` ofrece:

- `BaseComedor.Abrir(ruta)`, que migra una base vacía y rechaza una versión desconocida con `EsquemaDesconocidoException`;
- la unidad de trabajo pública `UnidadConfirmacion(BaseComedor, TimeProvider)`, con `Confirmar(SolicitudConfirmacion)`;
- los records `internal` de los eventos de outbox en `EventosSalida.cs`.

Cada confirmación exige una sesión de identidad `CONSUMPTION`, vigente y sin usar. `tbComedorSesionesIdentidad.comcreCodigo` admite NULL.

`Directory.Build.props` fija `net11.0` para todos los proyectos. El CI corre restore, build `-warnaserror` y test de la solución en `windows-latest`.

Paquetes consultados en NuGet el 20/10/2026:

- `Microsoft.AspNetCore.Components.WebView.Wpf` `11.0.0-rc.2.26505.5`. Su número de build no es el del resto de la RC2, porque se publica con MAUI.
- `Microsoft.AspNetCore.Mvc.Testing` `11.0.0-rc.2.26504.105`.
- `bunit` `2.12.5`.

`medir.ps1` cuenta «scaffolds conectados» con dos condiciones: un proyecto front, con `.razor` o `WebView`, que llegue por `ProjectReference` a uno con `Microsoft.Data.Sqlite`; y un proyecto `Sdk.Web` que referencie un `.csproj` propio.

## Goals / Non-Goals

**Goals:**

- Que la pantalla llegue a la confirmación real sobre SQLite real, sin dobles entre la pantalla y la base.
- Que el receptor exista como proyecto web con sus rutas declaradas y no pueda acusar un lote por error.
- Que los proyectos nuevos tengan los nombres de `readme.md` §2.3 y que los changes 2 y 3 los extiendan sin moverlos.

**Non-Goals:**

- Ver `proposal.md` (Non-goals). A nivel de diseño, además: sin inyección de dependencias de contenedor en el puesto, sin MVVM y sin estilos más allá de los necesarios para leer la pantalla.

## Decisions

### D1. Host WPF con `BlazorWebView` y una sola pantalla Razor

`src/Intelektron.INComedor.Kiosk` usa `Sdk="Microsoft.NET.Sdk.Razor"` con `OutputType` `WinExe`, `UseWPF` y `TargetFramework` `net11.0-windows10.0.17763.0`, que pisa al de `Directory.Build.props`. Referencia `Microsoft.AspNetCore.Components.WebView.Wpf` e `Infrastructure.Sqlite`.

- **Corrección durante el apply (20/10).** El diseño decía `net11.0-windows`. Con ese TFM la ventana se cierra al abrir con `FileNotFoundException: Microsoft.Windows.SDK.NET`, porque `Microsoft.Web.WebView2.Wpf` pide la proyección de WinRT, que sólo llega con la versión de plataforma. `10.0.17763.0` es la mínima de WebView2. `proposal.md` (Impact) y las tareas 3.1 y 3.2 se alinearon el mismo día, con la aprobación de David.
- `MainWindow.xaml` contiene sólo el `BlazorWebView`, con `wwwroot/index.html` y el componente raíz `Demostracion/PantallaDemostracion.razor`.
- No hay XAML de pantalla: WPF es la ventana host (ADR01).

Alternativas descartadas:

- **Pantalla en XAML de WPF.** ADR01 la reserva como contingencia, no como segunda tecnología de vistas.
- **.NET MAUI Blazor Hybrid (plantilla `maui-blazor`).** Suma el workload de MAUI y un modelo de aplicación multiplataforma que el puesto Windows no necesita. ADR01 eligió WPF.
- **Aplicación web Blazor servida en `localhost`.** El puesto pasaría a depender de un servidor local y de un navegador, y dejaría de ser el ejecutable de escritorio que se publica como release.

### D2. La lógica de la demostración vive en el Kiosk, sin `Application`

La carpeta `Demostracion/` del Kiosk tiene tres clases:

- `SemillaDemostracion` carga los datos sintéticos.
- `SesionesDePrueba` abre una sesión por toque.
- `ServicioDemostracion` prepara la base y confirma: arma la `SolicitudConfirmacion` y llama a `UnidadConfirmacion.Confirmar`.

La pantalla depende sólo de `ServicioDemostracion`, que se construye en `MainWindow` y se registra como singleton en los servicios del `BlazorWebView`.

Alternativas descartadas:

- **Crear ahora `Intelektron.INComedor.Application`.** D3 de `registrar-consumo-atomico` dejó ese proyecto para el change que lo ejercita. El caso de uso real de atención, con estados, sesión y reconfirmación, es de `atender-seleccion-y-confirmacion`. Aquí quedaría como pasamanos de una demostración.
- **Poner la siembra en `Infrastructure.Sqlite`.** Mezclaría datos de demostración con la infraestructura del producto.

### D3. Siembra con un SQL sintético embebido y UUID fijos

`Demostracion/semilla_demostracion.sql` se embebe como recurso, con UUID fijos y legibles (`00000000-0000-4000-8000-0000000000NN`). Siembra:

- un puesto `SINGLE_KIOSK` en `America/Argentina/Buenos_Aires` con corte 00:00 y su snapshot `ACTIVE`;
- el servicio y la categoría ALMUERZO y un ámbito «Por día en el puesto»;
- una persona activa «Persona de demostración», sin credencial;
- la opción «Almuerzo de demostración», versión 1, en ARS con escala 2, que carga 1 unidad de ALMUERZO;
- una regla por persona de límite 1.

`SemillaDemostracion` la aplica dentro de `BaseComedor.IniciarTransaccionEscritura` y sólo si `tbComedorConfiguracionPuestos` está vacía. La consulta se hace bajo el bloqueo, así que dos arranques simultáneos no siembran dos veces. Las columnas y su orden son los de la migración `0001`, los mismos que usa `FixtureSintetico`.

Alternativas descartadas:

- **Reusar `FixtureSintetico`.** Es código de pruebas con UUID aleatorios. Moverlo a `src` modifica soporte de tests existente, que necesita la aprobación de David, y mezclaría el escenario de quince opciones de las pruebas con la demostración.
- **Configuración por snapshot desde el receptor.** Es de la segunda ola (D-C24).
- **Sembrar en código C#, `INSERT` por `INSERT`.** Un SQL único es más fácil de leer entero en la revisión, como la migración `0001` (L87:444-461).

### D4. Una sesión de prueba nueva por toque, sin credencial

`SesionesDePrueba.Abrir` inserta en `tbComedorSesionesIdentidad` una sesión `CODE`/`CONSUMPTION` de la persona de demostración. Va con `comcreCodigo` NULL, la emisión en `TimeProvider.GetUtcNow()` y el vencimiento dos minutos después, y devuelve su UUID. Es el «adaptador de prueba entrega la sesión» que TK02.1 deja fuera de su alcance.

Alternativas descartadas:

- **Una sola sesión para todos los toques.** El segundo toque devolvería `SesionNoUtilizable` (AT26) en lugar del rechazo por cupo, y la pantalla no mostraría RB03.
- **Identificación simulada por código.** Es el primer trabajo de `atender-seleccion-y-confirmacion`.

### D5. Base en `%LOCALAPPDATA%\INComedor\demo\incomedor.db`

`MainWindow` calcula la ruta con `Environment.SpecialFolder.LocalApplicationData` y crea la carpeta. `ServicioDemostracion` recibe la ruta y un `TimeProvider`, así que las pruebas usan un archivo temporal y un reloj fijo.

Alternativas descartadas:

- **Junto al ejecutable.** Bajo `Program Files` no se puede escribir.
- **Un archivo de configuración.** La configuración del puesto es de la segunda ola. Una ruta fija alcanza para la demostración y para el README del evaluador.

### D6. Errores de apertura visibles en la pantalla

`ServicioDemostracion.Preparar()` devuelve un estado, `Lista` o `ErrorAlmacenamiento`, en lugar de dejar escapar `EsquemaDesconocidoException`. La pantalla muestra «Error de almacenamiento: la base no es de una versión conocida» y no dibuja el botón. Captura sólo esa excepción y `SqliteException`; cualquier otra sigue de largo.

- **Descartada: dejar que la excepción cierre la ventana.** El evaluador vería un cierre sin explicación.
- **Descartada: capturar `Exception`.** Escondería defectos (AGENTS.md: nada de excepciones tragadas).

### D7. Los eventos de outbox pasan a `Contracts`, con el mismo JSON

`src/Intelektron.INComedor.Contracts` es un `net11.0` sin dependencias. `EventosSalida.cs` se mueve allí con `git mv`. Los cuatro records pasan a ser `public sealed`, en el namespace `Intelektron.INComedor.Contracts.Eventos`, y conservan los nombres de C#, los `[JsonPropertyName]` y el orden de las propiedades. `Infrastructure.Sqlite` referencia `Contracts` y agrega el `using`; nada más cambia en `UnidadConfirmacion`. `ContratoEventosTests` lee el JSON guardado, no los tipos, y queda como guardia del movimiento sin tocarse.

Alternativas descartadas:

- **Dejar los records internos y que el receptor no referencie nada propio.** El receptor no tendría ninguna conexión con el puesto y `medir.ps1` seguiría en `back->propio no`.
- **Duplicar los DTO en `Sync.Api`.** Habría dos fuentes de verdad del mismo JSON, que es lo que ADR03 evita con un contrato explícito.
- **Mover también el lote (`SyncBatch`) y su resultado.** Son el contrato de `recibir-lotes-idempotente`.

### D8. Receptor con minimal API y respuestas `501`

`src/Intelektron.INComedor.Sync.Api` usa `Sdk="Microsoft.NET.Sdk.Web"`. `Program.cs` mapea `POST /api/incomedor/v1/sync-batches` y `GET /api/incomedor/v1/sync-batches/{batchId:guid}` a `Results.Problem(statusCode: 501, title: "Ingesta de lotes no implementada")`. El handler no declara cuerpo, así que no lo lee ni lo deserializa: un cuerpo inválido no llega a producir un 500.

- Termina con `public partial class Program;` para `WebApplicationFactory`.
- Referencia `Contracts`, pero hoy no usa ninguno de sus tipos. La referencia fija la dirección de la dependencia, receptor → contrato, y la usará `recibir-lotes-idempotente`. Es la debilidad declarada de este andamio.
- El `501` no está en las respuestas declaradas del OpenAPI. No se agrega porque es un estado transitorio del esqueleto, y el OpenAPI describe el receptor terminado.

Alternativas descartadas:

- **Controladores MVC.** TK01.1 los nombra, pero dos rutas que responden lo mismo no justifican el andamiaje. El change 3 elige al implementar la ingesta.
- **Responder `202 Accepted`.** Un puesto podría tomarlo como acuse y avanzar su cola sin resultado durable (RB16).
- **Validar ya `Idempotency-Key`.** Es el trabajo incluido 2 de TK01.1.

### D9. Pruebas: servicio sobre SQLite real, componente con bUnit y receptor con `WebApplicationFactory`

- **`tests/Intelektron.INComedor.Kiosk.Tests`** (`net11.0-windows10.0.17763.0`, el mismo TFM que el Kiosk, porque un proyecto no puede referenciar a otro de una plataforma mayor) referencia el Kiosk.
  - Prueba `ServicioDemostracion` sobre un archivo temporal real, con el mismo patrón de borrado de `-wal` y `-shm` de D11 del change anterior y un `RelojFijo` propio de 10 líneas.
  - Prueba `PantallaDemostracion` con bUnit `2.12.5` y un `ServicioDemostracion` real sobre archivo temporal, sin doble.
- **`tests/Intelektron.INComedor.Sync.Api.Tests`** usa `WebApplicationFactory<Program>`.

Cada prueba lleva en su `DisplayName` el AT o la fuente y el nombre del escenario del spec.

Alternativas descartadas:

- **Playwright por CDP sobre WebView2.** Es el spike de 2 h de la entrega final (§3.5). Exige la ventana real y un puerto de depuración.
- **Probar la pantalla sólo a mano.** El escenario visible quedaría sin prueba que lo nombre (DoD de feature, L52:72-73).
- **Kestrel en un puerto real para el receptor.** Es más lento y frágil en CI, y no prueba nada más que `TestServer` para dos rutas.
- **Compartir el soporte de pruebas con `Infrastructure.Sqlite.Tests`.** Exigiría moverlo o referenciar un proyecto de pruebas desde otro. `ArchivoTemporal` y `RelojFijo` son clases de pocas líneas y se copian.

### D10. CI sin cambios

Los cinco proyectos entran en `INComedor.sln`, y `ci.yml` los compila y prueba tal como está. `windows-latest` trae los targeting packs de WindowsDesktop junto con el SDK de `setup-dotnet`. bUnit no necesita WebView2.

- **Descartada: un job separado para el Kiosk.** Duplica restore y build sin aislar nada que hoy falle.

## Risks / Trade-offs

- [`WebView.Wpf` RC2 tiene otro número de build que el SDK] → Se fija la versión exacta en `Directory.Packages.props`. La tarea 1.2 verifica que restaura y compila con `-warnaserror`. Si hay un conflicto de versión con `Microsoft.AspNetCore.Components`, se para y se pregunta antes de bajar o subir versiones.
- [bUnit `2.12.5` no declara `net11.0`] → Se resuelve por compatibilidad con su destino más alto. La tarea 3.4 lo comprueba con una prueba vista en rojo y en verde. Si no corre, el componente se prueba a mano, con captura, y se declara.
- [Advertencias del SDK Razor o de WPF rompen `-warnaserror`] → Se corrigen en el código. No se agrega ningún `NoWarn` sin la aprobación de David.
- [La referencia `Sync.Api → Contracts` sin uso se lee como cumplimiento cosmético de «back conectado»] → D8 lo declara, y el README de la entrega dice qué hace y qué no hace el receptor.
- [La pantalla de demostración se toma como TK02.1 cumplido] → El non-goal lo niega y el título de la pantalla dice «demostración».
- [El receptor sin autenticación queda expuesto si alguien lo publica] → No persiste ni refleja nada, y no se despliega en este corte. La autenticación por certificado es el trabajo incluido 1 de TK01.1.

## Migration Plan

No hay datos previos ni despliegue. La base de demostración se crea en el primer arranque y se borra eliminando `%LOCALAPPDATA%\INComedor\demo\`. Revertir es revertir los commits del change. El movimiento de `EventosSalida.cs` se revierte con ellos, y `ContratoEventosTests` vuelve a guardar la versión anterior.

## Open Questions

Ninguna que cambie specs o tareas. Para el gate: D3 fija UUID legibles y nombres sintéticos en la siembra. Si David prefiere otros valores, se cambian en el SQL sin tocar el diseño.
