# Registro de trabajo con IA — INComedor

Primera entrega, actualizada el 29/09/2026. Autor: David Walfisch.
Este registro cubre investigación, especificación, revisión y preparación
documental. Las siguientes entregas incorporarán el trabajo de implementación,
pruebas operativas y despliegue.

## Contexto, herramientas y verificación humana

Se utilizó Codex, con modelo configurado `gpt-6-astra`, para leer la documentación
existente, recuperar las conversaciones pertinentes, contrastar consignas y
preparar Markdown, diagramas editables y OpenAPI. La investigación previa del
26/09 generó una especificación institucional con la skill de documentos institucionales `itk-word`; esta fase reutiliza
su evidencia para formular el alcance académico.

En esta fase se distribuyeron análisis entre subagentes: requisitos/historias,
API/datos y revisión independiente. El agente principal integró resultados y
consultó criterios al autor. Se usaron lectura local, comparación SHA256, lectura
de chats, documentación oficial y validaciones estáticas. Las decisiones
de producto y de alcance fueron confirmadas por el autor.

Las fuentes empresariales y el historial completo se conservan en el entorno
interno. Este paquete usa cliente anonimizado y datos sintéticos. Las citas
siguientes son extractos reales del autor, o resúmenes expresamente identificados.
No se enlazan conversaciones privadas ni se publican documentos internos.

### Herramientas y modelos por fase

| Fase | Herramienta y modelo registrado | Aporte |
|---|---|---|
| Investigación y especificación inicial | Codex, modelo configurado `gpt-6-astra` | Contraste de fuentes y generación documental con la skill `itk-word`. |
| Diseño académico | Codex, modelo configurado `gpt-6-astra` | Arquitectura, especificación, historias, datos, diagramas, OpenAPI y controles estáticos. |
| Revisión independiente | Claude Code, `claude-opus-5-5[1m]`, esfuerzo `max`, según el registro de la revisión | Contraste del planeamiento y detección de inconsistencias. |
| Revisión e integración | Codex, modelo configurado `gpt-6-astra` | Contraste de observaciones con fuentes, corrección de contratos y revisión de coherencia documental. |
| Evaluación simulada y refinamiento | Claude Code, `claude-opus-5-5[1m]` | Evaluación con una rúbrica orientativa y aplicación, por pedido del autor, de sus cuatro mejoras prioritarias: épicas e historias de tamaño real, credenciales y permisos locales, priorización y consistencia del contrato. |

La revisión cruzada mejoró la auditoría dentro de la transacción de negocio,
la diferenciación de motivos de rechazo y el detalle de historias y tickets.
Las observaciones se contrastaron con la especificación antes de integrarlas.

En la revisión final del diseño se distribuyeron tres focos entre subagentes:
consigna y presentación académica, coherencia de datos/contratos y viabilidad del
stack. El autor confirmó que las cuatro funcionalidades son obligatorias y que
el puesto debe seguir la hora de Windows y auditar sus saltos. Estas decisiones
se incorporaron al alcance, al plan de implementación y a los criterios de aceptación.

## 1. Descripción general del producto

**Prompt 1 — extracto real del pedido inicial, 26/09:**

> El sistema INComedor deberá poder recibir algún tipo de identificación desde diferentes dispositivos: a priori, consideremos que capturará un evento en un puerto serie (virtualizado a través de un dispositivo que se lo aporte con el código de identificación de la persona). También deberá poder comandar al un biomini de última generación conectado a USB. Como plataforma, usaremos una All-in-One de gama media, con pantalla táctil y Windows 11.

Resultado: definición de un kiosco con adaptadores, persistencia propia y relaciones
con INWeb. El extracto conserva la redacción original y omite referencias
confidenciales del caso empresarial.

**Prompt 2 — extracto real, 27/09:**

> Sobre el proyecto, analizá lo que estuvimos preparando sobre el INComedor, y generá con eso una base con todo que piden para el proyecto […]

Resultado: paquete de primera entrega con README, registro de IA, especificación,
historias, tickets, datos y contrato. El autor confirmó el recorte con adaptadores
de prueba y homologación posterior para INWeb/BioMini reales.

**Prompt 3 — aclaración real del autor sobre BioMini, 26/09:**

