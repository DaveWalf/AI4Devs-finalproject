# Decisiones de producto y arquitectura

Versión documental: 1.1 · Fecha: 29/09/2026.

Este registro distingue decisiones del MVP académico y condiciones necesarias
para una futura integración productiva. Los identificadores no se reutilizan:
D-C07 reservado, sin asignar.

## Decisiones confirmadas

| ID | Decisión | Consecuencia |
|---|---|---|
| D-C01 | All-in-One táctil Windows 11 y C# compatible con INWeb. | Aplicación local sobre .NET y arquitectura por capas. |
| D-C02 | Serie virtual y BioMini Suprema USB. | Contratos de adaptadores; el MVP usa dobles de prueba y la integración física requiere homologación. |
| D-C03 | Templates habilitados, identificación 1:N y código seguido de verificación 1:1. | Dos flujos independientes con devolución del ID de la persona. |
| D-C04 | MVP académico con adaptadores de prueba. | La lógica, persistencia y recuperación se implementan realmente; la integración real con INWeb y el lector BioMini corresponde a una etapa productiva. |
| D-C05 | Autonomía desatendida con las últimas reglas locales. | SQLite conserva la autoridad del consumo; INWeb configura y consolida eventualmente. |
| D-C06 | Autor: David Walfisch. | Proyecto académico individual. |
| D-C08 | Multipuesto auditado informa en línea y actualiza inmediatamente los registros de los pares. | Difusión, registro local deduplicado y aviso de exceso ante una ventana concurrente. |
| D-C09 | Iniciales DW, marcas de producto reales y cliente anonimizado. | Datos sintéticos; se excluyen identidad del cliente y datos operativos privados. |
| D-C10 | D-A02: política configurable por instalación, con continuidad local como valor inicial. | CONTINUE_LOCAL mantiene atención con incidencia; SUSPEND_NEW_CONSUMPTIONS suspende nuevas confirmaciones mientras no se informa a INWeb o no se recibe el feed de pares. La política persiste y conserva consumos y comprobantes previos. |
| D-C11 | Interfaz para el Administrador del Sistema, configuración y diagnóstico de componentes. | F04 incluye interfaz y matriz de pruebas con adaptadores; hardware real por homologar. |
| D-C12 | Emisión privilegiada futura para terceros internos y externos, con qué/para quién/cuándo/observaciones. | Operador y beneficiario separados; fuera del MVP. |
| D-C13 | Dos bitácoras completas, general e interacciones, externas a DB en texto plano mensual AAAAMM. | Archivos separados y correlacionados; auditoría transversal desde el MVP. |
| D-C14 | Ante fallo del archivo, continuar con respaldo durable en SQLite y reconstruir señalando el intervalo degradado. | Cola recuperable por destino; si SQLite tampoco persiste, se impiden consumos nuevos. |
| D-C15 | Tickets para terceros: sólo reglas del beneficiario, sin excepciones de cupo. | El privilegio de emisión conserva derechos/cupos; internos y externos requieren identidad y habilitación. |
| D-C16 | Las cuatro funcionalidades F01–F04 son obligatorias para el MVP académico. | Identificación/selección, autorización/registro, comprobante/recuperación y sincronización/supervisión forman el compromiso completo de la entrega final. La secuencia está en el plan académico. |
| D-C17 | Usar la hora de Windows y continuar operando ante saltos del reloj, registrándolos para auditoría. | El día de negocio usa la zona y el corte configurados. Se acepta el riesgo de fecha/cupo si la hora es incorrecta; no se aplica un piso temporal ni una suspensión adicional por reloj. Se conservan hechos originales y la política de conectividad D-A02. |
| D-C18 | Adoptar la convención del ecosistema INWeb en persistencia y C#, con mapa explícito y contrato JSON vigente. | Perfil aprobado por el autor: tablas `tbComedor...`, columnas mnemónicas y dominio español; preservar UUID, claves compuestas y referencias externas. [Nomenclatura e integración](nomenclatura-integracion.md), ADR11, RNF15 y AT51; detalle físico en M0. |
| D-C19 | Prioridad y capacidad confirmadas por David el 29/09/2026 en la conversación de decisiones. | Ambas políticas multipuesto y su cambio son must para la final. Sólo diagnóstico ampliado y escalas 125/150 % pueden diferirse. Capacidad: 6 h semanales hasta 22/10 y hasta 12/11; M0 objetivo 06/10. Las tallas siguen siendo estimaciones técnicas, no una viabilidad aprobada. Ver [plan académico](plan-academico.md#capacidad-y-reestimación). |
| D-C20 | Códigos no reasignables entre personas y sin revocaciones programadas a futuro, confirmado por David el 29/09/2026. | Conservar titularidad histórica; revocación efectiva al activar el snapshot que la comunica. Vencimientos previstos mediante ValidToUtc; demora offline aceptada. Ámbito y persistencia histórica por cerrar en M0. |
| D-C21 | Dos roles sin herencia, confirmado por David el 29/09/2026. | OPERATOR reimprime con motivo; SYSTEM_ADMINISTRATOR configura, diagnostica y exporta. Una persona puede tener ambos; Soporte usa administrador en el MVP. |
| D-C22 | Mantenimiento mediante usuario y contraseña individual local, confirmado por David el 29/09/2026. | Alternativa sin INWeb ni lector; misma autorización por acción, límites de intentos y auditoría. No habilita consumo ni reimpresión sin OPERATOR. Cuenta sintética en MVP; provisión, almacenamiento y recuperación se especifican en M0. |
| D-C23 | Credencial todavía no vigente: IDENTITY_NOT_RESOLVED externo, confirmado por David el 29/09/2026. | Causa interna diferenciada de código desconocido, sin registrar el código en claro; no abre selección ni consumo. |

## Condiciones del diseño académico

| ID | Aspecto | Definición |
|---|---|---|
| D-A01 | Identificación académica y privacidad | David Walfisch / DW; cliente anonimizado y datos sintéticos, conforme a D-C09. |
| D-A02 | Pérdida de conexión en multipuesto auditado | D-C10 establece continuidad local inicial y suspensión alternativa. La decisión local y los hechos confirmados se conservan. |
| D-A03 | Repositorio académico | [DaveWalf/AI4Devs-finalproject](https://github.com/DaveWalf/AI4Devs-finalproject), rama `feature/entrega-1-DW`. |
| D-A04 | Conjunto de prueba | ALMUERZO compartido entre variantes, corte 00:00, zona argentina y quince opciones de menú; valores sintéticos definidos en el plan de pruebas. |

## Selección técnica del MVP

- **Interfaz:** WPF + Blazor Hybrid sobre C#/.NET 10, con WebView2.
- **Persistencia local:** SQLite con Microsoft.Data.Sqlite directo, SQL parametrizado
  y transacciones explícitas para controlar el registro atómico y la recuperación.
  Las operaciones se ejecutarán en una cola de trabajo fuera del hilo de interfaz.
- **Integración académica:** API REST/HTTPS ASP.NET Core, EF Core y SQL Server para la
  consolidación. El servicio de prueba representa los contratos de INWeb.
- **Dispositivos:** adaptadores separados del dominio; broker biométrico para
  aislar el SDK y sus dependencias nativas.
- **Sincronización:** snapshots íntegros, outbox, deduplicación, acuses durables y
  difusión de hechos entre puestos.
- **Impresión:** trabajo durable posterior a la transacción del consumo.
- **Bitácoras:** UTF-8/JSON Lines, AuditRecord y cola por archivo, escritor
  serializado y recuperación por AuditId.

La [arquitectura](arquitectura.md) desarrolla la justificación y alternativas;
el [contrato de administración y bitácoras](administracion-y-bitacoras.md)
especifica configuración, diagnóstico y recuperación.
El [plan académico](plan-academico.md) establece prioridad, secuencia, tallas y evidencia
de las épicas, sus historias y las cuatro funcionalidades.

## Condiciones para la integración productiva

La implementación académica utiliza un conjunto sintético y adaptadores de prueba.
Antes de desplegar en una instalación real deberán verificarse:

- Reglas comerciales, equivalencias, precios y colectivos aplicables.
- Modelo y licencia del SDK BioMini, formatos de templates, enrolamiento,
  revocación y alternativa operativa a la biometría.
- Tramas serie, modelo de impresora, driver y detección física.
- Provisión segura, alta y baja productiva de operadores y administradores sobre
  los roles locales de ADR12, protección de datos, retención, capacidad y
  recuperación consistente de base y archivos.
- Integración con INWeb, identidad de equipos y distribución protegida del padrón.

La emisión para terceros, anulaciones, restituciones de cupo, liquidaciones,
concesionario, reservas, pagos, inventario y canje físico son ampliaciones futuras.
En particular, D-C15 conserva la prohibición de excepciones de cupo para terceros.
