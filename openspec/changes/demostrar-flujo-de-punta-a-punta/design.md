# Design

## Context

El motivo está en `proposal.md`, «Why». Lo que condiciona el cómo, medido en el código el 30/10 sobre `5843152`:

- `MainWindow` arma todo con valores fijos: la carpeta `%LOCALAPPDATA%\INComedor\demo\` (`CarpetaDemostracion()`), la base `incomedor.db` adentro, los comprobantes en `comprobantes\`, los certificados en `certificados\` y el receptor en `https://localhost:5001/`. La carpeta de datos del WebView2 es la que elige la biblioteca por defecto.
- El receptor ya toma su carpeta de certificados, su base y su puerto de la configuración (`Receptor:CarpetaDemostracion`, `Receptor:BaseDemostracion` y `Receptor:PuertoDemostracion`, en `DemostracionReceptor.cs`), y su instancia de `INCOMEDOR_SQLSERVER`. No hace falta cambiarlo.
- El ciclo de envío del puesto corre una vez al arrancar y después cada 30 s (`ServicioAtencion.CicloDeEnvioAsync`). La franja «Sincronización» es un `role="status"` con `aria-label="Sincronización con el receptor"`.
- La pantalla ya tiene los selectores que necesita el E2E: `#codigo`, `#ingresar`, los botones `.opcion` con el nombre de la opción, `#confirmar`, `#mensaje` y `#listo`. No hace falta tocar Razor.
- El spike del 29/10 midió que, en una sesión elevada como la de este equipo, WebView2 ignora `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`, y que el puerto entra por `BlazorWebViewInitializing` con `EnvironmentOptions.AdditionalBrowserArguments`. `BlazorWebViewInitializingEventArgs` también expone `UserDataFolder`.
- La CI corre `dotnet test INComedor.sln --no-build` en `windows-latest`. Todo proyecto de pruebas de la solución entra solo, y `ci.yml` no se toca en este change.
- La L58 vigente pide el E2E (L58:96), la evidencia (L58:111-117), el README para revisar en local (L58:121) y sólo credenciales de prueba, con un `.env.example` (L58:125). Para escritorio, la L59:57 pide el EXE como Release.

## Goals / Non-Goals

**Goals:**
- Un E2E que use los mismos binarios que corre quien revisa, en local y en la CI, sin tocar los datos del usuario.
- Que el paquete del Release sea exactamente lo que el E2E probó.
- Que el puesto sin variables se comporte byte por byte como hoy.

**Non-Goals:**
- Hacer andar la CI si al runner le falta algo: el diagnóstico sí está en el alcance, el arreglo en `ci.yml` es de David.
- Grabar el video o publicar el Release.
- Acelerar el ciclo de envío para la prueba: el E2E espera el ciclo real de 30 s (D5).

## Decisions

### D1. El entorno se resuelve en una clase pura

`EntornoDemostracion.Resolver(Func<string, string?> variable, string datosLocales)` devuelve un record con la carpeta, la dirección del receptor, el puerto de CDP (o ninguno) y la carpeta de datos del WebView2 (o ninguna). `MainWindow` sólo lo aplica. Las reglas de los tres requirements de `modo-demostracion` (ruta absoluta, `https`, rango 1024 a 65535) se prueban con `EntornoDemostracionTests` en `Kiosk.Tests`, sin ventana y sin tocar el entorno del proceso.

- **Descartada: leer `Environment` dentro de `MainWindow`.** Sólo se probaría abriendo la ventana, y cada caso de borde costaría un proceso.

### D2. El puerto de CDP entra por código, en todos los builds

Con un puerto válido, `MainWindow` asigna `blazorWebView.BlazorWebViewInitializing` y pone `--remote-debugging-port=<puerto>` en `EnvironmentOptions.AdditionalBrowserArguments`, como la costura del spike. No se agrega `--remote-debugging-address`: Chromium escucha por defecto sólo en loopback, y el escenario «Puerto válido» lo comprueba. El rango empieza en 1024 para no tomar un puerto de servicio del sistema.

Riesgo de seguridad (OWASP A05): con el puerto abierto, cualquier proceso del mismo equipo puede manejar la pantalla y leer lo que muestra. Por eso se abre sólo con la variable, nunca por defecto, y el README dice que es sólo para la prueba. Sin la variable no escucha nada (escenario «Sin la variable»).