> El biomini es el de Suprema. ¿es el mismo que consideraste? En ese caso, la idea es que tenga los templates de las personas habilitadas, y a través de una búsqueda 1:n o una identificación previa con un código, y luego una verificación, se certifique que la persona es tal, y con su ID asociado se siga el resto del circuito.

Corrección humana incorporada: ambos flujos son requisitos del producto. Modelo,
SDK, compatibilidad y residencia de templates siguen sujetos a homologación.

## 2. Arquitectura del sistema

**Prompt 1 — aclaración real del autor, 27/09:**

> El INComedor debe nutrirse del INWeb y generar una base propia, como lo haría cualquier equipo, sin depender de que el INWeb esté en línea en ningún momento. Debe funcionar de modo completamente desatendido, con las últimas reglas que se le hayan configurado, y con una base local propia, que puede ser el SQL Lite si considerás que es más eficiente. Reformulá lo que sea necesario en el proyecto para asegurar este esquema.

Impacto: se reemplazó la propuesta inicial de commit central/ONLINE_ONLY por
autoridad local SQLite, arranque offline provisionado y sincronización eventual.
Se reformularon arquitectura, requisitos, API, datos, historias y pruebas.

**Prompt 2 — aclaración real sobre varios puestos, 27/09:**

> La mayoría de los esquemas tendrá un único puesto. En caso de más de uno, y donde se necesite auditar, sí o sí deberán estar informando al INWeb en línea, y los que sean deberán además actualizar inmediatamente su registro también. Existe una ventana potencial donde la persona igual saque más de uno, que deberá generar un aviso para su posterior auditoría.

Impacto: publicar y propagar hechos inmediatamente en el perfil multipuesto
auditado, actualizar registros locales sin dobleconteo y generar avisos por
excesos concurrentes. No se promete exclusión global instantánea.

**Prompt 3 — decisión real del autor, 29/09:**

Ante las opciones de continuidad, suspensión o configuración por instalación,
el autor respondió «3», eligiendo «Permitir configurarlo por instalación, con
continuidad local como valor inicial».

Impacto: D-A02 cerrada; se documentaron CONTINUE_LOCAL y SUSPEND_NEW_CONSUMPTIONS,
persistencia de la política, incidencias y recuperación del feed. La suspensión
afecta confirmaciones nuevas; conserva replay y comprobantes ya registrados.
Se actualizaron el contrato, el modelo y los escenarios, sin ejecutar el producto.

## 3. Modelo de datos

**Prompt 1 — resumen de instrucción al subagente de datos, no cita literal:**

Diseñar padrón/configuración versionados, intención/consumo/contador locales,
ticket, outbox, lotes y acuses; templates separados; incorporar consumos observados
de otros puestos e impedir que un eco propio incremente el contador otra vez.

Resultado: diccionario y diagrama ER con restricciones, ubicación y responsabilidades.
Se revisan claves de intención, evento, lote, consumo y copia de ticket. Los datos
de ejemplo son sintéticos; no se vuelcan huellas, tarifas ni legajos empresariales.

**Prompt 2 — resumen del pedido y decisiones reales del autor, 29/09:**

Incorporar interfaz administrativa y diagnóstico por componente, emisión futura
para terceros internos/externos y bitácoras general/interacciones externas a DB,
de texto plano mensual AAAAMM. Ante la consulta de fallo de archivo, el autor eligió
continuar con respaldo durable SQLite y reconstrucción con intervalo degradado.
Para terceros confirmó: «Sólo reglas del beneficiario, sin excepciones de cupo».

Resultado: clasificación funcional/no funcional/futura, roles, matriz de diagnóstico,
archivos independientes y cola de recuperación; RF40–46 y AT37–44. Se actualizaron
arquitectura, datos, UX, historias/tickets y límites del contrato API.

**Prompt 3 — resumen del pedido y decisión real del autor, 29/09:**

Contrastar los nombres de tablas, campos y objetos C# de INWeb para que INComedor
sea coherente con su ecosistema. El autor eligió adoptar la convención en
persistencia y C#, con un mapa explícito que conserve el contrato JSON y las
identidades; solicitó incorporar el resultado a la documentación académica.

