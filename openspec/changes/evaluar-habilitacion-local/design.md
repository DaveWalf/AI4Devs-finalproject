# Design

## Context

El motivo está en proposal.md. Lo que sigue es el estado de partida, leído el 29/10/2026 en `48cc5bb`.

- **Dónde se decide.** `UnidadConfirmacion.Confirmar`, dentro de `BEGIN IMMEDIATE` (RB06): valida la sesión (`SesionUtilizable`: existe, es de la persona, no venció, no se usó, y la persona existe en el snapshot activo), relee la opción de menú, calcula la fecha de negocio, lee las reglas con `LeerReglasVigentes` y llama a `EvaluadorCupo.Evaluar`. La decisión se persiste en la misma transacción.
- **Qué hace hoy con las reglas.** `LeerReglasVigentes` trae las reglas por persona del servicio (`comperCodigo = $per`) y filtra la vigencia en C#, con inicio inclusivo y fin exclusivo. No lee `comregPrioridad`. `EvaluadorCupo` agrupa por categoría: con cero reglas deniega `SERVICE_NOT_ENABLED`, con dos o más lanza `ReglasCupoAmbiguasException` y con una aplica su límite (RB03). No hay regla cero ni prioridad.
- **Qué no mira.** `comperActivo` no se consulta: una persona dada de baja con regla vigente se autoriza. La credencial de la sesión (`tbComedorSesionesIdentidad.comcreCodigo`, nullable) tampoco.
- **Lo que ya existe y se reusa.** El esquema `0001` tiene `comperActivo`, las fechas de vigencia y revocación de `tbComedorCredencialesPersonas`, y `comregLimite`, `comregPrioridad` y la vigencia de `tbComedorReglasHabilitacion`. El contrato (`api/openapi.yaml:1327-1341`) ya enumera `PERSON_INACTIVE`, `CREDENTIAL_REVOKED`, `CREDENTIAL_EXPIRED`, `RULE_DENIED` y `SERVICE_NOT_ENABLED`, con `RULE_DENIED` definido como «una regla aplicable vigente Limit=0 (RB02)». No hace falta migración ni cambio de contrato.
- **Lo que no se puede tocar.** Las pruebas existentes: `EvaluadorCupoTests` (9 métodos de prueba sobre la firma actual de `EvaluadorCupo.Evaluar` y `ReglaPersona`, incluida la de la excepción por ambigüedad), `FixtureSintetico` y el resto (regla de `AGENTS.md`). `ReplayTests:47` desactiva a la persona habilitada para probar que el replay no revalida: con este change eso sigue igual, porque el replay se resuelve antes de decidir.
- **La sesión de demostración no lleva credencial** (`ServicioDemostracionTests`, D6 de `fijar-primer-corte`): el puesto de demostración tiene que seguir autorizando el primer toque.

## Goals / Non-Goals

**Goals:**

- Que RB02 viva en una función pura del dominio, con sus bordes probados en unidad, y que SQLite sólo lea datos.
- Que `EvaluadorCupo` y sus pruebas queden como están: la habilitación elige una regla por categoría y el cupo la usa.
- Que cada escenario del delta tenga una prueba con su `DisplayName` y su AT, vista en rojo antes de implementar.

**Non-Goals:**

- Cambiar cómo se identifica la persona o cómo se crea la sesión: es la identificación, otro change.
- Resolver reglas de grupo o el ámbito del puesto (D7 y D8).

## Decisions

### D1. Un evaluador nuevo de dominio antes del de cupo

`Domain` suma `EvaluadorHabilitacion.Evaluar`, una función pura que recibe la persona (activa o no), la credencial de la sesión (si hay), el instante de la decisión, el servicio, las cargas de la opción y las reglas candidatas con `Priority` y vigencia. Devuelve una denegación con su motivo o, por cada categoría de las cargas, la regla positiva elegida como `ReglaPersona`. `UnidadConfirmacion` le pasa esa lista a `EvaluadorCupo.Evaluar`, que encuentra exactamente una regla por categoría y decide el cupo como hoy.

