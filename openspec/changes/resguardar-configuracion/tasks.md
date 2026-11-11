## 1. La configuración de la base

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: leer la versión activa entera tabla por
  tabla y recuperarla en otra base (una versión nueva con las mismas personas, opciones y permisos, y la versión anterior
  conservada); rechazar una configuración sin una tabla o con columnas de más, y una con un código que en esta base es de
  otra persona; con INWeb como dueño, denegar (D1 a D3). Verifica: rojo antes y verde después. Hecho: 7 pruebas en
  `ResguardoConfiguracionTests` (con revisar sin escribir), las 7 vistas fallar con `NotImplementedException` y en verde.
- [x] 1.2 `AdministracionLocal.LeerConfiguracion` y `RecuperarVersion`. Verifica: 1.1 en verde y
  `Infrastructure.Sqlite.Tests` entero. Hecho: más `RevisarConfiguracion`; Sqlite 279 de 279.

## 2. El resguardo en el puesto

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: `ResguardoPuesto` guarda el `.zip` con el manifiesto, la
  configuración y los ajustes locales que existen, sin certificados; `Revisar` rechaza un zip dañado, una parte alterada,
  una desconocida o con `..`, otro formato o esquema y una parte local inválida, y resume uno válido; `Recuperar` crea la
  versión nueva y reemplaza los ajustes locales (D1 a D3). Verifica: rojo antes y verde después. Hecho: 4 pruebas en
  `ResguardoPuestoTests` (guardar, revisar uno válido, recuperar en otro puesto y ocho resguardos alterados), vistas fallar
  con `NotImplementedException` y en verde.
- [x] 2.2 `ResguardoPuesto`. Verifica: 2.1 en verde. Hecho: con `Interpretar`, `Serializar` y `VolverAlPorDefecto` en los
  almacenes de estilo y textos, y `Serializar`, `ImagenDe` y `Reemplazar` en el de presentación; Kiosk 501 de 501.

## 3. En la pantalla

- [x] 3.1 Pruebas nuevas, vistas fallar antes: «Guardar resguardo» (y cancelar la carpeta); «Recuperar resguardo» muestra
  el resumen con «Recuperar» y «Cancelar»; «Recuperar» deja la versión nueva y la pantalla con el estilo, el logo y los
  textos recuperados; un rechazo con su motivo; con INWeb no se ofrece recuperar; los tres registros sin códigos ni
  nombres (D4 y D5). Verifica: rojo antes y verde después. Hecho: 6 pruebas en `ResguardoEnGeneralTests`, vistas fallar
  porque General no tenía los botones, y en verde.
- [x] 3.2 Los botones de General, el cartel y los registros; el diálogo de archivos con título y filtro de `.zip`.
  Verifica: 3.1 en verde y `Kiosk.Tests` entero, con las pruebas existentes que cambien listadas con su porqué. Hecho: Kiosk 507
  de 507; cambió `PuntoFinalTests` (decisión 13, prevista en la propuesta): clasifica los seis métodos nuevos de
  `MensajesAtencion` con sus valores de ejemplo, que es lo que esa prueba exige a todo método nuevo; ninguna aserción se aflojó.

## 4. El puesto compilado, documentos y verificación integral

- [x] 4.1 E2E nueva a 1024 × 768: guardar el resguardo, cambiar el estilo, recuperarlo con el diálogo de archivos y ver el
  estilo de antes; los dos botones de General y los del cartel medidos, con capturas miradas (D6). Verifica: `dotnet test
  tests/Intelektron.INComedor.E2E.Tests` en verde. Hecho: `ResguardoE2ETests` en verde; General 207,4 × 56 px los dos a la misma altura y
  con la letra de «Cerrar» (18px); el cartel, 56 px parejos y termina en 557 px; tres capturas miradas.
- [x] 4.2 `readme.md` (General) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`. Hecho: 0, con el control
  en 2.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