Resultado: contraste de DDL, entidades y mappings; perfil `tbComedor...`, columnas
mnemónicas y negocio español. Se separaron claves enteras nativas y UUID del
módulo. La revisión corrigió una propiedad con el mismo nombre que su clase:
`VersionConfiguracion.NumeroVersionConfiguracion`. Se incorporaron los mapas del ER
(42 entidades y 233 atributos entonces; 44 y 256 tras el refinamiento), ADR11, D-C18, RNF15 y AT51, con trabajo
asignado a TK03/TK01. Codex integró los cambios y validó en copia el paquete y el
contrato estático; las pruebas de producto permanecen previstas, no ejecutadas.

## 4. Especificación de la API

**Prompt 1 — resumen de instrucción al subagente de API, no cita literal:**

Preparar tres operaciones OpenAPI 3.1: configuración completa, envío de lotes y
consulta de acuse. Modelar errores, identidad del equipo, idempotencia y conflictos.
La recepción central no vuelve a autorizar hechos ya confirmados localmente.

Resultado: contrato propuesto con ejemplos y validación estática. El diccionario
define los campos de la distribución biométrica y de la propagación de consumos
pares; su transporte se especificará en M0 como contrato complementario. OpenAPI
desarrolla las tres operaciones principales seleccionadas; en el refinamiento
posterior incorporó credenciales y permisos locales al snapshot.

## 5. Historias de usuario

**Prompt 1 — resumen de instrucción al subagente SDD, no cita literal:**

Construir tres historias con precondiciones y escenarios verificables: consumo
autónomo, continuidad/sincronización y recuperación de comprobante. Trazar ambos
flujos biométricos, fallas, reglas y dependencias, distinguiendo producto y demo.

Resultado: HU01–HU03 y escenarios vinculados. El autor corrigió la autoridad de
decisión y la política entre puestos; esas correcciones prevalecen sobre el borrador previo.
La evaluación simulada señaló que las tres tenían tamaño de épica; se rotularon
como épicas y se agregaron HU01.1, HU02.1 y HU02.2 con un backlog de historias hijas.

## 6. Tickets de trabajo

**Prompt 1 — resumen de instrucción de planificación, no cita literal:**

Derivar tres tickets representativos de backend/dominio, frontend y datos con
dependencias, entregables y definición de terminado; separar homologación real y
trabajo adicional del alcance de esos tres ejemplos.

Resultado: TK01–TK03 con criterios de aceptación y evidencia requerida para
considerar terminada cada tarea. En el refinamiento pasaron a épicas técnicas y se
agregaron TK03.1, TK02.1 y TK01.1, de tamaño tarea, con AT propios y un responsable.

## 7. Pull requests

El registro de prompts para revisión de código se incorporará durante las
entregas de implementación, junto con los enlaces de las revisiones realizadas.

## Intervención humana

El autor definió plataforma, dispositivos, autonomía, ambos modos biométricos,
alcance académico, administración local y comportamiento entre puestos. Corrigió
la propuesta inicial de autorización central para convertirla en una operación
autónoma con reglas y persistencia locales.

También confirmó anonimización del cliente, datos sintéticos, cuatro
funcionalidades obligatorias y continuidad ante saltos del reloj con auditoría
del cambio y del riesgo de fecha/cupo. Las decisiones vigentes se conservan en
[decisiones.md](docs/decisiones.md).

La IA se utilizó como asistencia a la investigación, al diseño y a su revisión.
Las decisiones de consumo del producto serán reglas determinísticas de la aplicación.

## Corrección posterior a la revisión independiente — 29/09/2026

Herramienta: Codex; modelo: gpt-6-astra; trabajo inline, sin subagentes. El autor pidió aplicar una matriz acotada de coherencia documental, conservar antecedentes y verificar en copia, sin publicar ni implementar. Confirmó ambas políticas multipuesto en la demo, 6 h semanales, M0 objetivo 06/10, códigos no reasignables, revocación recibida sin programación futura, roles sin herencia, cuenta individual local para mantenimiento y motivo externo genérico para código futuro.

La intervención ajustó especificación, contratos, responsabilidades y planificación; agregó AT56 dentro de administración. Las decisiones técnicas de provisión, protección de cuentas e historial de códigos quedan abiertas en M0. Los controles estáticos y simulaciones de fixtures no se presentan como ejecución del producto. El autor conserva la decisión de formalizar la entrega.
