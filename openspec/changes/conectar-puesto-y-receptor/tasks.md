# Tasks

Reglas de todo el change:

- Cada test nuevo se escribe primero, se corre y se ve fallar por la razón esperada: un fallo de aserción o un `NotImplementedException` de un esqueleto, no un error de compilación. Recién después se implementa lo que lo hace pasar.
- Si un test pasa sin cambios de producción, se rompe a propósito la línea que lo sostiene, se lo ve fallar y se restaura (L81:36-43).
- Ningún test existente se modifica sin la aprobación de David. Los 48 de `Domain.Tests` e `Infrastructure.Sqlite.Tests` quedan intactos.
- Cada test lleva en su `DisplayName` el AT o la fuente y el nombre del escenario del spec.
- Los comandos se corren desde la raíz del repo, porque `global.json` se lee del directorio actual.

## 1. Contrato de eventos compartido

- [x] 1.1 Crear `src/Intelektron.INComedor.Contracts` (`net11.0`, sin dependencias) y agregarlo a `INComedor.sln`; mover con `git mv` `src/Intelektron.INComedor.Infrastructure.Sqlite/EventosSalida.cs` a `src/Intelektron.INComedor.Contracts/Eventos/EventosSalida.cs`, con los cuatro records `public sealed`, el namespace `Intelektron.INComedor.Contracts.Eventos` y los mismos nombres, `[JsonPropertyName]` y orden de propiedades (D7); agregar a `Infrastructure.Sqlite` la referencia a `Contracts` y el `using`. Verificar con `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (48 en verde, incluidas las dos de `ContratoEventosTests`) y `git diff --stat -- tests/` vacío

## 2. Esqueleto del receptor

- [ ] 2.1 Crear `src/Intelektron.INComedor.Sync.Api` (`Sdk.Web`, referencia a `Contracts`) con un `Program.cs` que mapea las dos rutas de D8 a `Results.Ok()` como esqueleto y termina en `public partial class Program;`; crear `tests/Intelektron.INComedor.Sync.Api.Tests` con xUnit v3 y `Microsoft.AspNetCore.Mvc.Testing` `11.0.0-rc.2.26504.105` agregado a `Directory.Packages.props`; agregar los dos a la solución. Verificar con `dotnet build INComedor.sln -warnaserror`
- [ ] 2.2 Escribir con `WebApplicationFactory<Program>` las pruebas de «Envío de un lote» (501, `application/problem+json`, ningún 2xx), «Consulta de un lote», «Cuerpo que no es JSON» (501 y no 500; la respuesta no contiene la cadena enviada) y «Ruta no declarada» (404). Verlas fallar contra el esqueleto con `dotnet test INComedor.sln --filter "FullyQualifiedName~Sync.Api.Tests"`; la de 404 pasa sin cambios, así que se aplica la regla de romper a propósito, mapeando un comodín temporal
- [ ] 2.3 Reemplazar `Results.Ok()` por `Results.Problem(statusCode: 501, title: "Ingesta de lotes no implementada")`, sin parámetro de cuerpo en los handlers (D8); verificar que pasan las cuatro pruebas de 2.2
- [ ] 2.4 Revisar el receptor contra OWASP Top 10 (A01 acceso, A03 inyección, A05 configuración, A09 registro) y pegar en la respuesta la lista punto por punto, con lo que aplica hoy y lo que llega con `recibir-lotes-idempotente`; verificar que la prueba «Cuerpo que no es JSON» cubre A03 y A05

## 3. Puesto con pantalla de demostración

- [ ] 3.1 Crear `src/Intelektron.INComedor.Kiosk` (D1: `Sdk.Razor`, `WinExe`, `UseWPF`, `net11.0-windows`, `Microsoft.AspNetCore.Components.WebView.Wpf` `11.0.0-rc.2.26505.5` en `Directory.Packages.props`, referencia a `Infrastructure.Sqlite`) con `App.xaml`, `MainWindow.xaml` con sólo el `BlazorWebView`, `wwwroot/index.html` y un `Demostracion/PantallaDemostracion.razor` vacío; agregarlo a la solución. Verificar con `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores)
- [ ] 3.2 Crear `tests/Intelektron.INComedor.Kiosk.Tests` (`net11.0-windows`, referencia al Kiosk) con su `ArchivoTemporal` y su `RelojFijo` (D9), y escribir las pruebas de `ServicioDemostracion` sobre un archivo SQLite real temporal: «Primer arranque sobre una base inexistente», «Un segundo arranque no duplica ni borra», «Base de una versión de esquema desconocida» (estado `ErrorAlmacenamiento` y hash SHA-256 del archivo igual antes y después), «Primera confirmación del día», «Segunda confirmación con el cupo agotado» y «La sesión de prueba no lleva código» (`comcreCodigo` NULL en la sesión creada). Verlas fallar contra esqueletos de `ServicioDemostracion`, `SemillaDemostracion` y `SesionesDePrueba` que lanzan `NotImplementedException`, con `dotnet test INComedor.sln --filter "FullyQualifiedName~Kiosk.Tests"`
- [ ] 3.3 Escribir `Demostracion/semilla_demostracion.sql` (D3: UUID fijos, columnas en el orden de la migración `0001`, sólo datos sintéticos) como recurso embebido, e implementar `SemillaDemostracion`, `SesionesDePrueba` (D4) y `ServicioDemostracion` con `Preparar()` y `Confirmar()` (D5 y D6, capturando sólo `EsquemaDesconocidoException` y `SqliteException`); verificar que pasan las pruebas de 3.2
- [ ] 3.4 Agregar `bunit` `2.12.5` a `Directory.Packages.props` y a `Kiosk.Tests`, y escribir las pruebas de `PantallaDemostracion` con un `ServicioDemostracion` real sobre archivo temporal: un toque muestra AUTHORIZED con identificadores de consumo y comprobante (AT01); un segundo toque muestra DENIED y `LOCAL_LIMIT_REACHED` (AT02); con una base de versión desconocida muestra el error de almacenamiento y no dibuja el botón. Verlas fallar contra el componente vacío de 3.1
- [ ] 3.5 Implementar `PantallaDemostracion.razor` (persona y opción sintéticas fijas, botón «Confirmar», decisión, motivo e identificadores) y registrar `ServicioDemostracion` como singleton en `MainWindow` con la ruta de D5; verificar que pasan las pruebas de 3.4
- [ ] 3.6 Correr `dotnet run --project src/Intelektron.INComedor.Kiosk`, tocar «Confirmar» dos veces y guardar una captura de cada resultado en `C:\AI\INComedor\ai4devs\gestion\evidencias\conectar-puesto-y-receptor\`; verificar que la primera muestra AUTHORIZED y la segunda `LOCAL_LIMIT_REACHED`. Si la ventana no puede abrirse en la sesión, decir cuál comando no se pudo correr y por qué, y dejar la captura como «Tu parte» de David

## 4. Documentación y verificación integrada

- [ ] 4.1 Agregar en `AGENTS.md`, sección «Comandos», `dotnet run --project src/Intelektron.INComedor.Kiosk` (puesto de demostración) y `dotnet run --project src/Intelektron.INComedor.Sync.Api` (receptor esqueleto); agregar en `readme.md` un apartado «Cómo ver la demostración» con esos comandos, la ruta de la base de D5 y lo que el receptor hace y no hace (D8). Verificar que los dos comandos corren tal como quedan escritos: la ventana abre, y `curl -i -X POST http://localhost:<puerto>/api/incomedor/v1/sync-batches` responde 501
- [ ] 4.2 Correr `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln`, y pegar su salida literal: 0 advertencias, 0 errores y todas las pruebas en verde, con su cantidad por proyecto (48 anteriores más las nuevas)
- [ ] 4.3 Comprobar que cada uno de los 10 escenarios del spec tiene al menos un test cuyo `DisplayName` lo nombra, con una lista escenario → test en la respuesta; correr `openspec validate --all --strict` sin errores
- [ ] 4.4 Correr `pwsh -NoProfile -File C:\AI\INComedor\.claude\skills\estado\medir.ps1` y pegar el ítem «scaffolds conectados»: `front->BD si · back->propio si`; correr `git status --short` y comprobar que no aparecen `bin/`, `obj/`, archivos `.db` ni capturas

## Workflow follow-up

- `/openspec-verify-change conectar-puesto-y-receptor` y su salida en la descripción del PR, con la plantilla «¿Qué cambia? ¿Por qué? ¿Cómo probarlo?».
- Commit por grupo de tareas, por rutas, en `feature/entrega-2-DW`; push, PR y merge son de David.
- Archivar con `/openspec-archive-change` en el mismo PR y correr `openspec validate --all --strict`.
