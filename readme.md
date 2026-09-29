# INComedor

**Primera entrega AI4DEVS · Documentación técnica · 29/09/2026**

INComedor es un puesto autónomo de autoservicio de comedor empresarial para una
All-in-One táctil con Windows 11. Identifica, aplica habilitaciones y cupos,
registra consumos e imprime comprobantes con una base local propia. Funciona
de manera desatendida aunque INWeb esté fuera de línea en el perfil habitual y
con la política inicial. En multipuesto auditado puede configurarse suspensión
de nuevas confirmaciones durante la pérdida de comunicación. INWeb distribuye
información y consolida los hechos cuando se recupera la conexión.

**Estado:** diseño técnico del MVP académico. La implementación y las pruebas
operativas corresponden a las siguientes entregas. La demostración utilizará
datos sintéticos y adaptadores de prueba para INWeb y dispositivos.

## Índice

0. [Ficha del proyecto](#0-ficha-del-proyecto)
1. [Descripción general del producto](#1-descripción-general-del-producto)
2. [Arquitectura del sistema](#2-arquitectura-del-sistema)
3. [Modelo de datos](#3-modelo-de-datos)
4. [Especificación de la API](#4-especificación-de-la-api)
5. [Historias de usuario](#5-historias-de-usuario)
6. [Tickets de trabajo](#6-tickets-de-trabajo)
7. [Pull requests](#7-pull-requests)
8. [Uso de IA y cobertura](#8-uso-de-ia-y-cobertura)

## 0. Ficha del proyecto

| Campo | Contenido |
|---|---|
| Autor | David Walfisch |
| Nombre | INComedor |
| Descripción breve | Kiosco autónomo de comedor, táctil, con identificación, reglas locales, ticket y sincronización. |
| Organización | INTELEKTRON. Caso empresarial con cliente anonimizado y datos sintéticos. |
| URL del proyecto | Aplicación de escritorio Windows con ejecución local; no corresponde una URL de aplicación en esta etapa. |
| URL o ZIP del repositorio | [DaveWalf/AI4Devs-finalproject](https://github.com/DaveWalf/AI4Devs-finalproject), rama `feature/entrega-1-DW`. |
| Etapa | Entrega 1: documentación técnica. |
| Modalidad del MVP | Adaptadores de prueba para servicios y dispositivos; persistencia y reglas reales en la implementación académica. |

## 1. Descripción general del producto

### 1.1. Objetivo

Permitir que una persona habilitada elija y registre un servicio sin intervención
habitual del operador ni conexión obligatoria con el servidor. El operador
podrá recuperar comprobantes e identificar incidencias sin duplicar consumos.
Administración recibirá posteriormente los hechos consolidados.

Identificar no concede por sí solo permiso de consumo. Registrar y emitir un
ticket tampoco acredita el retiro físico de la comida: el canje en la línea es
una ampliación independiente.

### 1.2. Características y funcionalidades principales

El MVP académico comprende **cuatro funcionalidades obligatorias**. Las cuatro
integran el compromiso de la entrega final; su secuencia y aceptación figuran en el
[plan académico](docs/plan-academico.md).

| ID | Funcionalidad | Resultado previsto para las etapas ejecutables |
|---|---|---|
| F01 | Identificación y selección | Identidad local y catálogo extensible según habilitaciones sincronizadas. |
| F02 | Autorización y registro local | Reglas y cupos en SQLite; un único consumo ante doble toque, retry o reinicio. |
| F03 | Comprobante y recuperación | Ticket tras commit local; recuperar impresión sin registrar otra comida. |
| F04 | Sincronización y supervisión | Configuraciones íntegras, envío de hechos, acuses idempotentes, interfaz administrativa y diagnóstico por componente. |

El producto requiere serie virtual y BioMini Suprema USB. La biometría contempla
**1:N contra templates de personas habilitadas** y **código previo + verificación
1:1**. Ambos devuelven el ID estable de la persona. Los adaptadores de prueba del
MVP representan esos contratos, sin demostrar precisión biométrica ni integración
física real.

El caso habitual y la demostración inicial son de un puesto autónomo. En instalaciones
con varios puestos y auditoría, todos informan a INWeb en línea y actualizan
inmediatamente su registro con los consumos de sus pares. Puede haber una ventana
concurrente de exceso: debe generar un aviso auditable, sin prometer exclusión global.
La falla de conectividad de ese perfil se configura por instalación: continuar
localmente (valor inicial), registrando la incidencia, o suspender nuevas
confirmaciones hasta recuperar el intercambio y el feed. Al reconectar se
concilian hechos y se avisa de los excesos; los consumos previos permanecen intactos.
Quedan fuera del MVP liquidación de haberes, nómina productiva, conciliación económica
con concesionario, reservas, pagos, inventario, invitados automáticos, enrolamiento
productivo, emisión privilegiada para terceros y canje físico. F04 recupera y sincroniza hechos operativos, no completa
esos circuitos empresariales.

La supervisión incluye una interfaz de configuración y diagnóstico para el
Administrador del Sistema. Se requieren dos bitácoras externas de texto plano,
general e interacciones, mensuales con AAAAMM; ante fallo del archivo se continúa
con respaldo durable SQLite y se reconstruye señalando el período degradado.
[Configuración, diagnóstico y bitácoras](docs/administracion-y-bitacoras.md).

El puesto toma la hora de Windows y calcula el día de negocio con la zona y el
corte configurados. Si detecta un salto del reloj, continúa operando y registra
el cambio para auditoría; no aplica un piso temporal ni una suspensión adicional.
Una hora incorrecta puede afectar el día asignado y el cupo disponible. La
supervisión muestra la incidencia y la reconciliación conserva los hechos originales.
Esta política es independiente de la suspensión por conectividad de D-A02.

### 1.3. Diseño y experiencia de usuario

Recorrido: **reposo → identificación local → selección → confirmación → registro
local → ticket → reposo**. La sincronización ocurre aparte.

![Flujo y recuperación](diagrams/flujo.svg)

Los [bocetos de pantallas](docs/experiencia.md) son diseños, no capturas de software.
Incluyen cancelación, rechazo por cupo, huella no coincidente, falta de papel y
recuperación. Una nueva lectura no sustituye la identidad de una sesión ocupada.

### 1.4. Instrucciones de instalación

Para consultar esta entrega, abrir este README y sus anexos. Las instrucciones
ejecutables de instalación se incorporarán con el código de la entrega 2.

La demostración se diseñará para Windows 11, .NET 10, WebView2 y almacenamiento local
y datos sintéticos. El primer provisionamiento podrá hacerse por sincronización
o paquete local validado. Con base provisionada, el arranque y la operación no
exigirán INWeb en el perfil autónomo y con continuidad local. El modo
multipuesto con suspensión conserva el arranque, diagnóstico y recuperación
locales, pero no admite nuevos consumos hasta recuperar la comunicación. Sin ninguna configuración inicial válida, el puesto informará
que requiere provisionamiento y mantendrá deshabilitadas las confirmaciones.

La entrega de código agregará versiones exactas, comandos probados, carga del
conjunto sintético y recuperación.

## 2. Arquitectura del sistema

### 2.1. Diagrama de arquitectura

![Contexto autónomo](diagrams/contexto.svg)

El puesto tiene **autoridad operativa local** sobre identidad, reglas, contador,
consumo y ticket. INWeb administra padrón y políticas y mantiene la vista
consolidada. Se utiliza la última configuración íntegra, respetando sus vigencias
explícitas, sin caducidad arbitraria que obligue a conectarse.

La arquitectura por capas separa dominio, dispositivos y transporte. Favorece
pruebas y autonomía, a costa de mantener dependencias nativas y resolver
consistencia eventual. [Justificación y alternativas](docs/arquitectura.md).

### 2.2. Descripción de componentes principales

| Componente | Tecnología prevista | Responsabilidad |
|---|---|---|
| Kiosco táctil | C#/.NET 10, WPF + Blazor Hybrid | Pantallas, accesibilidad y sesión. |
| Aplicación y dominio | Bibliotecas C# | Identidad operativa, habilitación, cupo e idempotencia local. |
| Administración y diagnóstico | UI local + casos de uso con permisos | Configuración de equipo, pruebas aisladas e inventario de versiones. |
| Bitácoras | Archivos UTF-8 mensuales + escritor recuperable | General e interacciones separadas, respaldo durable y reconstrucción. |
| Persistencia local | SQLite + Microsoft.Data.Sqlite directo | Configuración, contador, consumos, impresión y outbox. |
| Adaptadores | Serie, biometría, impresión y simuladores | Entradas y resultados independientes de reglas. |
| Broker BioMini | Proceso local y SDK homologado | Captura y matching 1:N/1:1 sobre padrón protegido. |
| Sincronizador | Worker C# | Configuración y envío de hechos con IDs estables. |
| API central | ASP.NET Core 10 REST/HTTPS | Configuración y recepción idempotente de lotes. |
| Consolidación | Adaptador INWeb, EF Core y SQL Server | Hechos centrales, acuses y conflictos. |

Las operaciones SQLite se ejecutarán en una cola de trabajo fuera de la interfaz;
la aplicación mantendrá la sesión estable mientras persiste la transacción.

### 2.3. Descripción de alto nivel y estructura de ficheros

Documentación de esta entrega:

```text
readme.md                       portada con las secciones de la plantilla
prompts.md                      registro de uso de IA
docs/
  especificacion-sdd.md         requisitos RF, reglas RB, aceptación AT y calidad RNF
  historias-y-tickets.md        épicas, historias de tamaño real y tickets de tarea
  plan-academico.md             prioridad, tallas, hitos y matriz de aceptación
  arquitectura.md               capas, decisiones ADR, protección y despliegue
  modelo-datos.md               diccionario de datos y ER de entidades y claves
  nomenclatura-integracion.md   convención de nombres compartida con INWeb
  nomenclatura-entidades.csv    mapa de entidades a tablas y clases
  nomenclatura-campos-er.csv    mapa de atributos a columnas y propiedades
  administracion-y-bitacoras.md administración local, diagnóstico y bitácoras
  experiencia.md                bocetos de pantallas y mensajes
  pruebas.md                    plan de pruebas por nivel
  decisiones.md                 decisiones del autor y condiciones del diseño
  cobertura-entrega.md          relación entre la consigna y cada artefacto
api/openapi.yaml                contrato de las tres operaciones de sincronización
diagrams/                       fuentes .dot y .mmd editables y sus SVG
```

Estructura prevista de la solución, la misma en todos los documentos. Las carpetas
de código todavía no existen:

```text
src/
  Intelektron.INComedor.Domain/                reglas, cupos, intención y consumo, sin dependencias externas
  Intelektron.INComedor.Application/           casos de uso y control de permisos locales
  Intelektron.INComedor.Contracts/             DTO del JSON versionado, con nombres explícitos
  Intelektron.INComedor.Infrastructure.Sqlite/ migraciones, transacción RB06, outbox y bitácoras
  Intelektron.INComedor.Kiosk/                 host WPF + Blazor Hybrid: atención, supervisión y administración
  Intelektron.INComedor.Biometrics.Broker/     proceso aparte que aísla el SDK de BioMini
  Intelektron.INComedor.Devices/               adaptadores de serie e impresora y dobles de prueba de dispositivos
  Intelektron.INComedor.Sync.Api/              receptor ASP.NET Core de configuración y lotes
  Intelektron.INComedor.Integration.INWeb/     adaptador a INWeb y consolidación con EF Core y SQL Server
tests/                                         un proyecto de pruebas por proyecto de src, más el E2E
```

### 2.4. Infraestructura y despliegue

![Despliegue académico](diagrams/despliegue.svg)

El puesto Windows ejecutará la interfaz, los casos de uso y los adaptadores.
SQLite residirá en disco local, con acceso exclusivo de la aplicación del puesto.
La API académica ASP.NET Core se ejecutará en un entorno de prueba y consolidará
en SQL Server; representará los contratos de integración con INWeb.

La puesta en marcha prevista comprende:

1. Instalar la aplicación, .NET y WebView2 y preparar directorios con permisos.
2. Crear la base y cargar el conjunto sintético y una configuración íntegra.
3. Activar los adaptadores de prueba y verificar diagnóstico, identidad y catálogo.
4. Ejecutar el flujo local con red ausente y comprobar persistencia y comprobante.
5. Habilitar la API de prueba y verificar sincronización, acuses y recuperación.

Cada puesto conserva su archivo SQLite; la sincronización intercambia hechos
por API. Actualizaciones, reinicios y restauración preservan eventos pendientes.
La integración física y el despliegue productivo requieren homologación.

La demostración final conserva ambas políticas multipuesto y su cambio como must. Sólo diagnóstico ampliado y escalas 125/150 % pueden diferirse. El [plan](docs/plan-academico.md#capacidad-y-reestimación) registra 6 h semanales, M0 objetivo 06/10 y viabilidad todavía no demostrada. Cuenta local de mantenimiento y sus procedimientos se especifican en M0; no equivalen a integración física homologada.

### 2.5. Seguridad

Controles diseñados: identidad de terminal y HTTPS para sincronizar, credenciales
por código con vigencia y revocación, permisos locales por rol para operador y
administrador, separación de secretos y biometría, minimización, auditoría y
protección local. Credenciales y permisos llegan en el snapshot; el operador se
autentica sin red con código y verificación 1:1 o cuenta individual local de mantenimiento, y cada acción privilegiada se
comprueba y se audita ([SDD §7.4](docs/especificacion-sdd.md#74-credenciales-y-control-de-acceso-local)). Un fallo de autenticación con INWeb afecta la sincronización, no sustituye
las reglas de autorización locales.

Se proponen ACL y cifrado de disco más protección específica para templates.
SQLite estándar no se considera cifrada por defecto. No se guardarán imágenes de
huellas ni templates en logs. Retención, acceso, enrolamiento y revocación productivos
se definirán y verificarán antes de una instalación productiva.

### 2.6. Tests

El [plan de pruebas](docs/pruebas.md) cubre dominio, SQLite real, sincronización,
contratos, E2E y dispositivos. Casos centrales: arranque offline, cupo local,
doble confirmación, caída tras commit, reimpresión, snapshot parcial, acuse perdido
y ambos modos biométricos. Incluye también saltos del reloj, cálculo del día de
negocio y vigencias. En esta entrega los casos están especificados; su ejecución
y evidencia se incorporarán con el producto.

## 3. Modelo de datos

INComedor adopta las convenciones de datos y C# del ecosistema INWeb. El
[mapa lógico/físico/C#](docs/nomenclatura-integracion.md) define tablas
`tbComedor...`, columnas mnemónicas y negocio en español. Los DTO mantienen
el contrato JSON; las claves nativas de INWeb y los UUID del módulo conservan
su tipo y ámbito. Es diseño de implementación, sin afirmar tablas ya creadas.

### 3.1. Diagrama del modelo de datos

![Modelo lógico resumido](diagrams/datos-resumen.svg)

[Diagrama ER de entidades y claves (ampliable)](diagrams/datos-er.svg), [fuente editable](diagrams/datos.mmd) y [diccionario](docs/modelo-datos.md),
con tipos, claves, relaciones y restricciones. SQLite contiene la autoridad local
del consumo; la consolidación deduplica por identidad de origen sin volver a autorizar.

### 3.2. Descripción de entidades principales

El siguiente resumen conserva los nombres y restricciones del
[diccionario completo](docs/modelo-datos.md). UUID identifica entidades y hechos;
PK es clave primaria, FK clave foránea y UQ unicidad. Las referencias a padrón,
menú y reglas incluyen su snapshot para conservar la interpretación histórica.

| Entidad | Atributos esenciales y tipos | Claves y restricciones |
|---|---|---|
| PersonReference | SnapshotId, PersonId: UUID; DisplayName: texto; Active: booleano; IdentityMaterialVersion: entero | PK (SnapshotId, PersonId); FK snapshot; estado activo separado del derecho a consumir. |
| PersonCredential | SnapshotId, CredentialId, PersonId: UUID; Kind: enum; NormalizedValue: texto; ValidFromUtc, ValidToUtc, RevokedAtUtc: fecha-hora UTC opcionales | PK (SnapshotId, CredentialId); FK persona; código único por snapshot incluso revocado; no reasignable entre personas, con historial pendiente de concretar en M0; vencida y revocada tienen motivos propios. |
| ServiceType | SnapshotId, ServiceTypeId: UUID; Label: texto | PK (SnapshotId, ServiceTypeId); FK snapshot; un servicio puede tener distintas variantes de menú. |
| ConsumptionIntent | IntentId: UUID; IdentitySessionId: token opcional; PayloadHash: bytes(32); Status: enum; DecisionId: UUID opcional; ResultPayload: JSON opcional | PK IntentId; FK sesión cuando existe; UQ DecisionId cuando existe. Mismo ID y contenido recuperan el resultado durable. |
| Consumption | ConsumptionId, IntentId, SnapshotId, PersonId, MenuItemId: UUID; BusinessDate: fecha; Quantity, MenuVersion, PolicyVersion, CatalogVersion: enteros; ClientSequence: entero de 64 bits; PriceSnapshot: JSON | PK ConsumptionId; UQ IntentId; FK intención/persona/menú versionados; precio y versiones inmutables. El ticket lo referencia desde Ticket. |
| QuotaUsage | PersonId, QuotaScopeId, QuotaBucketId: UUID; BusinessDate: fecha; Used, Version: enteros | PK persona/ámbito/categoría/fecha, igual que la clave de cupo de RB03; Used no negativo; proyección de consumos propios y de pares ya recibidos. |
| Ticket | TicketId, ConsumptionId: UUID; PrintData: JSON; CreatedAtUtc: fecha-hora UTC | PK TicketId; UQ/FK ConsumptionId: un ticket lógico por consumo. |
| OutboxEvent | EventId: UUID; IntentId y TicketId: UUID opcionales; EventType, State: enum; PayloadVersion: entero; Payload, PayloadHash | PK EventId; referencias de intención/ticket cuando corresponda; mismo ID y contenido en reenvíos. |
| AuditRecord | AuditId, KioskId, GenerationId, BootId; Sequence: entero de 64 bits; OccurredAtUtc, RecordedAtUtc: fecha-hora; Context: JSON | PK AuditId; UQ (KioskId, GenerationId, Sequence); registro inmutable; decisión y auditoría comparten transacción. |

Los consumos observados de pares tienen deduplicación propia antes de afectar
QuotaUsage. Ticket y PrintJob separan el comprobante lógico de cada copia y del
resultado físico. Los templates se mantienen en el almacén protegido del broker,
fuera de consumos, payloads y bitácoras.

## 4. Especificación de la API

El [OpenAPI 3.1](api/openapi.yaml), versión 0.6.0-draft, define tres operaciones por construir:

| Operación | Propósito |
|---|---|
| `GET /api/incomedor/v1/configuration` | Configuración versionada completa, con credenciales y permisos locales, para activar localmente; 304 si no cambió y 404 si todavía no hay publicación. |
| `POST /api/incomedor/v1/sync-batches` | Lotes de hechos locales con acuses por evento. |
| `GET /api/incomedor/v1/sync-batches/{batchId}` | Recuperar el resultado del lote ante respuesta perdida. |

La autorización del consumo pertenece al puesto. Estas tres operaciones
seleccionadas cubren configuración y consolidación. El diccionario define además
los campos de la distribución biométrica y de la difusión de consumos entre
puestos; su transporte se especificará en M0. Administración y diagnóstico son
casos de uso locales.

## 5. Historias de usuario

Las tres épicas HU01–HU03 cubren F01–F04 y se descomponen en historias de tamaño
real. Estas son las tres historias principales; el
[backlog de historias hijas](docs/historias-y-tickets.md#backlog-de-historias-hijas)
completa los 35 escenarios con persona, talla y prioridad, y el
[plan académico](docs/plan-academico.md) ordena su ejecución.

| Épica | Objetivo | Personas | Historias hijas |
|---|---|---|---|
| HU01 | Identificarse y consumir de forma autónoma | Comensal | HU01.1–HU01.3 |
| HU02 | Recuperar y sincronizar consumos sin perder autonomía | Soporte técnico y Administrador de personal | HU02.1–HU02.6 |
| HU03 | Supervisar el puesto y recuperar un comprobante | Operador y Administrador del Sistema | HU03.1–HU03.5 |

### HU01.1 — Confirmar un servicio sin red y sin duplicarlo

**Épica:** HU01 · **Persona:** Comensal · **Prioridad:** must · **Talla:** M · **Tickets:** TK02.1 (interfaz), TK03.1 (datos) y TK01 (dominio, principal de AT01).

**Como** Comensal, **quiero** elegir un servicio del catálogo y confirmarlo en el puesto aunque no haya red, **para** recibir mi comprobante sin que un toque repetido, una cancelación o un cambio de precio alteren lo que consumí.

**Precondiciones:** base provisionada con el fixture sintético, sesión de identidad abierta por el adaptador de prueba (la identificación es HU01.2) y perfil monopuesto o `CONTINUE_LOCAL`.

| Escenario | Dado | Cuando | Entonces |
|---|---|---|---|
| HU01-A | Persona sintética habilitada, cupo local libre y red ausente | Confirma servicio | Una decisión autorizada, consumo/cupo/evento locales y ticket lógico |
| HU01-B | Catálogo de 15 opciones | Navega y cancela antes de confirmar | Navegación completa y cero consumo |
| HU01-D | Catálogo cambió entre mostrar y confirmar | Confirma valor anterior | Se informa el cambio y se solicita reconfirmación; no cambia el importe silenciosamente |
| HU01-G | Operación confirmándose | Se repite toque o llega otra lectura | Conserva persona/intención; no duplica consumo |

**Terminado:** una persona sintética confirma un servicio con la red desconectada y recibe su comprobante; cancelar no deja consumo, un segundo toque no crea otro y un cambio de precio obliga a reconfirmar.

### HU02.1 — Recuperar el resultado después de una caída

**Épica:** HU02 · **Persona:** Soporte técnico · **Prioridad:** must · **Talla:** S · **Tickets:** TK03.1 (datos) y TK01 (contrato de replay/conflicto, principal de AT25).

**Como** Soporte técnico, **quiero** que el puesto recupere tras un reinicio el resultado de una confirmación ya registrada, **para** resolver la caída sin cobrar dos veces ni perder el comprobante.

**Precondiciones:** base provisionada y una confirmación ya guardada por la transacción local.

| Escenario | Dado | Cuando | Entonces |
|---|---|---|---|
| HU02-B | Consumo confirmado localmente | El proceso cae antes de mostrar el resultado | Recupera mismos IDs y un solo trabajo original; no vuelve a consumir cupo |
| HU02-L | Decisión local durable y posterior vencimiento de sesión, baja o cambio de snapshot | Reintenta misma intención/ámbito/payload | Recupera resultado original sin repetir validación de negocio ni crear consumo/ticket/trabajo; una intención nueva sí usa reglas actuales |

**Terminado:** después de cortar el proceso en cada punto de la confirmación, el puesto vuelve a mostrar el mismo resultado con los mismos identificadores, sin un segundo consumo, y rechaza reutilizar la intención con otro contenido.

### HU02.2 — Reenviar lotes y consolidarlos sin duplicar

**Épica:** HU02 · **Persona:** Soporte técnico · **Prioridad:** must · **Talla:** M · **Tickets:** TK01.1 (receptor, principal de AT27) y TK01/TK03 (cliente sincronizador y cola local de U05).

**Como** Soporte técnico, **quiero** que un lote reenviado después de perder el acuse se reconozca como el mismo, **para** recuperar los pendientes sin duplicar consumos en la consolidación ni ocultar conflictos.

**Precondiciones:** receptor académico con su base de prueba y eventos pendientes en el puesto.

| Escenario | Dado | Cuando | Entonces |
|---|---|---|---|
| HU02-C | Hay eventos pendientes | Vuelve la conexión | Envía lote con IDs estables; el puesto sigue atendiendo durante sincronización |
| HU02-D | El servidor recibió el lote pero se perdió el ACK | Se reenvía o consulta el mismo lote | Deduplicación central; no se crea otro consumo ni se pierde el evento local |
| HU02-F | Servidor detecta discrepancia de cupo entre puestos | Concilia los hechos | Conserva ambos, informa conflicto y pide revisión; no niega retroactivamente una comida |
| HU02-G | Credencial de sincronización vencida | Se intenta enviar lote | Error técnico visible y evento retenido; en monopuesto sigue atención; nueva operación multi puesto aplica CONTINUE_LOCAL o SUSPEND_NEW_CONSUMPTIONS persistido |

**Terminado:** el receptor devuelve el mismo acuse ante reenvío/consulta, detecta contenido incompatible y rechaza hechos ajenos; el cliente mantiene atención según D-A02, retiene eventos ante error y avanza su cola sólo por acuse durable. Se conserva el consumo local.

## 6. Tickets de trabajo

Tres tickets de tamaño tarea, uno por capa, con dependencias disponibles al
iniciarlos, un responsable y sus propios criterios de aceptación. Pertenecen a las
épicas técnicas TK01 (backend), TK02 (interfaz) y TK03 (datos). El
[detalle completo](docs/historias-y-tickets.md#tickets-de-trabajo) incluye trabajo,
archivos previstos y exclusiones; la [matriz de aceptación](docs/plan-academico.md#matriz-de-aceptación)
asigna cada AT a un único responsable.

### TK03.1 — Datos: migración inicial y transacción de confirmación

**Unidad:** U02 · **Hito:** M1 · **Talla:** M · **Historias:** HU01.1, HU01.3 y HU02.1 · **Responsable:** DW

**Objetivo:** crear la primera migración SQLite y la unidad de trabajo que guarda una decisión en una sola transacción (RB06), para que la confirmación sea todo o nada y se recupere sin duplicarse.

**Trabajo incluido:** migración numerada `0001` con las tablas de la transacción y las de configuración que referencian; apertura con claves externas activas y versión de esquema comprobada; búsqueda previa por `IntentId` y hash; unidad de trabajo con `BEGIN IMMEDIATE` que reconsulta intención, snapshot y cupo, invoca al dominio y guarda su decisión dentro de la misma transacción; unicidades del modelo.

**Dependencias disponibles:** [diccionario](docs/modelo-datos.md) y mapa de nombres, fixture sintético del [OpenAPI](api/openapi.yaml) y contrato de intención cerrado en M0 (U01). Para cerrar AT03 se requiere el evaluador de dominio de TK01 integrado; TK03.1 conserva la responsabilidad de su evidencia.

**Aceptación:** AT03 (último cupo con dos confirmaciones simultáneas), AT05 (caída después del commit) y AT35 (repetición de una intención ya decidida).

**Terminado:** la migración crea una base vacía válida y los tres AT pasan contra un archivo SQLite real, con el comando y su salida registrados. El ensayo de caída de proceso no acredita resistencia a la pérdida física del disco.

### TK02.1 — Interfaz: selección y confirmación con sus estados

**Unidad:** U03 · **Hito:** M1 · **Talla:** M · **Historia:** HU01.1 · **Responsable:** DW

**Objetivo:** construir en WPF + Blazor Hybrid las pantallas de catálogo, confirmación y resultado sobre el caso de uso local, con estados que impidan confirmar dos veces o cambiar de persona durante la confirmación.

**Trabajo incluido:** estados reposo, selección, confirmando, resultado y regreso a reposo; catálogo paginado de 15 opciones por toque y teclado; cancelación y timeout de 30 s; un `IntentId` por confirmación; reconfirmación ante cambio de precio; mensajes por resultado.

**Dependencias disponibles:** para construir componentes, caso de uso con doble, fixture y mensajes de M0; para cerrar los AT y la demostración, TK01 y U02/TK03.1 integrados con SQLite real. M0 solo no basta para el cierre integral.

**Aceptación:** AT16 (catálogo y timeout sin consumo), AT26 (doble toque y lectura nueva) y AT47 (reconfirmación ante cambio de precio).

**Terminado:** los tres AT pasan en pruebas de componentes y en una demostración grabada sobre el equipo declarado, con persistencia local real del consumo.

### TK01.1 — Backend: receptor idempotente de lotes

**Unidad:** U05 · **Hito:** M1 · **Talla:** M · **Historia:** HU02.2 · **Responsable:** DW

**Objetivo:** implementar en el receptor académico ASP.NET Core `POST` y `GET` de lotes del [OpenAPI](api/openapi.yaml), sobre SQL Server de prueba, sin duplicar hechos ante reintentos.

**Trabajo incluido:** autenticación del equipo y lotes limitados a su ámbito; `Idempotency-Key` igual a `BatchId` y hash RFC 8785 del cuerpo; recibo del lote y eventos de entrada en una transacción, con un resultado por `EventId`; validación de la fecha de negocio de las cargas; caso de conciliación ante hechos incompatibles; consulta del acuse original.

**Dependencias disponibles:** OpenAPI 0.6.0-draft y certificados sintéticos de M0. El cierre integral de AT13/27 requiere cliente sincronizador y cola SQLite de U05, desarrollados por TK01/TK03; TK01.1 coordina la evidencia del receptor y del recorrido cliente-servidor.

**Aceptación:** AT12 (hechos incompatibles de dos terminales), AT13 (credencial inválida o lote ajeno), AT27 (acuse perdido y reenvío) y AT50 (cargas con otra fecha de negocio).

**Terminado:** pruebas de contrato del receptor contra SQL Server y ejemplos OpenAPI válidos; además AT13 y AT27 integran cliente y cola SQLite: error retiene eventos, ACK perdido permite reenvío y sólo el acuse durable avanza la cola. La prueba aislada del receptor no cierra HU02.2.

## 7. Pull requests

Las tres revisiones de código representativas se incorporarán durante las entregas
de implementación, con sus enlaces y resultados. Esta primera entrega documenta el diseño.

## 8. Uso de IA y cobertura

[Registro de IA](prompts.md): herramientas, modelos, subagentes, prompts
representativos y correcciones humanas que determinaron el diseño.

[Cobertura de E1](docs/cobertura-entrega.md): relación entre la consigna y cada
artefacto. [Plan académico](docs/plan-academico.md): prioridades, secuencia y
evidencia prevista para las siguientes entregas.
[Especificación SDD](docs/especificacion-sdd.md): requisitos, reglas y aceptación.
