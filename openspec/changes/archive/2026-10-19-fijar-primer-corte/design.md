# Design

## Context

Ver `proposal.md` (Why). El paquete de la entrega 1 diseña el producto completo: 46 RF, 26 RB, 56 AT, 44 entidades y catorce historias hijas, doce de ellas must. Esa documentación se conserva como visión del producto; este design fija qué parte de ella se construye y se demuestra, y con qué límites técnicos, antes de que exista código.

## Goals / Non-Goals

**Goals:**

- Que cada must del corte tenga un papel en el flujo de punta a punta, y que lo que sale del corte quede nombrado como segunda ola.
- Que el modelo de datos del corte sea un subconjunto del diccionario, sin entidades nuevas ni renombradas.
- Que el agente que implemente los changes siguientes lea un contexto corto y vigente (`AGENTS.md`, `openspec/config.yaml`, la spec viva y el change activo).

**Non-Goals:**

- No se estima de nuevo cada unidad U01–U09: cada change trae su talla.
- No se decide el transporte del feed ni la provisión por snapshot: son de la segunda ola.

## Decisions

### D1. Cinco must y una should

Must: HU01.1, HU01.3, HU02.1, HU02.2 sin el escenario F y HU03.1. Should: HU01.2 reducida al código (escenario J). El porqué de cada una está en `proposal.md`.

- **Descartada: conservar las doce must con la regla de recorte.** El punto 4 de la regla decía «no se recorta un must»; con 38 h para 48–96 h, eso dejaba la final sin un flujo terminado. Es el riesgo que marcó LIDR.
- **Descartada: tres must (HU01.1, HU02.1 y HU02.2).** Saca cupo y reimpresión, que la decisión del 18/10 pone en el corte.
- **Descartada: HU01.2 entera como must.** Serían seis must y entraría la biometría, que es segunda ola.

### D2. Identificación simulada por código

Un adaptador de prueba resuelve un código tipeado contra `PersonCredential` del fixture y abre la `IdentitySession`; en pantalla y en la evidencia se rotula como simulado. En el producto, sólo una verificación 1:1 abre la sesión (SDD §7.4); en el corte, el adaptador ocupa ese lugar.

- **Descartada: sesión fija sin código.** No ejercita `PersonCredential` y deja sin base al escenario J de la should.
- **Descartada: verificación 1:1 con un doble biométrico.** Es el contrato del broker, segunda ola.

### D3. Receptor sin el caso entre terminales

TK01.1 conserva AT13, AT27 y AT50; AT12 (hechos incompatibles de dos terminales) pasa a la segunda ola junto con HU02.2-F. La misma `Idempotency-Key` con otro cuerpo sigue respondiendo 409 sin alterar el original.

- **Descartada: mantener AT12.** Exige dos puestos provisionados, que es multipuesto.

### D4. Entidades del corte

El corte usa 27 de las 44 entidades de `docs/modelo-datos.md`, con sus nombres y claves:

| Almacén | Usa el corte | Quedan fuera |
|---|---|---|
| SQLite local | KioskConfiguration (sólo `SINGLE_KIOSK`), ConfigurationSnapshot, PersonReference, PersonCredential, LocalOperatorGrant (sólo `OPERATOR`), ServiceType, QuotaScope, QuotaBucket, MenuItemVersion, MenuQuotaCharge, EntitlementRule (sólo reglas por persona), IdentitySession, ConsumptionIntent, Consumption, ObservedConsumption (sólo `OWN`), ObservedQuotaCharge, QuotaUsage, Ticket, PrintJob, OutboxEvent, SyncBatch, SyncBatchItem y AuditRecord | PersonGroup y GroupMembership (reglas por grupo); PersonExternalKey (integración real con INWeb); ConnectivityIncident, PeerInbox y FeedCursor (multipuesto); LocalSettingsRevision (administración); AuditFileDelivery y LogDeliveryIncident (bitácoras) |
| Receptor central | KioskRegistry, SyncBatchReceipt, InboxEvent y ConsolidatedConsumption | ConfigurationPublication (provisión por snapshot); ReconciliationCase (AT12); ConsumptionFeed, CentralConnectivityIncident, QuotaAuditAlert y AlertConsumption (multipuesto); CentralVendorCost (costo del concesionario) |
| Biometría | — | BiometricTemplateEnvelope |

