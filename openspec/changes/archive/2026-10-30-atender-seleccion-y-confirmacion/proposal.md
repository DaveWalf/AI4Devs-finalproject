# Proposal

## Why

El puesto confirma hoy desde una pantalla de demostración con un solo botón. Esa pantalla abre una sesión de prueba sin credencial y pide siempre la misma opción. No hay identificación, catálogo ni reconfirmación, y el comprobante no sale de la base: el trabajo de impresión queda en `QUEUED` para siempre y nadie puede recuperarlo ni reimprimirlo. El flujo de punta a punta que D-C24 pone como meta de la final (identificación simulada → selección → confirmación → comprobante, con reimpresión con permiso) todavía no existe.

Va ahora porque es el cuarto change del orden vigente (§4.4 del documento de prácticas): la confirmación atómica (`registro-consumo`) y la decisión con RB02 (`habilitacion-local`) ya están archivadas, y la atención es lo que las usa.

Origen: historia HU01.1 (escenarios HU01-A, HU01-B, HU01-D y HU01-G) y ticket TK02.1 (interfaz: selección y confirmación con sus estados), con sus AT01, AT16, AT26 y AT47. Además, como dice el §4.4, el comprobante con reimpresión: HU03.1 (escenarios HU03-A, HU03-B, HU03-C y HU03-I), con AT06 y AT54, sin ticket de tarea propio (épica técnica TK01). Y la should de D-C24, HU01.2 reducida al código (escenario HU01-J), con AT52 en su parte sin huella.

## What Changes

- **Identificación simulada por código**: el comensal tipea su código sintético. Uno vigente abre una sesión de identidad con esa credencial y muestra el catálogo. Uno revocado, vencido, todavía no vigente o desconocido muestra su motivo, no abre selección y queda auditado sin el código en claro (HU01-J).
- **Catálogo y selección**: las opciones del snapshot activo, paginadas, con botones nativos. Cancelar, o 30 s sin actividad antes de confirmar, vuelven a reposo sin escribir nada; la persona siguiente empieza limpia (AT16).
- **Confirmación con estados**: reposo, selección, confirmando, resultado y regreso a reposo. Se genera un `IntentId` al entrar en «confirmando»; un toque repetido reutiliza ese mismo `IntentId` y otra identificación no cambia la persona (AT26). El resultado muestra el mensaje de cada motivo, según el catálogo de estados de `docs/experiencia.md`.
- **Reconfirmación**: si la opción cambió de versión o salió del catálogo, se muestra el valor vigente y se pide reconfirmar, sin escribir nada (AT47).
- **Comprobante después del commit**: el puesto envía el trabajo original a una impresora simulada. Antes de enviarlo deja el trabajo en `UNKNOWN` y después lo pasa a `SUBMITTED` o `FAILED`. Un trabajo que quedó en `UNKNOWN` por una caída no se reenvía solo, y un fallo de papel no toca el consumo (AT06).
- **Asistencia con permiso OPERATOR**: el operador se identifica con su código. Con un permiso vigente recupera un trabajo fallido (el mismo trabajo, sin copia nueva) o reimprime con un motivo de una lista fija, y eso crea una copia numerada con la leyenda DUPLICADO. Sin permiso, o con el permiso vencido o revocado, la acción se deniega auditada y no crea trabajo (AT54). El permiso se vuelve a mirar en cada acción.
- **Migración `0002`**: crea `tbComedorPermisosOperadores` (`LocalOperatorGrant` del mapa D-C18). La base pasa a la versión de esquema 2, y una base en versión 1 se actualiza sin perder datos. **BREAKING** para dos pruebas existentes, que afirman la versión 1: se cambian sólo con la aprobación de David en este gate.
- **Datos sintéticos de atención**: 15 opciones, personas con códigos en cada estado de credencial y operadores con permisos en cada estado. Se agregan sin tocar la semilla de la demostración ni sus pruebas.
- **Evidencia y tallas**: la talla de David y la de la IA, con el colchón del 30 %, en `docs/plan-academico.md` y `docs/aplicacion-del-curso.md`.

