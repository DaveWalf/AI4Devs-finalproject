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
- Test: `dotnet test INComedor.sln` (vigente).
- Spec: `openspec validate --all --strict` (vigente).
- Puesto de demostración: `dotnet run --project src/Intelektron.INComedor.Kiosk` (ventana WPF; su base queda en
  `%LOCALAPPDATA%\INComedor\demo\`, se reinicia borrando esa carpeta).
- Receptor esqueleto: `dotnet run --project src/Intelektron.INComedor.Sync.Api` (`http://localhost:5000`; responde 501).
- Todos desde la raíz del repo: `global.json` se lee del directorio actual.
