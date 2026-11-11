# Design

## Context

- **La atención.** `MaquinaAtencion.Identificar(codigo)` e `IdentificarPorCredencial(credencial)`, la del combo, abren
  una sesión de identidad de consumo (`_sesion`, privada) y dejan `PersonaId` y `NombreVisible`. Con opciones, la
  pantalla muestra la selección, cuyo título tiene «Cancelar». Sin opciones, muestra `EstadoAtencion.SinOpciones` con
  «No tenés opciones disponibles ahora.» y «Listo», y avisa `PersonaSinConsumo` por correo.
- **El engranaje.** Con el código de un operador, el engranaje llama a `Asistencia.Entrar(codigo)`. Esa llamada
  resuelve la credencial, evalúa `OPERATOR` con `EvaluadorPermisoOperador`, abre una sesión `PRIVILEGED_ACTION` de 2
  minutos y deja `ASSISTANCE_GRANTED` o `ASSISTANCE_DENIED`. Después, `PantallaAtencion.razor` abre Configuración con
  `AccesoOperador` y la sesión adoptada por Consumos (`SesionOperador.Adoptar`).
- **Lo que ya cubre la mejora 1.** La sesión que vence durante Configuración muestra
  `MensajesAtencion.SesionOperadorVencida` (`d6bbb24`).
- **Lo que no se puede reabrir.** Configuración se abre sólo en reposo (`CancelarConfiguracionFueraDeReposo`).

## Goals / Non-Goals

**Goals:**

- Que el operador llegue a sus comidas y a su Configuración desde la identificación, por el código o por el combo
  (gate, respuestas 1, 2 y 4).
- Que la sesión privilegiada siga con las mismas reglas de RB26 y que la bitácora distinga el camino.

**Non-Goals:**

- Los de la proposal: no cambian el engranaje, los permisos ni Personas.

## Decisions

### D1. Mostrar el botón lee el permiso y no abre nada

Al identificar, `MaquinaAtencion` pregunta `EsOperador(personaId)`: un método nuevo de `Asistencia` que sólo lee el
permiso `OPERATOR` del snapshot activo y lo evalúa con `EvaluadorPermisoOperador` en ese instante. No abre sesión ni
escribe en la bitácora. La respuesta queda en `MaquinaAtencion.EsOperador` hasta que la atención termina.

- *Descartada*: abrir la sesión privilegiada al identificarse. Cada operador que va a comer abriría una sesión y dejaría
  un `ASSISTANCE_GRANTED` sin haber pedido nada. Además, `MaquinaAtencionTests` y `MaquinaSimulacionTests` cuentan las
  sesiones de identidad, y cambiarían.

### D2. «Configuración» abre la sesión desde la sesión de consumo

`Asistencia.EntrarDesdeIdentificacion(sesionConsumo)` hace en una transacción lo mismo que `Entrar(codigo)`, pero toma
la credencial de una sesión de consumo vigente en lugar de resolver un código:

1. vuelve a evaluar `OPERATOR` (RB26);
2. cierra la sesión de consumo;
3. abre la `PRIVILEGED_ACTION`;
4. deja `ASSISTANCE_GRANTED` o `ASSISTANCE_DENIED` con `Source` `IDENTIFICATION`.

Sirve igual para el código y para el combo, porque los dos abren la misma sesión de consumo. `PantallaAtencion.razor`,
al concederse, termina la atención sin consumo (`MaquinaAtencion.Cancelar`, con la causa `CONFIGURATION`), abre
Configuración con `AccesoOperadorIdentificacion` (`OPERATOR_IDENTIFICATION`) y hace que Consumos adopte la sesión, como
el engranaje. Si se deniega, la atención sigue y la pantalla muestra el aviso del motivo
(`MensajesAtencion.DeMotivo`).

- *Descartada*: guardar el código tipeado hasta que se toque «Configuración» y llamar a `Entrar(codigo)`. El código no
  es secreto, pero `AGENTS.md` pide no retenerlo, y el combo no tiene código.
- *Descartada*: pedir la clave o el código otra vez desde el combo. David lo descartó en el gate (respuesta 4).

### D3. El botón en la pantalla, alineado con su hermano

- **En la selección**, «Configuración» va en el título, a la izquierda de «Cancelar», con la misma clase de botón
  secundario, el mismo alto táctil, la misma tipografía y el mismo relleno. Los dos comparten la línea de base y el
  borde superior.
- **En la pantalla sin opciones**, va en `.acciones`, a la izquierda de «Listo». Los dos comparten alto, ancho mínimo y
  tipografía; «Listo» sigue siendo el principal.

- *Descartada*: poner «Configuración» en la cabecera. La cabecera no tiene controles (requisito de
  `presentacion-puesto`), y el engranaje no se muestra durante una atención.

### D4. La bitácora con el camino

`CONFIGURATION_OPENED` lleva `Access` `OPERATOR_IDENTIFICATION` y el operador como actor. Lo que se guarda en esa
Configuración usa el mismo `Access`, porque `_acceso` es uno solo por apertura. Ningún registro lleva el código.

- *Descartada*: registrar `OPERATOR` con un campo `Source` aparte. Un solo campo, `Access`, ya dice cómo se entró en todos
  los eventos de Configuración, y sumar otro obligaría a mirar dos.

## Risks / Trade-offs

- **El aviso por correo.** Un operador sin comidas sigue disparando `PersonaSinConsumo` al identificarse, como hoy,
  aunque vaya a Configuración. Si David lo quiere silenciar, va en un change aparte.
- **La sesión de consumo se cierra antes que la privilegiada se abra**, en la misma transacción: si la base falla, no
  queda ninguna abierta, y la pantalla muestra el error de almacenamiento.
- **El combo sin validación** abre Configuración a cualquiera frente a la pantalla en mantenimiento con la simulación
  encendida. Es lo que pidió David, porque el combo no existe en operativo.
