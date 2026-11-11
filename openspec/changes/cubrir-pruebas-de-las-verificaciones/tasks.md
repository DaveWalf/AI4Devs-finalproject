## 1. La oferta

- [ ] 1.1 La rama de `CatalogoCambiado` que termina sin opciones (`MaquinaAtencion.cs`, verificación de A del 04/11): con
  la opción elegida retirada por un cambio de versión y todo el cupo del día usado, confirmar deja la atención sin
  opciones, sin elegida y sin consumo. Verifica: la prueba nueva en verde, vista fallar antes con una afirmación
  imposible.
- [ ] 1.2 El `OPTIONS_OFFERED` que se repite al cambiar el catálogo antes de confirmar (A): un segundo registro en la
  misma sesión, sin la opción retirada y con la categoría agotada. Verifica: ídem.

## 2. La administración local

- [ ] 2.1 El modo de la impresora con una versión de INWeb activa (C, 06/11): «Sin papel» se aplica mientras Opciones
  sigue bloqueada. Verifica: ídem.
- [ ] 2.2 El replay con `ReasonCause` (C): una denegación fuera de horario repetida con el mismo `IntentId`, ya dentro de
  la franja, devuelve la decisión original con su causa. Verifica: ídem.
- [ ] 2.3 La pantalla de la baja (C, cubierta sólo por composición): quitar a `0001`, que ya consumió, y tipear su código
  muestra el mensaje de `CREDENTIAL_REVOKED`; su consumo sigue y Personas la muestra de baja. Verifica: ídem.
- [ ] 2.4 La pantalla de la reconfirmación (C, cubierta sólo por composición): con «Almuerzo común» elegido, la
  administración lo lleva a 1.800,00 y «Confirmar» muestra el valor nuevo y pide reconfirmar. Verifica: ídem.

## 3. El acceso

- [ ] 3.1 El código de una persona al pasar a operativo (verificación del 10/11): `0001` en la clave de «Pasar a
  operativo» dice que un código no es la clave, el modo sigue en mantenimiento y la general tiene `DAILY_KEY_REJECTED`
  con `ACTIVATE_OPERATIVE` y `PERSON_CODE`, sin `0001`. Verifica: ídem.
- [ ] 3.2 Las guardas de D6 sobre el modo y la simulación: no se alcanzan desde la pantalla, porque con el acceso de un
  operador sus botones no existen. Se anotan como no probables, sin tocar el código. Verifica: la nota en este archivo.

## 4. Cierre

- [ ] 4.1 Sin código de producto ni pruebas existentes: `git diff --stat origin/final-project-dw -- src/` y
  `git diff --diff-filter=M --name-only origin/final-project-dw -- tests/` vacíos. Verifica: las dos salidas pegadas.
- [ ] 4.2 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados.
