# Design

## Context

Ver `proposal.md` («Why») para los pedidos de David. El estado de `8a57ef0` que este change toca:

- `PantallaAtencion.AbrirConfiguracion`: en operativo pide la clave (`AccionClave.AbrirConfiguracion`) y en mantenimiento llama a `AbrirConfiguracionCon(AccesoMantenimiento)`. `ResultadoClave` registra `DAILY_KEY_ACCEPTED` o `DAILY_KEY_REJECTED` y sigue con la acción: abrir, cerrar o pasar a operativo.
- `DialogoClaveDelDia.razor`: un `<input id="clave-del-dia" type="password" inputmode="numeric">` sin `value` (D6 de `configurar-puesto-por-pestanas`), «Aceptar» (`#aceptar-clave`), «Cancelar» (`#cancelar-clave`) y el aviso de desarrollo. Valida con `IValidadorClaveDelDia`, que en desarrollo es `ClaveDelDiaDeDesarrollo`: de 1 a 12 dígitos ASCII.
- `Asistencia.Entrar(codigo)` resuelve el código (RB25), evalúa el permiso `OPERATOR` (RB26), abre una sesión `PrivilegedAction` y registra `ASSISTANCE_GRANTED` o `ASSISTANCE_DENIED`. Devuelve `ResultadoAsistencia(SesionId, PersonId, NombreVisible, ReasonCode)`. La usa `SesionOperador`, de la pestaña Comprobantes.
- `PanelConfiguracion.razor`: el enum `Pestana`, con las nueve, y `Oculta(pestana)` para mostrar una sola.
- Los registros de la página y de `AdministracionLocal` escriben `ActorPersonId = null` y `ActorReason = "NO_AUTHENTICATION"`.

## Goals / Non-Goals

**Goals:**

- Un solo diálogo para la clave y el código del operador, operable sólo con la pantalla.
- Que el operador no llegue a General, ni a cerrar el puesto, ni a pasar a operativo, por ningún camino.
- Que lo tipeado siga sin guardarse ni registrarse, y que el código del operador no aparezca en ningún registro.

**Non-Goals:**

- La clave real de INWeb, la cuenta de mantenimiento y los demás permisos de operador.

## Decisions

### D1. El engranaje pide la clave en los dos modos

`AbrirConfiguracion` pide la clave siempre. `AccesoMantenimiento` queda sólo para cerrar con Escape en mantenimiento, que sigue directo.

El estado de las bitácoras que muestra General se lee al tocar el engranaje, antes de pedir la clave: así muestra lo que había al tocarlo, como ya decía el código, y la clave aceptada no aparece como un pendiente más («Configuración al día» de `bitacoras-externas`). Decidido en el apply, a la vista en la tarea 3.3.

- **Descartada: pedirla también para cerrar en mantenimiento.** El pedido nombra el menú de configuración, y cerrar en mantenimiento es la salida del técnico.

### D2. Qué acepta la clave

`ClaveDelDiaDeDesarrollo` no cambia: de 1 a 12 dígitos. «ENTER» vacío da «La clave no es válida».

- **Descartada: aceptar vacío.** Un toque sin querer en «ENTER» abriría Configuración. El teclado ya sólo tipea dígitos.

### D3. El teclado y el campo

- **El teclado.** Debajo del campo va un `div.teclado-numerico`, una grilla de 4 × 4 con la disposición del teclado numérico extendido (ver el punto 4 del gate):
  - los dígitos, de `#tecla-0` a `#tecla-9`, con el 0 de dos columnas;
  - `#tecla-limpiar`, que borra todo, alto en el lugar del «+»;
  - `#tecla-borrar`, que borra el último dígito, en el lugar del «.»;
  - «ENTER», que conserva `id="aceptar-clave"` y es alto en el lugar del Enter.
  
  Cada tecla mide al menos 56 px. «Cancelar» (`#cancelar-clave`) va debajo, separado, para que no se toque sin querer.
- **El campo.** El `<input id="clave-del-dia">` sigue siendo `type="password"`, ahora con `inputmode="none"`, para que Windows no abra su teclado táctil. Tiene `value` ligado a `_clave`, así el teclado en pantalla y el físico escriben en el mismo lugar. Recibe el foco al abrirse, y Enter en él es «ENTER». Un dígito que lleva el largo a más de 12 no se agrega.
- **Lo que cambia de D6 de `configurar-puesto-por-pestanas`.** El campo vuelve a dibujar su valor, pero como puntos de contraseña, sólo mientras el diálogo está abierto, y `_clave` se vacía al aceptar, al rechazar y al cancelar. Nada lo guarda ni lo registra, y «La clave no queda en ningún lado» lo sigue probando sobre la base, las bitácoras y los archivos.
- **Descartada: un campo sin `<input>`, con un `div` de puntos y las teclas físicas atendidas a mano.** Cambia las 8 referencias de las pruebas a `#clave-del-dia` y reescribe lo que el navegador ya hace con un campo.
- **Descartada: el orden de un teléfono.** No es la disposición del teclado extendido que pidió David.

### D4. El aviso de clave sin validar

Con `Access = "DAILY_KEY"`, Configuración muestra arriba `#aviso-clave-sin-validar` (`role="status"`), con `MensajesAtencion.ClaveSinValidar`: «Entraste con una clave del día sin validar: esta versión acepta cualquier número». El aviso queda hasta cerrar Configuración. Con un operador no aparece, porque su código sí se valida. El aviso y «Operador: …» (D6) van en la fila del título de Configuración, para no quitarle alto a las tablas: con una línea propia, la de personas mostraba nueve filas en 1024 × 768 y no las diez de `administracion-local`. Decidido en el apply, a la vista en la tarea 4.2.