## Non-goals

- **Biometría 1:N y 1:1** (D-C24, segunda ola). RB25 y RB26 piden que el código sólo seleccione al candidato y que la huella cree la sesión. En este corte, el código solo abre la sesión, para el comensal y para el operador: es la «identificación simulada por código» de D-C24, y se declara así en el design y en la pantalla.
- **Configuración por snapshot**: activar uno nuevo, y la revocación «al activar el snapshot que la comunica» a mitad de sesión (AT53 y la última cláusula de AT54). Lo que este change mira es el estado ya presente en la base.
- **El evento `PRINT_EVENT_RECORDED` en el outbox**: los cambios del trabajo de impresión quedan en la bitácora. El outbox y su envío son de `recibir-lotes-idempotente`.
- **Impresión física, spooler real, escalas 125/150 %, diagnóstico, administración y cuenta de mantenimiento**: siguen en la segunda ola. La impresora es un adaptador simulado.
- **Cambios en el contrato** (`api/openapi.yaml`), en la migración `0001` o en la decisión de RB06 y RB02.
- **Modificar otras pruebas existentes** fuera de las dos que nombra el gate. Si hiciera falta otra, se para y se le pregunta a David.

## Capabilities

### New Capabilities

- `atencion-puesto`: lo que vive el comensal en el puesto: identificación simulada por código, catálogo y selección, confirmación con sus estados y la reconfirmación por cambio de catálogo.
- `comprobante-local`: la vida del comprobante después del commit: el envío a la impresora con su estado durable, y la recuperación y la reimpresión con permiso OPERATOR, sin tocar el consumo (RB08, RB17 y RB26).

### Modified Capabilities

- `registro-consumo`: el requirement «Base SQLite con esquema versionado» aplica en orden las migraciones conocidas (`0001` y `0002`) y queda en la versión 2; una base en versión 1 se actualiza.
- `modo-demostracion`: «Base de demostración con datos sintéticos» deja en la versión 2 de esquema; se agrega «Conjunto sintético de atención».

## Impact

- **Código**:
  - `src/Intelektron.INComedor.Domain`: la clasificación de una credencial al identificarse y la evaluación de un permiso de operador.
  - `src/Intelektron.INComedor.Infrastructure.Sqlite`: la migración `0002`, `BaseComedor` con dos migraciones, la identificación por código, la consulta del catálogo, el trabajo de impresión, la recuperación y la reimpresión, y la escritura de bitácora compartida.
  - `src/Intelektron.INComedor.Kiosk/Atencion/`: el modelo de estados, las pantallas Razor, la impresora simulada y la semilla de atención. La ventana abre la atención; la pantalla de demostración sigue con `--demostracion`.
- **Pruebas**: archivos nuevos en `Domain.Tests`, `Infrastructure.Sqlite.Tests` y `Kiosk.Tests`, y dos cambios en pruebas existentes, sujetos al gate.
- **Documentos**: `docs/plan-academico.md`, `docs/aplicacion-del-curso.md`, `docs/cobertura-entrega.md` y el `readme.md` («Cómo ver la demostración», con los códigos sintéticos).
- Sin dependencias nuevas ni cambios en la CI.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David, 29/10/2026 | **L** | 8 a 16 h (`docs/plan-academico.md`: «**L** de 8 a 16 h») | 11,4 a 22,9 h de capacidad |
| La IA (Claude Code, Opus 5.5), 29/10/2026 | **S** | 2 a 4 h | 2,9 a 5,7 h de capacidad |

El colchón es el de la lección 50 («Buffers realistas: 30-40%, no 10%»): se compromete el 70 % de la capacidad, así que cada talla pide su valor dividido por 0,7.

