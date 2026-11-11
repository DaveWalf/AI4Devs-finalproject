## 1. La prueba que recorre los mensajes

- [x] 1.1 `PuntoFinalTests` en `Kiosk.Tests`, vista fallar antes: las constantes de `MensajesAtencion` terminan en punto
  salvo `TituloReposo` y `SinRed`, que no; los métodos que arman oraciones, con valores de ejemplo, también; `Motivo`,
  `EstadoBitacora` y `UsoDelDia` no (D2 y D3). Verifica: rojo antes y verde después.
  - Escrita después de mudar las oraciones sin cambiarles el texto (`Kiosk.Tests` 428 de 428 con la mudanza sola): `total:
    4`, `error: 2` (las constantes y los mensajes armados, sin punto; las dos de «sin punto» ya pasaban, porque cuidan lo
    que no se toca). Después del punto, 4 de 4.

## 2. El punto y la mudanza

- [x] 2.1 Las oraciones sueltas de `PantallaAtencion.razor`, `PanelConfiguracion.razor`, `PanelPersonas.razor`,
  `PantallaDemostracion.razor`, `Red/ResultadoRed.cs` y `SesionOperador.cs` pasan a `MensajesAtencion`, y cada oración
  lleva su punto (D1 y D2). Verifica: 1.1 en verde y `dotnet build INComedor.sln -warnaserror` sin advertencias.
  - Mudadas: `Confirmando`, `TocaParaRetirar`, `ClaveParaAbrir`, `ClaveParaCerrar`, `ClaveParaOperativo`,
    `EdicionDePersonasAbierta`, `EdicionDeOpcionesAbierta`, `OriginalEnviado`, `ComprobanteQuedoPendiente`,
    `CopiaEnviada`, `RedCanceladaEnPaso` y `RedFallo`; la pantalla de demostración usa `ErrorAlmacenamiento`. 78
    oraciones ganaron su punto. Build: `0 Advertencia(s)`, `0 Errores`.
- [x] 2.2 Las pruebas existentes que comparan un mensaje con un literal suman el punto, y nada más (D4, decisión 13).
  Verifica: `Kiosk.Tests` y `Infrastructure.Sqlite.Tests` en verde, y la lista de archivos cambiados pegada.
  - Con el punto y las pruebas sin tocar: `Kiosk.Tests` `total: 432`, `error: 14`, todas por el punto («Expected: "Revisá
    los campos marcados"», «Actual: "Revisá los campos marcados."»). Un script sumó el punto a cada literal de prueba que
    era exactamente un mensaje, y el diff se comprobó línea por línea: 25 líneas, cada una sólo con un punto más dentro de
    un literal. Después: Kiosk `total: 432`, `error: 0`; Sqlite `total: 234`, `error: 0`. Pruebas existentes cambiadas
    del puesto: `AccesoOperadorTests.cs`, `CodigoDuplicadoEnElPanelTests.cs`, `FueraDeHorarioTests.cs`,
    `MaquinaAtencionTests.cs`, `PanelOpcionesTests.cs`, `PanelPersonasTests.cs`, `PantallaDemostracionTests.cs`,
    `PestanasConfiguracionTests.cs` y `TablaPersonasTests.cs`.

## 3. El puesto compilado, documentos y verificación integral

- [x] 3.1 Las E2E que comparan un mensaje con un literal suman el punto; la E2E entera contra el puesto compilado, con una
  captura del reposo y de un aviso a 1024 × 768 mirada. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests` en
  verde.
  - El mismo script sumó el punto en `AccesoOperadorE2ETests.cs`, `AccesoPuestoE2ETests.cs`, `AdministracionE2ETests.cs`
    (dos), `BitacorasExternasE2ETests.cs`, `CartelEleccionE2ETests.cs`, `FlujoPrincipalTests.cs` y
    `SimulacionE2ETests.cs`, que comparan con `ToHaveTextAsync`. `dotnet test tests/Intelektron.INComedor.E2E.Tests`:
    `total: 32`, `error: 0` (22:56 a 23:01). Capturas miradas: `03b-consumo-registrado` («Consumo registrado. Retirá tu
    comprobante.» y «Tocá el comprobante para retirarlo.», con punto), `04-cupo` («Hoy ya usaste: almuerzo 1 de 1», sin
    punto) y `06-reposo-tactil` (el título, la leyenda y el estado del pie, sin punto).
- [x] 3.2 `prompts.md`, el criterio humano 16. Verifica: el chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso`
  con su control negativo, `enlaces rotos: 0`.
  - El criterio 16 dice que el change puntuó el resto, con el enlace a `PuntoFinalTests`. Control negativo:
    `enlaces rotos: 2`; sobre `readme.md`, `prompts.md`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md`:
    `enlaces rotos: 0`.
- [ ] 3.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