- **Descartada: un cartel de 2 s antes de entrar.** Suma una espera en cada entrada y un temporizador más que probar.

### D5. El código de un operador en el mismo diálogo

Al tocar «ENTER», la página pregunta primero si lo tipeado es un código del snapshot activo. Lo hace `IdentificacionPorCodigo.EsCodigoConocido(codigo)`: lee el valor normalizado (RB01, RB25) sin abrir sesión ni registrar nada.

- **Si es un código**, para abrir Configuración la página llama a `Asistencia.Entrar(codigo)`, que ya clasifica la credencial y el permiso `OPERATOR` y registra su resultado.
  - Si se concede, entra con `Access = "OPERATOR"` y su `PersonId` como actor.
  - Si no, el diálogo muestra `MensajesAtencion.DeMotivo(reason)` y sigue abierto.
- **Para cerrar o pasar a operativo**, un código se rechaza sin llamar a `Asistencia`: el diálogo dice `MensajesAtencion.CodigoNoEsClave`, y la general registra `DAILY_KEY_REJECTED` con `Cause = "PERSON_CODE"`, sin el valor.
- **Si no es un código**, es la clave del día, como hoy.

- **Descartada: llamar a `Asistencia.Entrar` con cualquier valor.** Una clave del día que no es un código registraría un `ASSISTANCE_DENIED` por código desconocido en cada entrada.
- **Descartada: dos botones, «Clave del día» y «Soy operador».** Es más explícito, pero suma un toque y un modo más al diálogo, y el código del operador ya se distingue solo.

### D6. Las pestañas del operador

`PanelConfiguracion` recibe `Operador` (el nombre visible o nulo). Con operador:
- las pestañas son Consumos, Personas e Impresora, en ese orden, con Consumos elegida;
- las demás no existen en el DOM, y las flechas, Inicio y Fin recorren sólo esas tres;
- arriba va «Operador: <nombre>».

Sin General no hay «Cerrar el puesto», «Pasar a operativo» ni el interruptor de la simulación. `CambiarModo`, `CambiarSimulacion` y el cierre desde General vuelven sin hacer nada si el acceso es `OPERATOR`, por si un evento llegara igual.

- **Descartada: mostrar las nueve pestañas con las otras deshabilitadas.** Enseña lo que no se puede usar y ocupa la fila.

### D7. El actor

La página guarda `_actor` (`Guid?`) junto a `_acceso`. `RegistrarConfiguracion` escribe `ActorPersonId = _actor` y `ActorReason = "NO_AUTHENTICATION"` sólo si `_actor` es nulo. `AdministracionLocal.GuardarPersonas` y `GuardarOpciones` ganan un parámetro opcional `Guid? actor = null` con la misma regla, así sus llamadas y pruebas de hoy no cambian. La consulta de Consumos registra por la página.

- **Descartada: el actor nulo con un operador conocido.** Contradice RB21 («todo cambio … tiene actor»).

### D8. Las pruebas que suman la clave

Dos ayudantes nuevos, en archivos nuevos, entran a Configuración: tocan el engranaje, tipean `4711` en el teclado en pantalla y tocan «ENTER».
- `tests/Intelektron.INComedor.Kiosk.Tests/EntradaConfiguracion.cs`, para las pruebas de componentes;
- `tests/Intelektron.INComedor.E2E.Tests/EntradaConfiguracionE2E.cs`, para el puesto compilado.

En cada prueba existente que entra por el engranaje en mantenimiento, el toque pasa a ese ayudante, y donde se mide el acceso, `MAINTENANCE_MODE` pasa a `DAILY_KEY`. Nada más. El diff de `tests/` se revisa entero en la evidencia.

- **Descartada: un ajuste oculto que deje entrar directo en las pruebas.** Las pruebas dejarían de probar el puesto que se entrega.

### D9. Alcance y documentos

`AGENTS.md` y `openspec/config.yaml` dicen que la clave de desarrollo se pide en los dos modos, y que el acceso del operador a Consumos, Personas e Impresora sale de la segunda ola por este change. `readme.md` suma la clave al recorrido y `9001` en Configuración. `docs/aplicacion-del-curso.md` suma la evidencia.

## Risks / Trade-offs

- [Mientras la clave acepte cualquier número, quien no es operador entra con acceso completo tipeando otro número] → Aceptado y dicho en el gate. La restricción vale de verdad cuando INWeb valide la clave. El aviso de clave sin validar lo recuerda en cada entrada.
- [Una clave del día real igual al código de una persona se rechazaría] → Hoy no existe una clave real. Cuando llegue la de INWeb, el orden de D5 se revisa con su formato.
- [Unas 28 pruebas existentes cambian a la vez] → Cada diff se limita a D8 y se revisa entero. Si una falla por otra razón, se para.
- [`inputmode="none"` en el WebView2] → La prueba E2E mira el atributo; que el teclado táctil no aparezca lo ve David en su monitor.
- [El corte del 11/11 a las 12:00] → Recorte del punto 10 del gate.

## Migration Plan

Sin migración de base. Un puesto existente pide la clave en mantenimiento desde el primer arranque. Para volver atrás alcanza con revertir el merge.