`AuditRecord` queda en el corte porque RB06 lo escribe en la misma transacción; lo que sale son los archivos mensuales que lo exportan. La configuración se carga desde un fixture sintético como snapshot activo.

- **Descartada: un modelo nuevo y reducido.** Rompe la trazabilidad con el diccionario, los mapas y el OpenAPI que LIDR ya evaluó.
- **Descartada: migrar las 44 entidades en `0001`.** Es el alcance que se está recortando.

### D5. Pantallas sólo Razor, WPF como host

Atención y reimpresión son componentes Razor dentro de un BlazorWebView; WPF es sólo la ventana host. El portal INWeb ya es ASP.NET Core MVC/Razor, así que el puesto comparte tecnología de vistas con el ecosistema. WPF XAML queda como contingencia si el host no cumple el ensayo táctil, no como segunda tecnología.

- **Descartada: pantallas en WPF XAML con Razor sólo para administración.** Dos tecnologías de UI para un autor y seis horas por semana.
- **Descartada: .NET MAUI o WinUI.** No suman frente a WPF en un único equipo Windows y alejan del ecosistema.

### D6. Protección en reposo del corte

- El código de identificación no es un secreto: identifica, no prueba identidad (SDD §7.4). No se cifra; sí queda fuera de logs, tickets y eventos.
- Si el corte usa mTLS con el receptor, la clave privada del certificado del puesto se guarda en el almacén de certificados de Windows, marcada como no exportable.
- La ACL de la base SQLite se limita a la cuenta que corre el puesto.
- El cifrado de disco (BitLocker) es requisito de instalación, no una función del producto.
- Templates biométricos y verificador de la cuenta de mantenimiento quedan fuera del corte.

- **Descartada: SQLCipher u otro cifrado de la base.** Suma una dependencia nativa y la gestión de su clave, para datos sintéticos que ya protegen el disco cifrado y la ACL.
- **Descartada: certificado en un archivo PFX con su contraseña en la configuración.** Deja la clave exportable y el secreto junto a la aplicación.

### D7. La spec viva crece por change

`openspec/specs/` empieza vacía. Cada change agrega su capability con requirements tomados de las RB y escenarios tomados de los AT, y se archiva cuando su código y sus tests están en la rama.

- **Descartada: volcar la SDD en `openspec/specs/`.** Declararía como requisito del corte lo que D1 saca, y le devolvería al agente los 420 KB que LIDR pidió acortar.

### D8. Una sola fuente de contexto para dos herramientas

`AGENTS.md` en la raíz, de 40 a 80 líneas, con sólo lo que no se infiere del código. `CLAUDE.md` es una línea, `@AGENTS.md`; Codex lee `AGENTS.md` directamente.

- **Descartada: enlace simbólico de `CLAUDE.md`.** En Windows pide privilegios o modo desarrollador.
- **Descartada: dos archivos con el mismo contenido.** Divergen con el primer cambio.

### D9. Este change va sin specs

`skip_specs: true`: decide alcance y contexto, no comportamiento. Le corresponde la DoD de documentación de L52: enlaces internos no rotos y revisión humana.

- **Descartada: un requirement de «alcance».** Sería inventar comportamiento para pasar la validación.

## Risks / Trade-offs

- [Un lector encuentra D-C19 y el plan viejo, y cree que las doce must siguen vigentes] → D-C19 se conserva con una nota que la enmienda y remite a D-C24; el plan, el README y la cobertura se alinean en este mismo change.
- [La should HU01.2-J no llega a la entrega 2] → Es la primera en diferirse; si no llega, se declara pendiente, sin tocar las must.
- [La identificación simulada se lee como biometría demostrada] → El adaptador se rotula como simulado en pantalla, en las pruebas y en el README.
- [mTLS agrega complejidad al receptor] → El change `recibir-lotes-idempotente` decide si lo usa; D6 fija dónde va la clave si lo usa.

## Migration Plan

No hay despliegue: es un cambio de documentación en la rama `feature/entrega-2-DW`. Para revertirlo alcanza con revertir su commit; el PR #1 y la rama `feature/entrega-1-DW` no se tocan.

## Open Questions

- ¿El receptor del corte autentica al puesto con mTLS o con otra credencial de prueba? Se decide en `recibir-lotes-idempotente`; no cambia el alcance ni las tareas de este change.
