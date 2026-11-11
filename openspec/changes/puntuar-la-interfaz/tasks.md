## 1. La prueba que recorre los mensajes

- [ ] 1.1 `PuntoFinalTests` en `Kiosk.Tests`, vista fallar antes: las constantes de `MensajesAtencion` terminan en punto
  salvo `TituloReposo` y `SinRed`, que no; los métodos que arman oraciones, con valores de ejemplo, también; `Motivo`,
  `EstadoBitacora` y `UsoDelDia` no (D2 y D3). Verifica: rojo antes y verde después.

## 2. El punto y la mudanza

- [ ] 2.1 Las oraciones sueltas de `PantallaAtencion.razor`, `PanelConfiguracion.razor`, `PanelPersonas.razor`,
  `PantallaDemostracion.razor`, `Red/ResultadoRed.cs` y `SesionOperador.cs` pasan a `MensajesAtencion`, y cada oración
  lleva su punto (D1 y D2). Verifica: 1.1 en verde y `dotnet build INComedor.sln -warnaserror` sin advertencias.
- [ ] 2.2 Las pruebas existentes que comparan un mensaje con un literal suman el punto, y nada más (D4, decisión 13).
  Verifica: `Kiosk.Tests` y `Infrastructure.Sqlite.Tests` en verde, y la lista de archivos cambiados pegada.

## 3. El puesto compilado, documentos y verificación integral

- [ ] 3.1 Las E2E que comparan un mensaje con un literal suman el punto; la E2E entera contra el puesto compilado, con una
  captura del reposo y de un aviso a 1024 × 768 mirada. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests` en
  verde.
- [ ] 3.2 `prompts.md`, el criterio humano 16. Verifica: el chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso`
  con su control negativo, `enlaces rotos: 0`.
- [ ] 3.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
