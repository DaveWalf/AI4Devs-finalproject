# Guion del video

Un recorrido de 2 a 3 minutos por el puesto (D9 de `demostrar-flujo-de-punta-a-punta`), que acompaña al Release
como archivo y que el README enlaza en «Cómo revisar». Lo graba David; este guion fija qué se muestra y cuánto dura
cada paso. Todo lo que aparece es sintético: los códigos son los de la tabla de «Cómo ver la demostración» del
[README](../readme.md#cómo-ver-la-demostración).

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
2. Tener a mano el zip del Release descomprimido, y en el navegador la corrida de la CI del PR con el E2E en verde.
3. Dejar la impresora simulada en «Disponible».

## Los pasos

| Paso | Tiempo | Qué se ve | Qué se dice |
|---|---|---|---|
| 1 | 15 s | La portada del README | INComedor es un puesto de autoservicio de comedor que atiende sin red; el video recorre el corte de la entrega |
| 2 | 15 s | El EXE del Release, abierto desde la terminal con la carpeta propia | Es el paquete autocontenido: abre sin .NET instalado y crea su base sintética al arrancar |
| 3 | 30 s | `0001`, «Almuerzo común», «Confirmar» y el mensaje «Consumo registrado. Retirá tu comprobante» | Identificación simulada por código, selección y confirmación atómica; el comprobante sale después del commit |
| 4 | 15 s | `0001` otra vez con el mismo almuerzo, y el mensaje de cupo | El cupo se aplica en el puesto, sin consultar a nadie |
| 5 | 20 s | La franja de la impresora en «Sin papel», `0001` con «Café con leche» y «El comprobante queda pendiente» | Un fallo de papel nunca crea otro consumo: el consumo queda registrado y el comprobante, pendiente |
| 6 | 30 s | La impresora en «Disponible», `9001` y «Asistencia»: «Recuperar» el pendiente y «Reimprimir» otro con un motivo, que sale con DUPLICADO | El operador recupera el mismo trabajo y reimprime una copia marcada, con un motivo de una lista fija |
| 7 | 30 s | La franja con los pendientes; en la otra terminal, el receptor de vuelta; la franja «al día» | Con el receptor apagado la atención sigue; al volver, el puesto envía sus lotes sin duplicarlos, en el ciclo de 30 s |
| 8 | 15 s | La corrida de la CI con el E2E en verde | El mismo flujo lo recorre una prueba de punta a punta en cada push, y el Release se arma sólo si pasa |

Total: 170 s.

En el paso 7, el receptor se arranca al empezar el paso, con el mismo comando de «Antes de grabar», y la espera del
ciclo se narra o se corta en la edición. Si la CI todavía no tiene el E2E en verde, el paso 8 muestra la corrida local:
`dotnet test tests/Intelektron.INComedor.E2E.Tests`.

## Después de grabar

1. Cerrar el puesto y el receptor, y borrar la carpeta propia (`$env:TEMP\incomedor-video`) y la base
   `INComedorReceptorVideo`.
2. Subir el video como archivo del Release en borrador, junto al zip y su suma, y publicarlo.
3. En el README, cambiar «Pendiente» por el enlace al video en «Cómo revisar».
