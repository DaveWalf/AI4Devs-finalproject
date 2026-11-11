## Context

`MensajesAtencion` reúne casi todo lo que la pantalla del puesto dice: 55 constantes y métodos como `DeMotivo`,
`VersionGuardada` o `ExportacionHecha`. Hasta `buscar-y-exportar-consumos`, ninguna oración terminaba en punto. Quedan
fuera de esa clase unas pocas oraciones: las de `PantallaAtencion.razor` («Confirmando: el puesto está ocupado», «Tocá el
comprobante para retirarlo» y los tres pedidos de la clave del día), la indicación de `PanelConfiguracion.razor`, el
`title` de `PanelPersonas.razor`, el error de `PantallaDemostracion.razor`, los resultados de `ResultadoRed` y los de
`SesionOperador`. La regla de `openspec/config.yaml` dice que toda oración termina en punto y que un título, un rótulo, un
botón, una opción o un estado no.

## Goals / Non-Goals

**Goals:** cada oración de la pantalla con su punto final; una prueba que lo sostenga para los mensajes que vengan.

**Non-Goals:** reescribir mensajes; el comprobante impreso; las bitácoras; las specs y el `readme.md`, que citan los
mensajes con el punto fuera de las comillas.

## Decisions

### D1. Las oraciones viven en `MensajesAtencion`

Las oraciones sueltas de los componentes, de `ResultadoRed` y de `SesionOperador` se mudan a `MensajesAtencion`, como
constantes o como métodos con sus partes variables (el paso de la red, el número de la copia). Los componentes las usan
desde ahí. Así la prueba de D3 las ve todas, y un texto nuevo tiene un lugar obvio.

- **Descartada: puntuar los literales donde están.** Quedarían fuera de la prueba, y el próximo texto suelto volvería a
  salir sin punto.

### D2. Qué no lleva punto

No llevan punto, por ser títulos, rótulos, botones, opciones o estados: `TituloReposo` (el título del reposo), `SinRed`
(un estado de la pestaña Red), `Motivo` (los botones del motivo de la reimpresión), `EstadoBitacora` («Bitácora general:
al día»), `UsoDelDia` («Hoy ya usaste: almuerzo 1 de 1»), los estados de las filas de Personas y Opciones («Nueva», «Se
da de baja al guardar», «Sale del catálogo al guardar»), la leyenda «Identificación simulada por código (demostración)»,
los rótulos de las franjas («Sin receptor configurado», «Sin papel») y el detalle de cada etapa de la prueba de conexión
(«no se resolvió …»), que va después del nombre de la etapa, como un estado. La cabecera «INComedor · tu pausa del día»
es un título.

- **Descartada: puntuar también los estados.** La RAE no pone punto a títulos, rótulos ni enunciados que funcionan como
  tales, y un «Activa.» en una celda de tabla se lee como un error.

### D3. La prueba

`PuntoFinalTests` (en `Kiosk.Tests`) recorre por reflexión las constantes `string` públicas de `MensajesAtencion` y exige
que terminen en punto, salvo una lista explícita (`TituloReposo` y `SinRed`), que además afirma que ésas no lo llevan.
Otro caso llama a cada método que arma una oración con valores de ejemplo (`DeMotivo` con cada motivo conocido y uno
desconocido, `VersionGuardada`, `ExportacionHecha`, `CarpetaElegida`, `PedidoOperadorPara`, los de la red y los de la
asistencia), y otro afirma que `Motivo`, `EstadoBitacora` y `UsoDelDia` no terminan en punto. Una constante nueva sin
punto, o un título nuevo sin pasar por la lista, la hacen fallar.

- **Descartada: un analizador de Roslyn sobre los literales.** Distinguir un título de una oración pide el mismo juicio
  que la lista, a un costo mucho mayor.

### D4. Las pruebas existentes y los documentos

Las pruebas que comparan un mensaje con su constante no cambian. Las que lo comparan con un literal cambian sólo para
sumar el punto (decisión 13 del plan), y la tarea las lista. Las specs y el `readme.md` no cambian: citan los mensajes
entre comillas latinas, con el punto de la oración citada fuera de ellas. `prompts.md` actualiza el criterio humano 16.

## Risks / Trade-offs

- [Un mensaje se compone con otro y queda con dos puntos] → no hay composiciones fuera de `ExportacionHecha` con su aviso,
  que ya separa las oraciones con un espacio; la prueba de `ExportarConsumosTests` compara el texto entero.
- [Un punto alarga una línea y la parte en dos] → la E2E entera, que mide la atención y Configuración a 1024 × 768, corre
  en la verificación integral.

## Migration Plan

Sin migración: son textos de la pantalla. La reversión es revertir el PR.

## Open Questions

Ninguna.
