# Guion del video

Un recorrido de 2 a 3 minutos por el puesto (D9 de `demostrar-flujo-de-punta-a-punta`, que lo ponía como archivo del
Release; desde el 12/11/2026 va a YouTube como no listado) y que el README enlaza en «Cómo revisar». Lo graba David; este guion fija qué se muestra y cuánto dura
cada paso. Todo lo que aparece es sintético: los códigos son los de la tabla de «Cómo ver la demostración» del
[README](../readme.md#cómo-ver-la-demostración).

**El video publicado.** El [recorrido de 2:17](https://youtu.be/_wxPGLIcuJk) del 12/11/2026 sigue un guion posterior a
éste, con la reimpresión de `reimprimir-sin-codigo`: el flujo principal con el receptor apagado, el cupo del día, la
impresora sin papel, la recuperación y la reimpresión con la clave del día, que sale «SIN VALIDEZ: PRUEBA DE
FUNCIONALIDAD», y la sincronización cuando vuelve el receptor, con la espera del ciclo recortada y rotulada; después, el
change en OpenSpec, sus pruebas vistas fallar y la CI de `cab59de`. Lo produjo Claude Code: manejó el EXE del Release
v0.8.0 con toques emulados y locución sintética en español rioplatense, sobre datos sintéticos. Los pasos de abajo
quedan como el recorrido para hacerlo a mano.

## Herramienta y carpeta propia

- **Grabación:** la Herramienta Recortes de Windows 11, en su modo de grabación de pantalla, sobre la ventana del
  puesto. No hace falta instalar nada.
- **Carpeta propia:** para no grabar la base de uso diario, el puesto y el receptor usan una carpeta aparte, que se
  borra al terminar. En cada terminal, antes de abrir nada:

  ```powershell
  $env:INCOMEDOR_DEMO_DIR = "$env:TEMP\incomedor-video"
  $env:Receptor__CarpetaDemostracion = "$env:INCOMEDOR_DEMO_DIR\certificados"
  $env:Receptor__BaseDemostracion = 'INComedorReceptorVideo'
  ```

## Antes de grabar

1. En una terminal, desde la raíz del repo, arrancar el receptor una vez para que genere los certificados y
   registre el puesto, y cerrarlo con Ctrl+C:
   `dotnet run --project src/Intelektron.INComedor.Sync.Api -- --demostracion`. Sin LocalDB, la instancia de SQL
   Server va en `INCOMEDOR_SQLSERVER`.
2. Tener a mano el zip del paquete descomprimido (el del Release o, mientras no haya uno publicado, el que arma
   `publicar-puesto.ps1`, como dice el camino 1 del README), y en el navegador la corrida de la CI del PR con el E2E en verde.
3. Dejar la impresora simulada en «Disponible».

## Los pasos

| Paso | Tiempo | Qué se ve | Qué se dice |
|---|---|---|---|
| 1 | 15 s | La portada del README | INComedor es un puesto de autoservicio de comedor que atiende sin red; el video recorre el corte de la entrega |
| 2 | 15 s | El EXE del paquete, abierto desde la terminal con la carpeta propia: el reposo con la lista de la simulación | Es el paquete autocontenido: abre sin .NET instalado y crea su base sintética al arrancar; en mantenimiento, la simulación deja elegir a la persona de una lista |
| 3 | 30 s | Tocar el código con el dedo, `0001` con el teclado en pantalla e «Ingresar»; «Almuerzo común», el cartel «¿Confirmás tu elección?», «Confirmar» y el comprobante en pantalla con el nombre del comensal | Identificación simulada, selección y confirmación atómica; el comprobante sale después del commit, con el nombre y nunca el código |
| 4 | 15 s | `0001` otra vez: la selección ya no ofrece ningún almuerzo, sólo los refrigerios, y dice «Hoy ya usaste: almuerzo 1 de 1» | El cupo se aplica en el puesto, sin consultar a nadie, y la persona ve sólo lo que puede consumir |
| 5 | 20 s | El engranaje y la clave del día con su teclado numérico; en Configuración, la pestaña Impresora en «Sin papel»; `0001` con «Café con leche»: el consumo registrado y el comprobante pendiente | Un fallo de papel nunca crea otro consumo: el consumo queda registrado y el comprobante, pendiente |
| 6 | 30 s | El engranaje con el código `9001`: Configuración sólo con Consumos, Personas e Impresora; la impresora en «Disponible» y, en Consumos, «Recuperar» el pendiente y «Reimprimir» otro con un motivo, que sale con REIMPRESIÓN y «Habilitó:» | El operador entra con su código, recupera el mismo trabajo y reimprime una copia marcada, con un motivo de una lista fija y su nombre como actor |
| 7 | 30 s | La franja con los pendientes; en la otra terminal, el receptor de vuelta; la franja «al día» | Con el receptor apagado la atención sigue; al volver, el puesto envía sus lotes sin duplicarlos, en el ciclo de 30 s |
| 8 | 15 s | La corrida de la CI con el E2E en verde | El mismo flujo lo recorre una prueba de punta a punta en cada push, y el Release se arma sólo si pasa |

Total: 170 s.

En el paso 7, el receptor se arranca al empezar el paso, con el mismo comando de «Antes de grabar», y la espera del
ciclo se narra o se corta en la edición. Si la CI todavía no tiene el E2E en verde, el paso 8 muestra la corrida local:
`dotnet test tests/Intelektron.INComedor.E2E.Tests`.

## Después de grabar

1. Cerrar el puesto y el receptor, y borrar la carpeta propia (`$env:TEMP\incomedor-video`) y la base
   `INComedorReceptorVideo`.
2. Subir el video a YouTube como no listado.
3. En el README, cambiar «Pendiente» por el enlace al video en «Cómo revisar».
