## 1. La base

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: los datos del comprobante llevan el nombre
  visible del comensal y nunca el código; con correlativo desde 1000, dos confirmaciones dan 1000 y 1001, y bajar el
  «desde» no reusa; sin correlativo no hay número guardado; una reimpresión llega con «REIMPRESIÓN · copia N» y el nombre
  del operador, y un original recuperado sin leyenda ni operador; Consumos muestra el correlativo y lo encuentra (D1, D2
  y D5). Cambian, por la decisión 13, las afirmaciones de «DUPLICADO» de `AsistenciaTests` y `TrabajosImpresionTests` y
  la de ninguna propiedad de la persona en los datos de `TrabajosImpresionTests`, que sigue exigiendo que no estén el
  código ni la credencial. Verifica: rojo antes y verde después. Hecho: 6 nuevas (4 en `ComprobanteEnriquecidoTests`,
  2 en `AsistenciaTests`), 4 vistas fallar (el código de siempre y el original recuperado ya andaban), y las 4 existentes
  cambiadas, vistas fallar con «DUPLICADO» y la persona ausente.
- [x] 1.2 `UnidadConfirmacion` (el nombre y el correlativo), `TrabajosImpresion` (la leyenda y el operador),
  `ComprobanteAImprimir.Habilito` y `ConsultaConsumos` (el número y la búsqueda). Verifica: 1.1 en verde y
  `Infrastructure.Sqlite.Tests` entero. Hecho: Sqlite 289 de 289, dos veces; una corrida intermedia tuvo una caída
  aislada de `ConsultaDisponibilidadTests` que no se repitió en cinco corridas de la clase.

## 2. El comprobante en el puesto

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: las 50 frases (distintas, de hasta 60 caracteres, con su
  cierre, la misma para el mismo comprobante); `puesto.json` con su validación y los valores por defecto ante un archivo
  ilegible; la impresora simulada y el comprobante en pantalla con el puesto, el número, el comensal, «Habilitó:» en una
  copia y la frase antes del pie; la prueba de impresión con el puesto y una frase, sin persona ni número (D3, D4 y D6).
  Cambian, por la decisión 13, «DUPLICADO» en `PantallaAtencionTests` y `TextosComprobanteTests` y la ausencia del nombre
  en `ComprobanteEnPantallaTests` y `AccionesEnConsumosTests` («Ver»), que siguen exigiendo que no esté el código.
  Verifica: rojo antes y verde después. Hecho: 9 nuevas (7 en `ComprobanteDelPuestoTests`, 1 en
  `ComprobanteEnPantallaTests` y 1 en `PruebaImpresionTests`), las 9 vistas fallar contra los esqueletos; las de
  «DUPLICADO» de pantalla y textos fallaron con la leyenda del grupo 1, y la de «Ver», con el comensal nuevo.
- [x] 2.2 `FrasesComprobante`, `AlmacenDatosPuesto`, `ImpresoraSimulada`, `ComprobanteEnPantalla` y su dibujo, y la prueba
  de impresión. Verifica: 2.1 en verde. Hecho: Kiosk 520 de 520.

## 3. En la pantalla

- [x] 3.1 Pruebas nuevas, vistas fallar antes: «Datos del comprobante» en Impresora guarda el puesto y la numeración, avisa,
  deja `CONFIGURATION_CHANGED` de `TICKET_DATA` y vale desde la próxima confirmación; los campos fuera de forma se señalan
  sin guardar; con un operador, la sección se ve como el resto de Impresora (D7). Verifica: rojo antes y verde después. Hecho: 3
  en `DatosDelComprobanteTests`, vistas fallar porque la sección no existía, y en verde.
- [x] 3.2 La sección, sus registros y el cableado del «desde» del correlativo en la ventana. Verifica: 3.1 en verde y
  `Kiosk.Tests` entero, con las pruebas existentes que cambien listadas con su porqué. Hecho: Kiosk 523 de 523; el
  ayudante `PuestoDePrueba` arma el servicio como la ventana, con el «desde» de `puesto.json`, sin cambiar ninguna
  afirmación.

## 4. El puesto compilado, documentos y verificación integral

- [x] 4.1 E2E nueva a 1024 × 768: guardar los datos del comprobante con correlativo, medir sus campos y su «Guardar» contra
  los de los textos, confirmar un consumo y leer en el comprobante en pantalla el puesto, el número 1000, el comensal y una
  frase, con capturas miradas (D8). Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde. Hecho:
  `ComprobanteE2ETests` en verde. Encontró la columna de rótulos de cada sección de ancho distinto (los campos nuevos
  empezaban 105 px más a la derecha) y el «desde» corto: las dos con la columna de 12,5 em y el «desde» hasta el borde;
  todos los campos desde x 273,5 hasta x 960, de 56 px y 18 px de letra; el comprobante en pantalla termina en 426 px.
- [x] 4.2 `readme.md` (el comprobante, con el algoritmo del código) y la fila que corresponda de
  `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso` con su control
  negativo, `enlaces rotos: 0`. Hecho: 0, con el control en 2.
- [x] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas. Hecho sobre `1c75d72` (07:16 a 07:22):
  validate 16, build sin advertencias, el paquete con su E2E y 1027 de 1027 (Contracts 23, Domain 91, Sqlite 289, Kiosk
  523, Sync.Api 62, E2E 39). La primera, sobre `739139e`, falló por `xUnit1051` en la E2E nueva. La final, después del
  archivado, va en el PR.

## 5. La verificación

- [x] 5.1 Corregir lo que encontró la verificación por un agente sobre `739139e` (1 crítico, 7 advertencias, 11
  sugerencias). Verifica: las pruebas nuevas vistas fallar antes y la integral en verde. Hecho: los dos mensajes que
  todavía decían DUPLICADO (el aviso de incertidumbre y el de la copia enviada); el pedido del motivo con el número de la
  fila; `TextosComprobanteTests` vuelve a comparar los datos del comprobante y no una línea por posición;
  `TrabajosImpresionTests` prohíbe toda propiedad de la persona salvo `PersonName`; la copia que repite el comensal y el
  correlativo con la persona renombrada; «Ver» y la prueba de impresión en el delta; el costo medido del correlativo
  (unos 95 ms cada 100.000 comprobantes) como deuda en el diseño; el operador de la versión más nueva; el comando del
  nombre liberado; el nombre del puesto sin espacios de más y el «desde» ignorado con el código de siempre; lo último
  leído si `puesto.json` está tomado; el número de un `TicketNumber` que no es número; la frase de un comprobante
  conocido fijada; la fila OWASP con `ContratoEventosTests` y el guion sin DUPLICADO. Pruebas: 5 nuevas, 2 vistas fallar
  (los mensajes y el pedido del motivo); las otras 3 fijan lo que ya andaba. Sqlite 290, Kiosk 526.
