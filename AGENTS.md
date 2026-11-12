# AGENTS.md — INComedor

## Producto

Puesto autónomo de autoservicio de comedor para una All-in-One táctil con Windows 11.
Identifica, aplica cupos, registra el consumo e imprime el comprobante con su propia base SQLite.

## Alcance: primer corte (D-C24)

- Se construye: un puesto, identificación simulada por código, selección, cupo y fecha de negocio,
  confirmación atómica (RB06) con replay por `IntentId`, comprobante con reimpresión, outbox y
  receptor idempotente de lotes.
- Historias must: HU01.1, HU01.3, HU02.1, HU02.2 (sin el escenario F) y HU03.1; should: HU01.2-J.
- No se construye (segunda ola, diseñada y no demostrada): multipuesto y feed, configuración por
  snapshot, administración y diagnóstico, bitácoras mensuales, cuenta de mantenimiento, biometría
  1:N y 1:1 y auditoría de saltos del reloj. Si una tarea la necesita, parar y preguntar.
- `docs/` describe el producto completo: es antecedente, no requisito. El requisito es la spec viva
  de `openspec/specs/` más el change activo.

## Invariantes que el código no muestra

- El puesto autoriza y registra sin red. Ningún acuse central autoriza, deniega ni revierte un
  consumo ya confirmado: el receptor sólo deduplica y consolida.
- Una confirmación es todo o nada (RB06): intención, decisión, consumo, cupo, ticket, trabajo de
  impresión, outbox y `AuditRecord` en una sola transacción SQLite.
- Mismo `IntentId` y mismo contenido devuelven el resultado original sin revalidar; mismo
  `IntentId` con otro contenido es conflicto y no escribe nada.
- La impresión ocurre después del commit. Un fallo de papel nunca crea otro consumo.
- El código de identificación no es un secreto, pero no se escribe en logs, tickets ni eventos.

## Flujo de trabajo con OpenSpec

- Skills: `/openspec-explore`, `/openspec-propose`, `/openspec-apply-change`,
  `/openspec-verify-change`, `/openspec-archive-change` (en Codex, `$openspec-*`).
- Un change por ticket (TK03.1, TK02.1, TK01.1). Proponer y esperar la revisión de David antes
  de aplicar: la revisión del delta no se delega.
- Antes de cada PR: `openspec validate --all --strict` sin errores y la salida de
  `/openspec-verify-change` en la descripción. El change se archiva en el mismo PR.
- Si cambia un requisito, primero se cambia el change; nunca se parchea código sin él.

## Reglas

- No modificar un test existente sin aprobación explícita de David. Cada tarea lo verifica con `git diff --stat -- tests/`.
- Sólo datos sintéticos y cliente anonimizado: ningún dato personal real ni template biométrico.
- Never commit secrets: ni certificados, ni claves, ni cadenas de conexión con credenciales (el job `secretos` de la CI los busca con gitleaks).
- Revisar contra OWASP Top 10 todo lo que toca entrada externa: el receptor de lotes, el
  código tipeado y la configuración que llega al puesto.
- Un test nuevo se ve fallar antes de implementar lo que lo hace pasar.
- SQLite con `Microsoft.Data.Sqlite` y SQL parametrizado, sin ORM en el puesto; pruebas sobre un
  archivo SQLite real temporal, con dobles sólo para impresora y lector.
- Pantallas sólo Razor; WPF es la ventana host del BlazorWebView, no una segunda tecnología de UI.
- El Kiosk y sus pruebas usan `net11.0-windows10.0.17763.0`, no `net11.0-windows`: sin la versión de plataforma,
  WebView2 falla al abrir la ventana (`Microsoft.Windows.SDK.NET`) y las pruebas no referencian al Kiosk (NU1201).
- Commits por rutas, nunca `git add -A`; mensajes en español. Push y merge son de David.

## Comandos

- Build: `dotnet build INComedor.sln -warnaserror` (vigente).
- Test: `dotnet test INComedor.sln` (vigente). Las pruebas del receptor usan SQL Server: `(localdb)\MSSQLLocalDB`,
  u otra instancia en `INCOMEDOR_SQLSERVER`; cada clase crea su base y la borra.
- E2E: `dotnet test tests/Intelektron.INComedor.E2E.Tests` (vigente, ~1 min 45 s, entra también con el test de la
  solución). Lanza el receptor y el puesto compilados y maneja la pantalla con Playwright por CDP; necesita WebView2
  Runtime, SQL Server y escritorio. Cada prueba usa su carpeta, sus puertos y su base, y cierra sus procesos por PID.
- Variables del puesto, inertes sin valor (ver `.env.example`): `INCOMEDOR_DEMO_DIR` (carpeta absoluta de la
  demostración), `INCOMEDOR_RECEPTOR_URL` (sólo `https`) e `INCOMEDOR_CDP_PORT` (1024 a 65535, sólo loopback, sólo
  para pruebas). Paquete: `pwsh .github/scripts/publicar-puesto.ps1 -Version <v>`, que corre el E2E contra el EXE.
- Spec: `openspec validate --all --strict` (vigente).
- Puesto de demostración: `dotnet run --project src/Intelektron.INComedor.Kiosk` (ventana WPF con la atención; la
  pantalla anterior, con `-- --demostracion`). La base y los comprobantes de la impresora simulada quedan en
  `%LOCALAPPDATA%\INComedor\demo\`, que se reinicia borrando esa carpeta. Los códigos sintéticos están en el README.
- Receptor de demostración: `dotnet run --project src/Intelektron.INComedor.Sync.Api -- --demostracion`
  (`https://localhost:5001`, TLS mutuo). Genera certificados sintéticos sin contraseña en
  `%LOCALAPPDATA%\INComedor\demo\certificados\`, que el puesto usa si existen, y la base `INComedorReceptorDemo`
  en la instancia de `INCOMEDOR_SQLSERVER`. Sin `--demostracion` necesita `ConnectionStrings:Receptor` y
  `Receptor:Autoridad`; sin ellos responde 503.
- Todos desde la raíz del repo: `global.json` se lee del directorio actual.