- **Descartada: `#if DEBUG`.** El EXE del Release sería otro que el probado, y el E2E contra el paquete (D7) no podría conectarse.
- **Descartada: `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`.** Se ignora en un proceso elevado (spike del 29/10).

### D3. Tres variables, con el prefijo de `INCOMEDOR_SQLSERVER`

`INCOMEDOR_DEMO_DIR`, `INCOMEDOR_RECEPTOR_URL` e `INCOMEDOR_CDP_PORT`. Cada una tiene una razón propia: aislar los datos, no chocar con una demostración en el 5001 y abrir la depuración. Un valor inválido se ignora y el puesto usa el valor por defecto. Nunca cae en un destino relativo al directorio de trabajo ni en `http`.

Una ruta absoluta que no se puede crear (una unidad que no existe, una carpeta sin permiso) no es un valor inválido: el puesto no arranca. No vuelve a la carpeta por defecto, porque la spec le prohíbe tocarla cuando la variable trae una ruta absoluta, y así una demostración aislada nunca se mezcla con la base propia. Hoy se cierra sin mensaje; David decidió el 30/10 documentarlo y no agregar un aviso (sugerencia 1 del verify).

- **Descartada: una sola variable «modo E2E».** Mezcla aislamiento con depuración, cuando una demostración aislada sin puerto abierto también sirve, por ejemplo para grabar el video sin tocar la base propia.
- **Descartada: un archivo de configuración junto al EXE.** El paquete tendría un archivo más que validar, y la prueba tendría que escribir en la carpeta del EXE.

### D4. La carpeta elegida lleva también los datos del WebView2