```
sesión + persona + credencial + reglas candidatas + instante
          |
          v
  EvaluadorHabilitacion (RB02) --denegada--> PERSON_INACTIVE | CREDENTIAL_REVOKED |
          |                                  CREDENTIAL_EXPIRED | RULE_DENIED | SERVICE_NOT_ENABLED
          | una ReglaPersona por categoría
          v
  EvaluadorCupo (RB03, sin cambios) --> AUTHORIZED | LOCAL_LIMIT_REACHED
```

- **Descartada: reescribir `EvaluadorCupo` para que reciba prioridades y vigencias.** Cambia la firma que usan sus 9 métodos de prueba, y modificarlos pide la aprobación de David. Además mezcla RB02 (quién puede) con RB03 (cuánto le queda).
- **Descartada: resolver RB02 en el SQL de `UnidadConfirmacion`** (`ORDER BY comregPrioridad`, un `CASE` para la regla cero). Los bordes de AT29 y AT36 sólo se probarían sobre SQLite, y la regla de negocio quedaría en Infrastructure.

### D2. La vigencia se decide en el dominio, con el instante de la decisión

`LeerReglasVigentes` pasa a `LeerReglasCandidatas`: trae todas las reglas por persona del servicio, con `comregPrioridad`, `comregLimite` y las dos fechas, sin filtrar. El dominio aplica `[ValidFromUtc, ValidToUtc)` con extremos nulos abiertos, contra el mismo `ahora` del reloj que ya usa la unidad para la fecha de negocio.

- **Descartada: dejar el filtro de vigencia en Infrastructure.** La regla cero vencida y el permiso que vuelve a valer en el fin exacto (AT36) se decidirían en dos capas, y el borde sólo se probaría con SQLite.

### D3. El orden de RB02

1. Persona inactiva → `PERSON_INACTIVE`.
2. Credencial de la sesión, si hay: revocada (`comcreFechaRevocacionUtc` no nulo) → `CREDENTIAL_REVOKED`; vencida (`ValidToUtc` ≤ instante) → `CREDENTIAL_EXPIRED`.
3. Por cada categoría de las cargas: las reglas candidatas de esa categoría vigentes en el instante.
   - alguna con límite 0 → `RULE_DENIED` para toda la opción;
   - ninguna positiva → `SERVICE_NOT_ENABLED`;
   - si no, la positiva de mayor `Priority` (D5 para el empate).

El orden de 1 y 2 sigue a RB02 («Evaluar primero persona activa y credencial vigente, con motivos separados») y a RB25 (revocada antes que vencida). La regla cero va antes que la búsqueda del permiso porque «prevalece sobre cualquier permiso, aunque tenga mayor `Priority`».

- **Descartada: evaluar la opción entera con una sola regla.** Una opción con cargas en dos categorías (`ComboAlmuerzoYRefrigerio` del fixture) necesita una regla en cada una; así lo hace ya `EvaluadorCupo`.

### D4. Cuatro motivos nuevos, con los nombres que ya tiene el contrato

`MotivoDenegacion` suma `PersonInactive`, `CredentialRevoked`, `CredentialExpired` y `RuleDenied` al final de la enumeración, y `CodigoDeMotivo` de `UnidadConfirmacion` los traduce a los códigos del contrato. El dominio nombra; Infrastructure pone el nombre del contrato, como hoy.

- **Descartada: un motivo genérico (`CONSUMPTION_DENIED`).** RB02 pide motivos separados, y el contrato ya los tiene.

### D5. Un empate contradictorio no se decide

Si en la mayor `Priority` de una categoría hay dos o más reglas positivas que difieren en límite o en ámbito, el dominio lanza `ReglasHabilitacionContradictoriasException`. La transacción no llega al commit: no queda intención, decisión ni auditoría, igual que hoy con `ReglasCupoAmbiguasException`. Si las empatadas tienen el mismo límite y el mismo ámbito, no hay contradicción y se toma cualquiera.

- **Descartada: desempatar por código de regla.** Decide en silencio algo que RB02 prohíbe publicar.
- **Descartada: denegar con `SERVICE_NOT_ENABLED`.** Registraría como hecho auditado que la persona no tiene derecho, cuando el problema es la configuración del puesto.

### D6. La credencial que ya trae la sesión

