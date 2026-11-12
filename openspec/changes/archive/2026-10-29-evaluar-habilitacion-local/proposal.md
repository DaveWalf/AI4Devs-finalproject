# Proposal

## Why

El puesto decide hoy con RB02 recortada: toma «la única regla por persona vigente» para cada servicio y categoría, ignora `Priority` y la denegación explícita `Limit=0`, no mira si la persona sigue activa ni si la credencial de la sesión venció o fue revocada, y con dos reglas aplicables lanza una excepción. Eso alcanzó para la confirmación atómica de `registrar-consumo-atomico`, que lo declaró fuera de su corte, pero no para HU01.3: «Recibir la decisión según cupo y reglas locales vigentes», con sus escenarios C, H e I.

Va ahora porque David lo ubicó en el gate de `registrar-consumo-atomico` (19/10/2026) entre ese change y `atender-seleccion-y-confirmacion`: la atención completa necesita que la decisión ya sea la de RB02.

Origen: historia HU01.3 (escenarios HU01-C, HU01-H y HU01-I de la épica HU01), regla RB02 y sus AT01, AT09, AT17, AT29, AT36, AT52 y AT53. Ticket: la épica técnica TK01, punto 2 («validar sesión local, persona/credencial (RB25) … Aplicar RB02: filtrar vigencia `[ValidFromUtc, ValidToUtc)` con extremos opcionales, denegación `Limit=0` prioritaria y mayor `Priority` entre reglas positivas»). No tiene un ticket de tarea propio: los tres de la plantilla son TK03.1, TK02.1 y TK01.1.

## What Changes

- **Persona activa y credencial vigente primero**: una intención nueva de una persona dada de baja se deniega con `PERSON_INACTIVE`; si la sesión lleva una credencial, una revocada da `CREDENTIAL_REVOKED` y una vencida, `CREDENTIAL_EXPIRED`. Cada motivo es propio y queda auditado como cualquier rechazo de RB06.
- **Vigencia de las reglas**: inicio inclusivo y fin exclusivo, con extremos ausentes abiertos, evaluada con el reloj del puesto en el instante de la decisión. Una regla vencida es una regla de negocio que no aplica, no una falta de red; una regla sin vencimiento no caduca por estar sin red.
- **Regla cero**: una regla aplicable con `Limit=0` deniega con `RULE_DENIED` aunque haya un permiso de mayor `Priority`; cuando la regla cero deja de aplicar, se evalúan las restantes.
- **Prioridad entre reglas positivas**: decide la de mayor `Priority`. Un empate en la mayor prioridad entre reglas que dicen cosas distintas no se resuelve en silencio: la confirmación termina con error de configuración y no escribe nada.
- **El cupo usa la regla elegida**: el requirement de cupo de `registro-consumo` deja de exigir «la única regla vigente» y usa la que eligió la habilitación. El resto de RB03 no cambia.
- **Pruebas nuevas, sin tocar las existentes**: pruebas de dominio y sobre SQLite real, cada una vista en rojo antes de implementar. Las 61 actuales siguen en verde sin cambios.
- **Evidencia y tallas**: la talla de David y la de la IA, con el colchón del 30 %, pasan a `docs/plan-academico.md` y a las filas de estimación de `docs/aplicacion-del-curso.md`.

## Non-goals

- **La configuración por snapshot** (D-C24, segunda ola): activar una versión nueva, la descarga parcial y la revocación «al activar el snapshot que la comunica». De AT09 queda la baja ya presente en la base; de AT53, la credencial ya revocada y el replay. La descarga, la activación y la revocación a mitad de sesión por un snapshot nuevo se declaran fuera.
- **Reglas de grupo**: la migración `0001` no tiene pertenencia a grupos (`comgrpCodigo` sin FK, «los grupos son segunda ola»). El puesto sigue leyendo sólo reglas por persona.
- **La identificación**: resolver un código a su credencial, la huella 1:1 y los motivos `IDENTITY_NOT_RESOLVED` y «todavía no vigente» son de la identificación (`atender-seleccion-y-confirmacion` y biometría, segunda ola). Este change mira la credencial que ya trae la sesión.
- **Publicar la configuración**: RB02 dice que un empate contradictorio «impide publicar». Publicar es de INWeb; el puesto sólo se niega a decidir con una base así.
- Cambios en el modelo de datos, la migración `0001`, el contrato (`api/openapi.yaml` ya tiene los cinco motivos) o la pantalla de demostración.
- Modificar un test existente: no hace falta, y si hiciera falta se para y se le pregunta a David.