Con `INCOMEDOR_DEMO_DIR`, la base, `comprobantes\` y `certificados\` van ahí, y `BlazorWebViewInitializing` fija `UserDataFolder` en `<carpeta>\webview2`. Así, el E2E puede correr con una sesión de la demostración abierta: dos entornos de WebView2 sobre la misma carpeta de datos y con argumentos distintos no conviven. Sin la variable, la carpeta de datos del WebView2 sigue siendo la de hoy. El receptor del E2E recibe `Receptor__CarpetaDemostracion=<carpeta>\certificados`, `Receptor__PuertoDemostracion` y `Receptor__BaseDemostracion` por entorno, con las claves que ya lee.

- **Descartada: dejar la carpeta de datos del WebView2 compartida.** El E2E fallaría al crear su entorno cada vez que David tuviera el puesto abierto. La tarea 3.4 lo comprueba con el puesto abierto.

### D5. El E2E: procesos reales y el ciclo real

Proyecto nuevo, `tests/Intelektron.INComedor.E2E.Tests`: xUnit v3, `net11.0-windows10.0.17763.0`, en la solución, con `Microsoft.Playwright` 1.64.1 y `Microsoft.Data.SqlClient` para mirar la base del receptor. Referencia al puesto y al receptor con `ReferenceOutputAssembly="false"`, sólo para que se compilen antes. Busca sus EXE en la salida de cada proyecto, en la configuración de la propia prueba. `INCOMEDOR_E2E_KIOSK_EXE` reemplaza el del puesto por el del paquete (D7).

Preparación de cada prueba:
- una carpeta temporal propia;
- dos puertos libres, tomados con un `TcpListener` en el puerto 0 y soltados;
- una base del receptor `INComedorE2E_<8 hex>`, en la instancia de `INCOMEDOR_SQLSERVER` o en LocalDB.

Al final, siempre, en `finally`: cada proceso se cierra por su PID con su árbol (`Kill(entireProcessTree: true)`, como en el spike), se borra la base y se borra la carpeta. Antes y después se toma una foto de `%LOCALAPPDATA%\INComedor\demo\`: si existe, sus archivos y fechas, y si no, que sigue sin existir. Las dos fotos tienen que ser iguales.

**El flujo principal**, que es el escenario de `docs/pruebas.md:46`:
1. Arranca el receptor con `--demostracion`, que genera los certificados en la carpeta y registra el puesto, espera a que su puerto acepte conexiones y se detiene. Queda el servidor apagado.
2. Arranca el puesto con las tres variables. Playwright se conecta por CDP y espera `#codigo`.
3. `0001` → «Ingresar» → «Almuerzo común» → «Confirmar». `#mensaje` dice «Consumo registrado. Retirá tu comprobante», y `comprobantes\` tiene un archivo. Después, «Listo».
4. `0001` otra vez, el mismo almuerzo: el mensaje de cupo. Un solo consumo autorizado en la base del puesto (AT02).
5. La franja llega a «2 pendientes» (un consumo y un rechazo) en 45 s o menos: el ciclo es de 30 s.
6. Arranca el receptor otra vez, con la misma base y los mismos certificados. La franja llega a «al día» en 45 s o menos.
7. En la base del receptor, `tbComedorConsumosConsolidados` tiene un solo consumo, con el `ConsumptionId` del puesto (AT27).

Cada paso deja una captura en la carpeta de resultados de la prueba, que sirve de base para el guion del video (D9). Ningún paso usa `Thread.Sleep`: cada espera es una aserción de Playwright con plazo. La corrida esperada es de 1,5 a 2 minutos.

**Las costuras con la ventana real** (los escenarios de `modo-demostracion` que no se cubren en D1):
- puerto válido, que escucha sólo en loopback;
- sin la variable, donde ningún proceso del puesto escucha;
- `abc`, `80` y `70000`, donde la ventana abre y nada escucha.

Para ver qué escucha, los procesos del puesto se toman del árbol del PID (`CreateToolhelp32Snapshot`), y los puertos en escucha, de `GetExtendedTcpTable`. Las dos son de sólo lectura y sin dependencias nuevas.

Dentro del ensamblado E2E, las pruebas no corren en paralelo: cada una abre una ventana, y el orden se vuelve legible en el registro. Con las otras pruebas de la solución no comparten nada.

- **Descartada: alojar el receptor en el proceso de la prueba con `WebApplicationFactory`.** Sería otro arranque que el que usa quien revisa, y Kestrel bajo la fábrica arma sus opciones antes del arranque (lección del change 5).
- **Descartada: una variable que acorte el ciclo de 30 s.** Otra costura en producción para ahorrar un minuto; el E2E tiene que ver el ciclo real.
- **Descartada: atributos `data-testid` nuevos.** Los `id` existentes alcanzan, y la pantalla no cambia.

### D6. El E2E corre en la CI sin tocar `ci.yml`

Entra a la solución, así que el job `build-test` lo corre. Antes de lanzar nada, la preparación comprueba tres condiciones. Si falta una, falla con un mensaje que la nombra, nunca con un salto ni con un `Skip`:
- el runtime de WebView2, por la clave de registro que documenta Microsoft (`EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`, valor `pv`);
- la instancia de SQL Server, con una conexión;
- una sesión con escritorio interactivo (`Environment.UserInteractive`).

Si la primera corrida en `windows-latest` falla por una de esas tres, el remedio está en `ci.yml` y es de David. Las opciones son instalar el runtime, iniciar LocalDB con `sqllocaldb start MSSQLLocalDB`, o filtrar el ensamblado E2E.

- **Descartada: dejar el E2E fuera de la solución y correrlo con un script.** Pierde la barrera automática, y el evaluador no lo ve en la CI.
- **Descartada: un `Skip` condicional por entorno.** Una prueba que se saltea en la CI muestra verde sin haber corrido.

### D7. El paquete: carpeta autocontenida en un zip, armada y probada por un script

`.github/scripts/publicar-puesto.ps1 -Version <v>`:
1. compila la solución en `Release`;
2. publica el puesto con `dotnet publish src/Intelektron.INComedor.Kiosk -c Release -r win-x64 --self-contained true -o artifacts/publicacion/puesto`;
3. revisa que la publicación no traiga archivos `.db`, `.pfx`, `.key` o `.pem`, ni carpetas `comprobantes` o `certificados`, y si los trae falla y los nombra;
4. corre el flujo principal del E2E con `INCOMEDOR_E2E_KIOSK_EXE` apuntando al EXE publicado. En ese modo, la prueba lanza el puesto con un `PATH` sin carpetas de `dotnet` y con `DOTNET_ROOT` en una carpeta vacía, que es el escenario «Abre sin .NET instalado»;
5. sólo si todo pasó, escribe `artifacts/INComedor-Puesto-<v>-win-x64.zip` y su `.sha256`.

`artifacts/` se ignora en git. El mismo script corre en local y en el workflow (D8), así que el workflow no tiene pasos que no se hayan probado.

- **Descartado: `PublishSingleFile`.** Tiene que extraer `wwwroot` y las bibliotecas nativas de SQLite y de WebView2, y no se probó.
- **Descartado: MSIX o MSI.** Piden firma o un instalador, y la L59:57 pide el EXE.
- **Descartado: incluir el receptor.** Necesita SQL Server, y quien lo quiera revisar lo corre desde el código.

### D8. El Release lo arma `release.yml`, como borrador, con un tag de David

Workflow nuevo, `.github/workflows/release.yml`:
- se dispara sólo con `push: tags: ['v*']`;
- tiene `permissions: contents: read` arriba y `contents: write` sólo en el job que crea el borrador;
- tiene dos jobs. `paquete` corre en `windows-latest`, con sólo lectura, `publicar-puesto.ps1 -Version <tag>` y sube el zip y su suma como artefacto. `borrador`, que depende de `paquete`, corre en `ubuntu-latest` sin clonar el repo, comprueba la suma y hace `gh release create <tag> --draft` con `GITHUB_TOKEN`. Así el código del repo y de NuGet nunca corre con permiso de escritura;
- usa sólo acciones de GitHub (`checkout`, `setup-dotnet`, `upload-artifact` y `download-artifact`), sin terceros, fijadas por SHA con su versión en un comentario, porque una etiqueta como `@v7` puede moverse (OWASP A08).

Los dos jobs y las acciones por SHA los decidió David el 30/10 (sugerencia 6 del verify). Exigir que el tag esté sobre `main` se descartó, porque la entrega va en `final-project-dw`.

Si el script falla, `borrador` no corre y no queda Release. Publicar el borrador, y sumarle el video, es de David. El tag va después del paso al SDK estable del 10/11 (D-C25). El workflow y el script se prueban antes en local, con la RC, y el workflow corre de verdad recién con el primer tag.

- **Descartado: `gh release create` desde el equipo de David.** No arma el paquete en un entorno limpio, y la devolución pidió un pipeline que publique.
- **Descartado: `softprops/action-gh-release` u otra acción de terceros.** Suma una dependencia con permiso de escritura (OWASP A08), y `gh` ya está en el runner.
- **Descartado: que el Release salga publicado.** Publicar es de David, que lo hace con un clic sobre el borrador.

### D9. El video lo graba David, con un guion del change

`docs/guion-video.md` tiene el recorrido en pasos con su tiempo, de 2 a 3 minutos en total:
1. qué es INComedor y qué muestra el video;
2. el EXE del Release abriendo;
3. `0001`: selección, confirmación y comprobante;
4. el segundo almuerzo, que choca con el cupo;
5. «Sin papel» y el comprobante pendiente;
6. `9001`: «Asistencia», recuperar y reimprimir con DUPLICADO;
7. el receptor apagado, los pendientes y el receptor de vuelta, con la franja al día;
8. el E2E en verde en la CI.

Sugiere una herramienta que ya trae Windows (la grabación de pantalla de la Herramienta Recortes) y una carpeta de demostración propia (`INCOMEDOR_DEMO_DIR`), para no grabar la base de uso diario. El video va como archivo del Release y el README lo enlaza.

- **Descartado: que la IA arme el video con capturas.** Sin narración muestra menos que el E2E y sus capturas.
- **Descartado: YouTube no listado.** La L58:176 lo propone para la opción B, y suma una cuenta y una plataforma fuera del repo.

### D10. «Cómo revisar», `.env.example` y `docker-compose`

En el README, una sección «Cómo revisar» al principio del §1.4, con tres caminos:
- **el Release**: bajar el zip, comprobar la suma y abrir el EXE (requisito: WebView2 Runtime);
- **el código**: clon limpio, `dotnet build`, `dotnet test` y `dotnet run`, con el SDK de `global.json`;
- **el E2E**: el comando, lo que recorre y cuánto tarda.

Además:
- las credenciales de prueba: un enlace a la tabla de códigos sintéticos que ya está en «Cómo ver la demostración», más la línea de que son las únicas y no hay contraseñas;
- los datos sembrados;
- el `.env.example`, con sus cuatro variables vacías y comentadas (`INCOMEDOR_SQLSERVER`, `INCOMEDOR_DEMO_DIR`, `INCOMEDOR_RECEPTOR_URL` e `INCOMEDOR_CDP_PORT`). El README aclara que nada lo carga, porque el puesto lee el entorno del proceso, y muestra cómo fijarlas en PowerShell;
- la línea «`docker-compose`: no aplica», con su motivo: app de escritorio con base local, y Docker fuera del corte según el §7 del catálogo;
- el enlace al video.

El §2.6 suma el E2E y sus condiciones, y deja los conteos al día con la salida de `dotnet test`.

- **Descartada: sólo una línea sobre `.env.example` en el README.** Cumple la L58:121, pero no la L58:125 («Deja un `.env.example` con las variables vacías»).
- **Descartado: copiar la tabla de códigos en «Cómo revisar».** Dos copias se desalinean; un enlace no.

### D11. Evidencia

La última tarea agrega las filas de `docs/aplicacion-del-curso.md` (E2E con la L108 y la L58:96, Release con la L59:57, evidencia con la L58:111-117, README con la L58:121 y 125) y pone al día `docs/cobertura-entrega.md` y `docs/pruebas.md:46`. Suma a `prompts.md` el ajuste humano de la elevación, que el spike encontró y la costura resuelve, y a `docs/plan-academico.md` las tallas y las horas reales. `AGENTS.md` suma el comando del E2E y las tres variables, sin pasar de 80 líneas: hoy tiene 70.

## Risks / Trade-offs

- **[`windows-latest` sin escritorio interactivo, sin el runtime de WebView2 o sin LocalDB]** → El E2E falla con un mensaje que nombra lo que falta (D6), y el remedio en `ci.yml` es de David. Despejado el 30/10: la primera CI de la rama, la corrida 41108336041 sobre `006fb05`, pasó el E2E en `windows-latest` sin tocar `ci.yml`.
- **[Si el host de pruebas muere de golpe, el puesto y el receptor que lanzó el E2E quedan vivos]** → En la CI no pasa, porque el runner se descarta; en local, cada prueba usa sus puertos y su carpeta, así que un resto no rompe la corrida siguiente. Un Job Object los ataría al host; David decidió el 30/10 no agregarlo (sugerencia 8 del verify).
- **[El zip lleva los `.pdb`]** → Un error del puesto sale con archivo y línea. La ruta que muestran es la de donde se compiló, que en el Release es la del runner. David decidió el 30/10 dejarlos.
- **[El ciclo de 30 s hace lenta la prueba]** → Una corrida de 1,5 a 2 minutos, con esperas de hasta 45 s por paso y sin `Sleep`. Es el costo de no meter otra costura (D5).
- **[Un puerto «libre» que otro toma entre la reserva y el arranque]** → Si el puerto no responde en el plazo, la prueba toma otros dos y reintenta una vez; el registro lo dice.
- **[Con el puerto abierto, cualquier proceso local maneja la pantalla]** → Sólo con la variable; el E2E cierra los procesos en `finally`, y el README la marca como sólo para pruebas (D2).
- **[La convivencia de dos WebView2 sobre la misma carpeta de datos no se midió en este repo]** → D4 la evita con `UserDataFolder`, y la tarea 3.4 corre el E2E con el puesto abierto.
- **[La publicación autocontenida de Blazor Hybrid no se probó]** → El E2E contra el EXE publicado es la condición para armar el zip (D7).
- **[`Microsoft.Playwright` o un transitivo con una vulnerabilidad]** → El paso «Dependencias vulnerables» y NuGet Audit lo cortan, como pasó con `Microsoft.Data.SqlClient` en el change 5. Se fija la versión corregida y se registra.
- **[El workflow del Release no corre hasta el primer tag]** → Hace lo mismo que el script, que se prueba en local, y deja un borrador. Si falla, no queda nada publicado. David decidió el 30/10 ensayarlo antes del 10/11, con la RC, con un tag descartable que después se borra junto con su borrador (WARNING 3 del verify).
- **[El SDK todavía es la RC]** → El tag va después del 10/11 (D8 y D-C25).

## Migration Plan

No hay datos que migrar. Sin las variables, el puesto se comporta como hoy. Para volver atrás, alcanza con revertir los commits; si ya hubiera un tag, se borran el tag y su borrador.

## Open Questions

- El nombre del primer tag (por ejemplo, `v1.0.0`) lo elige David al empujarlo. No cambia las specs, el diseño ni las tareas.