`SesionUtilizable` no cambia. `UnidadConfirmacion` lee, en la misma transacción, `comperActivo` de la persona y, si `comcreCodigo` de la sesión no es nulo, la vigencia y la revocación de esa credencial. Una sesión sin credencial no evalúa credencial: es la de demostración (D6 de `fijar-primer-corte`) y la de una identificación 1:N futura.

- **Descartada: evaluar sólo la persona y dejar la credencial a la identificación.** RB02 y TK01 punto 2 la ponen en la decisión, y la sesión dura lo bastante para cruzar el `ValidToUtc` de su credencial.
- **Descartada: tratar «todavía no vigente» en la confirmación.** Es un motivo de la identificación (`IDENTITY_NOT_RESOLVED` con causa interna, RB25): una sesión así no debería existir, y el change de identificación es el que la impide.

### D7. Las reglas de grupo quedan fuera

La migración `0001` no tiene pertenencia a grupos: `comgrpCodigo` no tiene FK, con el comentario «los grupos son segunda ola». `LeerReglasCandidatas` sigue filtrando `comperCodigo = $per`.

- **Descartada: una migración `0002` con pertenencia a grupos.** Es modelo de datos de la segunda ola (padrón por snapshot, D-C24) y agranda el change más allá de la talla de David.

### D8. El ámbito sale de la regla

La regla elegida trae su ámbito (`comacCodigo`), que con la categoría y la fecha de negocio forma la clave de cupo (RB03). `tbComedorConfiguracionPuestos` no tiene un ámbito del puesto, así que no hay nada contra qué filtrar el «ámbito» de RB02 en este corte.

- **Descartada: agregar un ámbito al puesto.** Es configuración por snapshot (segunda ola).

### D9. Pruebas nuevas, con siembra propia

- `tests/Intelektron.INComedor.Domain.Tests/EvaluadorHabilitacionTests.cs`: los escenarios del delta en memoria, con reglas armadas en la prueba y bordes exactos.
- `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs`: los mismos escenarios contra `UnidadConfirmacion` sobre SQLite real. Hereda `ConfirmacionTestBase` y siembra sus reglas, credenciales y bajas con `Sql.Ejecutar` sobre la base del fixture, sin cambiar `FixtureSintetico`.

Cada `DisplayName` empieza con el AT o la regla de origen, como las pruebas existentes. Cada prueba se ve fallar antes de implementar (regla de `AGENTS.md`).

- **Descartada: agregar personas y reglas a `FixtureSintetico`.** Es un archivo de pruebas existente; tocarlo pide la aprobación de David, y el hook de `imponer-invariantes-en-codigo` la pediría.

### D10. Tallas, colchón y evidencia

Las tallas y el colchón del 30 % están en proposal.md. Una tarea los lleva a `docs/plan-academico.md`, sección «Capacidad y reestimación», y pasa a `Aplicada` las filas «Capacidad con 30 % de margen» y «La IA estima como un par, no como única fuente» de `docs/aplicacion-del-curso.md`. La última tarea agrega la evidencia del change.

- **Descartada: registrar las tallas sólo en proposal.md.** El mapa promete, desde este change, la talla doble y el margen en el plan académico.

## Risks / Trade-offs

- [Una base con reglas de grupo] → El puesto las ignora, como hoy. En este corte no hay grupos en el padrón; si aparecieran, una regla cero de grupo no se aplicaría. Queda declarado en Non-goals y como segunda ola.
- [Un empate contradictorio deja el puesto sin decidir para esa persona] → Es lo que RB02 quiere: esa base no debió publicarse. El error nombra la categoría y la prioridad, y no deja hechos falsos.
- [Cambia el resultado de una persona inactiva o de una credencial vencida] → Hoy se autorizan; después se deniegan con su motivo. Ninguna prueba existente fija el comportamiento actual (leído en `tests/`), y el replay de lo ya decidido no cambia.
- [La IA estima M y David S] → No se promedia (lección 50). Si el apply supera la S, el registro de horas lo muestra en la reestimación.

## Migration Plan

No hay migración de datos ni de esquema. Se revierte con `git revert` de los commits del apply.