## Capabilities

### New Capabilities

- `habilitacion-local`: quién puede consumir un servicio en el puesto, sin red, según RB02: persona activa, credencial de la sesión, vigencia de las reglas, regla cero y prioridad entre reglas positivas.

### Modified Capabilities

- `registro-consumo`: el requirement «Cupo por persona, ámbito, categoría y fecha de negocio» toma el límite y el ámbito de la regla que eligió la habilitación, en lugar de exigir una única regla vigente.

## Impact

- Código: `src/Intelektron.INComedor.Domain` (un evaluador de habilitación y cuatro motivos nuevos de denegación) y `src/Intelektron.INComedor.Infrastructure.Sqlite/UnidadConfirmacion.cs` (lee la persona, la credencial de la sesión y las reglas candidatas, y llama a los dos evaluadores dentro de la misma transacción de RB06). `EvaluadorCupo` y `ReglaPersona` conservan su firma.
- Pruebas: dos archivos nuevos, uno en `Domain.Tests` y otro en `Infrastructure.Sqlite.Tests`.
- Documentos: `docs/plan-academico.md`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md`.
- Sin dependencias nuevas ni cambios en la CI.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David, 29/10/2026 | **S** | 2 a 4 h (`docs/plan-academico.md`: «**S** de 2 a 4 h») | 2,9 a 5,7 h de capacidad |
| La IA (Claude Code, Opus 5.5), 29/10/2026 | **M** | 4 a 6 h | 5,7 a 8,6 h de capacidad |

El colchón es el de la lección 50 («Buffers realistas: 30-40%, no 10%»): se compromete el 70 % de la capacidad, así que cada talla pide su valor dividido por 0,7.

Por qué la IA da M: la regla tiene muchos bordes que pedir uno por uno (inicio exacto, fin exacto, regla cero vencida, empate, persona inactiva con regla cero), y cada uno es una prueba que se ve fallar antes, en dos niveles, el dominio y SQLite real. El cambio en `UnidadConfirmacion` cae dentro de la transacción de RB06. Las pruebas existentes no se tocan, así que las nuevas siembran sus propias reglas. A eso se suman la evidencia y `/openspec-verify-change`. Lo que acerca la talla a la S de David: el contrato ya tiene los cinco motivos, el esquema ya tiene `Priority`, vigencia, `comperActivo` y la credencial de la sesión, y no hay migración.

No se promedia (lección 50: «Se converge por discusión»): la diferencia va al gate.

## Para el gate de David

Lo que en otras exploraciones se te preguntaba; cada punto con la opción que se tomó y por qué. El detalle está en el design.

1. **¿Se mira la credencial al confirmar?** Tomada: sí, la de la sesión, revocada o vencida (D6). TK01 punto 2 lo pide, RB02 lo pone primero, y una sesión abierta en el borde de `ValidToUtc` autorizaría con una credencial vencida. Descartada: dejarlo todo a la identificación.
2. **Reglas de grupo.** Tomada: fuera, declarado (D7). No hay pertenencia a grupos en la base, y agregarla es una migración de la segunda ola.
3. **Empate contradictorio.** Tomada: error de configuración, sin escribir nada (D5). Descartadas: desempatar por código, que decide en silencio, o denegar con `SERVICE_NOT_ENABLED`, que registra como hecho que la persona no tiene derecho.
4. **AT09 y AT53 a medias.** Tomada: sólo lo que no necesita snapshot (Non-goals). D-C24 manda.
5. **La vigencia pasa al dominio.** Tomada: `UnidadConfirmacion` deja de filtrar por vigencia y le pasa al dominio todas las reglas de la persona para el servicio (D2), así los bordes de AT29 y AT36 se prueban en unidad.
6. **Tallas S y M.** No se promedian: la diferencia está explicada arriba y la conversación es tuya.
