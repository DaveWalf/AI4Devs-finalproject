## 1. El lector sin pantalla

- [x] 1.1 `ConfiguracionLector` con su validación y `AlmacenLector` (`lector.json`), validado al leer y al guardar (D3);
  el armado de tramas con el terminador, el largo, los caracteres, el resto viejo y el relleno (D4). Pruebas nuevas
  vistas fallar primero. Verifica: `dotnet test --project tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [x] 1.2 `IPuertoSerie`, `PuertoSimulado` y `PuertoSerieDelSistema` con `System.IO.Ports` `11.0.0`, y el servicio
  `LectorIdentificacion`: abre, reintenta cada 5 segundos, publica el estado y cada lectura, y registra
  `READER_STATE_CHANGED` (D1, D2 y D9). Pruebas nuevas con el puerto simulado vistas fallar primero; ninguna abre un
  puerto COM. Verifica: Kiosk en verde. Hecho (1.1 y 1.2): `LectorTests` (8), las ocho vistas fallar contra un esqueleto
  con `NotImplementedException`; `System.IO.Ports` 11.0.0 en `Directory.Packages.props` y en el `Kiosk`, sin avisos de
  NuGet Audit con `-warnaserror`.

## 2. La lectura en la atención

- [x] 2.1 El origen de la lectura en el registro de la identificación, sin cambiar el tipeado (D7). Prueba nueva vista
  fallar primero. Verifica: `dotnet test --project tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` en verde.
- [x] 2.2 `MaquinaAtencion.IdentificarPorLector` y la pantalla: identifica en reposo, descarta en una atención, con un
  diálogo o con el error de almacenamiento, con `READER_READING_DISCARDED` a lo sumo una vez por motivo y por minuto
  (D5 y D7); el simulador interno en reposo sólo en mantenimiento (D6). Pruebas nuevas vistas fallar primero. Verifica:
  Kiosk en verde. Hecho (2.1 y 2.2): `OrigenDeLaLecturaTests` (1, el origen en la clave `Origin` de siempre) y
  `LectorEnPantallaTests` (6), vistas fallar con la sobrecarga sin origen y sin el cableado (la de operativo pasa también
  sin él); Sqlite 297, Kiosk 580, sin cambiar ninguna prueba existente.

## 3. La pestaña Lector

- [x] 3.1 La pestaña después de Red, sólo con la clave del día: el medio, el puerto con «Actualizar», el formato, el
  terminador, el relleno, «Guardar» con `CONFIGURATION_CHANGED` de `READER`, el estado y la última lectura sin el código
  (D8). Cambian, por la decisión 13, las pruebas que fijan las diez pestañas: `PestanasConfiguracionTests`,
  `AccesoOperadorTests` y `AccionesEnConsumosTests`; y `PuntoFinalTests` clasifica los mensajes nuevos. Pruebas nuevas
  vistas fallar primero. Verifica: Kiosk en verde. Hecho: `LectorEnConfiguracionTests` (5), cuatro vistas fallar con la pestaña vacía (la del
  operador pasa también sin la sección); el relleno lateral de las pestañas baja de 10 a 7 px; las tres existentes pasan
  de diez a once pestañas y `PuntoFinalTests` clasifica `EstadoLector`, `UltimaLectura` y `PuertosDelEquipo`; Kiosk 585.

## 4. La pantalla de verdad y los documentos

- [ ] 4.1 E2E nueva a 1024 × 768: la fila de once pestañas entra; la pestaña Lector medida contra Impresora; con el
  simulador interno y el relleno 4, `1` por «Lector simulado» identifica a `0001`, con capturas miradas (D6 y D8).
  Cambian, por la decisión 13, `AccesoOperadorE2ETests`, `ConfiguracionPorPestanasE2ETests`, `ExportacionE2ETests` y
  `CorreoE2ETests`, que cuentan diez. Verifica: la E2E en verde.
- [ ] 4.2 `AGENTS.md` (sale de la segunda ola la lectura de tramas serie), `readme.md` (el medio, el simulador y cómo
  probar el puerto real) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces con
  su control negativo, `enlaces rotos: 0`.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