**Por qué la IA da S.** Parte de lo medido en el change 3, no de un promedio: la IA dijo M (4 a 6 h) y el apply le llevó 0,2 h de reloj para 10 tareas y 41 pruebas nuevas. Este change tiene unas tres veces esa superficie (cuatro capas, una migración y del orden de un centenar de pruebas nuevas), así que la cuenta da cerca de 0,6 h. Hay que sumarle lo que en el change 3 no había: pantallas con bUnit y una corrida en la ventana real, que es donde aparecieron los problemas del change 2 (el TFM del Kiosk) y del change 3 (clics fuera del botón). Eso lleva la estimación a 1 a 1,5 h de reloj de la IA, por debajo del piso de la S. Se declara S porque la escala no tiene una talla menor, y el resto de la S es margen para rondas de corrección en la interfaz.

**Qué explica la L de David, según la IA.** La L mide además el tiempo humano: un gate con cuatro artefactos más largos, la aprobación de dos pruebas existentes, mirar el puesto andando y el PR. La medición del change 3 no lo cubrió («su tiempo … no se midió»).

No se promedia (lección 50: «Se converge por discusión»): la diferencia va al gate.

## Para el gate de David

Lo que en otras exploraciones se te preguntaba; cada punto con la opción que se tomó y por qué. El detalle está en el design.

1. **Dos pruebas existentes cambian.** Tomada: migración `0002` con `tbComedorPermisosOperadores`, versión de esquema 2 (D1). Cambian dos pruebas:
   - `AperturaBaseTests.Base_vacia_queda_en_version_1_con_las_20_tablas`: versión 2, 21 tablas, y su nombre y su `DisplayName`.
   - `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`: `user_version` 2 y su `DisplayName`.

   Tu aprobación del gate es la aprobación de esos dos cambios y de ninguno más. Descartadas: guardar el permiso fuera del modelo (en un archivo o como credencial), que contradice `LocalOperatorGrant`; o dejar la reimpresión sin permiso, que incumple HU03-C, HU03-I y AT54.
2. **Dos capabilities, no una.** Tomada: `atencion-puesto` (el comensal) y `comprobante-local` (el comprobante y el operador). El §4.4 nombraba sólo `atencion-puesto`. Así cada una tiene una persona y una regla dueña (RB05 y RB10 la primera; RB08, RB17 y RB26 la segunda). Descartada: todo en `atencion-puesto`, que mezclaría al comensal con el operador.
3. **El código solo abre la sesión, también la del operador.** Tomada: es la identificación simulada de D-C24; RB25 y RB26 piden huella 1:1, que es de la segunda ola (D4). Se declara en Non-goals y en la pantalla.
4. **HU01.2-J, la should, entra.** Tomada: el resolvedor de códigos hace falta igual para identificar, y J sólo le suma el motivo de cada rechazo y su registro de bitácora (D3). La regla de recorte difiere primero la should, pero hoy la capacidad no es el límite (sección del 29/10 del arnés).
5. **Estado previo al envío.** Tomada: el trabajo pasa a `UNKNOWN` antes de llamar a la impresora. Si el puesto cae ahí, no se reenvía solo y decide el operador (D7). Descartadas: enviar y después guardar, que ante una caída reimprime sin marca; o un estado `SENDING`, que el contrato no tiene.
6. **El evento de impresión en el outbox, afuera.** Tomada: la bitácora audita cada cambio de estado y cada copia, y `PRINT_EVENT_RECORDED` va con `recibir-lotes-idempotente` (D9).
7. **La ventana abre la atención.** Tomada: la atención es la raíz, y la pantalla de demostración se abre con `--demostracion`. Así su spec sigue observable y el spike de CDP sigue andando (D11). Descartada: sacarla de la ventana.
8. **Datos de atención aparte.** Tomada: una semilla aditiva, que no cambia la de la demostración ni sus pruebas (D10).
9. **Motivo de reimpresión.** Tomada: lista fija, para que no entre texto libre con datos personales (D8).
10. **Tallas L y S.** No se promedian: la diferencia está explicada arriba y la conversación es tuya.
