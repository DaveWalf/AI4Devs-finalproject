# Tasks

Ninguna prueba existente cambia (D10): `git diff --diff-filter=M --stat 1000578 -- tests/` tiene que salir vacío al terminar cada grupo, con `1000578` la punta de `feature/ofrecer-solo-opciones-disponibles-dw` sobre la que se apila esta rama. Cada prueba nueva se ve fallar antes de implementar, con la salida pegada. Si una prueba existente falla, se para y se le pregunta a David (punto 5 del gate). Las pruebas de archivos usan una carpeta temporal real por caso; para que un archivo no admita escritura se le pone el atributo de sólo lectura, y para que una carpeta no la admita se crea un archivo con su nombre.

## 1. El escritor de archivos

- [ ] 1.1 Agregar `Infrastructure.Sqlite.Tests/BitacorasExternasTests.cs` con los escenarios de «Dos archivos mensuales por puesto», «Cada línea se entiende sola», «Cada registro va a su bitácora» y «La atención no espera al archivo» (el de orden; el del archivo bloqueado va en 1.5), llamando a la pasada del escritor sin temporizador y con un reloj fijo. Crear el escritor con `throw new NotImplementedException()` y ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~BitacorasExternas"`; pegar la salida
- [ ] 1.2 Implementar la severidad de `RegistroBitacora` (D12), el reparto (D3), la línea con `ExportedAtUtc` y `Recovered` (D5), la carpeta contenida en la de datos (D11) y la pasada con la marca leída del archivo (D2, D4). Verificar con el filtro de 1.1 en verde y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde
- [ ] 1.3 Agregar a `BitacorasExternasTests` los escenarios de «Reanudar tras una caída sin duplicar» (caída después de escribir, línea parcial, base y archivo que no coinciden), armando cada archivo a mano antes de la pasada. Ver fallar con el filtro de 1.1; pegar la salida
- [ ] 1.4 Implementar la reparación y la detección del desajuste (D6). Verificar con el filtro de 1.1 en verde
- [ ] 1.5 Agregar los escenarios de «Una falla de archivo no detiene la atención» y «Recuperar un archivo», más el del archivo bloqueado durante una confirmación de «La atención no espera al archivo» y una falla repetida tres pasadas que deja una sola incidencia. Ver fallar con el filtro de 1.1; pegar la salida
- [ ] 1.6 Implementar las incidencias agrupadas y la recuperación hasta el corte (D7). Verificar con el filtro de 1.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## 2. Lo que falta para reconstruir

- [ ] 2.1 Agregar `Infrastructure.Sqlite.Tests/ContextoReconstruibleTests.cs` con los escenarios de «Lo mostrado queda en el registro» (la autorización con sus cargos, el rechazo al confirmar y la oferta con la etiqueta y el importe de cada ofrecida) y uno de `SESSION_OPENED` al aceptar una identificación, con la sesión, el propósito, la persona y el método, sin el código. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ContextoReconstruible"`; pegar la salida
- [ ] 2.2 Ampliar los contextos de `OPTIONS_OFFERED`, `CONSUMPTION_AUTHORIZED` y `CONSUMPTION_DENIED` sin cambiar los campos que ya tienen (D10), registrar `SESSION_OPENED` en la transacción de la identificación y crear la clase pública con la que la atención registra en su propia transacción corta (D8). Verificar con el filtro de 2.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## 3. La atención, Configuración y el puesto

- [ ] 3.1 Agregar `Kiosk.Tests/BitacorasDelPuestoTests.cs`, con máquina, bUnit y la pasada del escritor sobre una carpeta temporal, para los escenarios de «Una atención se reconstruye con su archivo» (autorizada, cancelada, inactividad y cambio de página, leyendo sólo el archivo de interacciones), «El puesto y Configuración quedan en la general» (guardar el estilo, guardar sin poder escribir, el modo de la impresora y la versión desconocida) y «Configuración muestra el estado de las bitácoras» (al día y degradada). Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~BitacorasDelPuesto"`; pegar la salida
- [ ] 3.2 Implementar en `MaquinaAtencion` los registros de D8, en `ServicioAtencion` el escritor (arranca al terminar `Preparar`, cada 2 s, y `Dispose` espera la pasada en curso y hace la última) con `KIOSK_STARTED` y `KIOSK_STOPPED`, y en la pantalla y en Configuración los registros de D9 y el estado de cada bitácora. Verificar con el filtro de 3.1 en verde, `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## 4. De punta a punta

- [ ] 4.1 Agregar `E2E.Tests/BitacorasExternasE2ETests.cs` sin tocar los ayudantes existentes: el puesto compilado, una atención autorizada por CDP, el cierre ordenado del puesto y la lectura de los dos archivos de su carpeta (en interacciones, de `SESSION_OPENED` a `SESSION_CLOSED` con la misma sesión; en la general, `KIOSK_STARTED` y `KIOSK_STOPPED`; en ninguno, el código). Como el puesto ya escribe cuando esta prueba nace, verla fallar con una afirmación imposible (un `EventType` que no existe) y revertirla, y verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde; pegar las dos salidas
- [ ] 4.2 Medir «Ingresar» y elegir una opción con el cronómetro de la verificación de A (dos corridas de 60 vueltas, quince y nueve opciones, SQLite real en disco) y comparar con sus medianas de 8,4 a 10,0 ms. Pegar mediana y p95; si una mediana sube más de 3 ms, anotarlo como riesgo abierto para el gate de la verificación

## 5. Documentos

- [ ] 5.1 Cambiar en `AGENTS.md` la línea del primer corte: las bitácoras mensuales salen de la segunda ola por este change, y se nombra lo que sigue ahí (generación al restaurar, saltos del reloj, evento de emergencia, diagnóstico y conservación). Verificar con `git grep -n "bitácoras mensuales" -- AGENTS.md`, que muestre la línea nueva
- [ ] 5.2 Actualizar `readme.md` (dónde están las bitácoras y un paso del recorrido de verificación que abre el archivo de interacciones), `docs/modelo-datos.md` y `docs/arquitectura.md` (la marca de entrega es el archivo, D2, con el enlace a este change) y las dos filas de `docs/aplicacion-del-curso.md` que dicen que las bitácoras son de la segunda ola. Verificar con `git grep -nE "bitácoras y diagnóstico, que (son|quedan para la) segunda ola" -- docs/aplicacion-del-curso.md` sin resultados (hoy da dos, las líneas 117 y 164)
- [ ] 5.3 Sumar la evidencia del change a `docs/aplicacion-del-curso.md`, con el nombre del change y sin enlace propio a su carpeta (D5 de `mostrar-aplicacion-del-curso`), y su entrada en `prompts.md` si el apply deja un ajuste humano nuevo. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo)

## 6. Integración

- [ ] 6.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## Workflow follow-up

- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR.
- Redactar el borrador del PR en `C:\AI\INComedor\ai4devs\gestion\PR-escribir-bitacoras-externas-dw.md`, con la forma de `PR-ofrecer-solo-opciones-disponibles-dw.md`.
