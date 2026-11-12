## ADDED Requirements

### Requirement: Carpeta de la demostración elegida por el entorno

Si la variable `INCOMEDOR_DEMO_DIR` trae una ruta absoluta, el puesto SHALL guardar en esa carpeta la base, los comprobantes de la impresora simulada, los certificados de la demostración y los datos de su WebView2, y MUST NOT crear ni modificar la carpeta por defecto. Sin la variable, o con una ruta relativa, MUST usar `%LOCALAPPDATA%\INComedor\demo\`, como hasta ahora.

#### Scenario: Carpeta elegida por la variable
Origen: el E2E de `docs/pruebas.md:46` y AT01 (monopuesto sin red); `AGENTS.md`, «se reinicia borrando esa carpeta».
- **GIVEN** `INCOMEDOR_DEMO_DIR` con la ruta absoluta de una carpeta que no existe
- **WHEN** arranca la atención y una persona confirma un servicio
- **THEN** esa carpeta tiene la base, con el consumo, y el comprobante de la impresora simulada
- **AND** la carpeta `%LOCALAPPDATA%\INComedor\demo\` no se creó, o, si ya existía, no cambió ningún archivo

#### Scenario: Sin la variable
Origen: sin AT propio; la conducta de `conectar-puesto-y-receptor` y de `atender-seleccion-y-confirmacion`.
- **GIVEN** que `INCOMEDOR_DEMO_DIR` no está definida
- **WHEN** arranca la atención
- **THEN** el puesto usa `%LOCALAPPDATA%\INComedor\demo\` para la base, los comprobantes y los certificados

#### Scenario: Ruta relativa
Origen: sin AT propio; OWASP A05, una configuración ambigua no elige un destino que dependa del directorio de trabajo.
- **GIVEN** `INCOMEDOR_DEMO_DIR` con el valor `demo-relativa`
- **WHEN** arranca la atención
- **THEN** el puesto usa `%LOCALAPPDATA%\INComedor\demo\`
- **AND** no se crea ninguna carpeta `demo-relativa` bajo el directorio de trabajo

### Requirement: Dirección del receptor elegida por el entorno

Si la variable `INCOMEDOR_RECEPTOR_URL` trae una dirección absoluta con esquema `https`, el puesto SHALL enviar sus lotes a esa dirección. Sin la variable, o con cualquier otro valor, MUST usar `https://localhost:5001/`. Ningún valor de la variable MUST hacer que un lote salga por un canal sin TLS.

#### Scenario: Dirección https elegida por la variable
Origen: HU02-C y AT27 (reenvío y acuse), con el receptor de demostración en otro puerto.
- **GIVEN** el receptor de demostración escuchando en `https://localhost:<puerto>/`, con `<puerto>` distinto de 5001, y `INCOMEDOR_RECEPTOR_URL` con esa dirección
- **WHEN** el puesto confirma un consumo y corre un ciclo de envío
- **THEN** el receptor tiene ese consumo consolidado y el estado de la sincronización dice que está al día

#### Scenario: Dirección sin TLS
Origen: AT13 (credencial de equipo) y OWASP A02, un lote no viaja en texto plano.
- **GIVEN** `INCOMEDOR_RECEPTOR_URL` con `http://localhost:<puerto>/`
- **WHEN** arranca la atención
- **THEN** el puesto usa `https://localhost:5001/`
- **AND** ningún pedido llega a `http://localhost:<puerto>/`

#### Scenario: Valor que no es una dirección
Origen: sin AT propio; una variable mal escrita no deja al puesto sin receptor por defecto.
- **GIVEN** `INCOMEDOR_RECEPTOR_URL` con el valor `localhost:5001`, sin esquema
- **WHEN** arranca la atención
- **THEN** el puesto usa `https://localhost:5001/`

### Requirement: Puerto de depuración sólo con la variable

El puesto SHALL abrir el puerto de depuración remota de su WebView2 sólo si la variable `INCOMEDOR_CDP_PORT` trae un número entero entre 1024 y 65535, y sólo en la interfaz de loopback. Sin la variable, o con otro valor, MUST NOT abrir ningún puerto de depuración, y la ventana MUST abrir igual. Esto vale también cuando el puesto corre elevado.

#### Scenario: Puerto válido
Origen: el spike del 29/10 (`docs/spikes/spike-e2e-cdp-20261029.md`) y L58:96, «al menos un test E2E».
- **GIVEN** `INCOMEDOR_CDP_PORT` con un puerto libre entre 1024 y 65535
- **WHEN** arranca la atención
- **THEN** un cliente del protocolo de depuración de Chromium se conecta en `127.0.0.1` y ese puerto y encuentra la pantalla de reposo, con el campo del código
- **AND** ninguna dirección que no sea de loopback acepta conexiones en ese puerto

#### Scenario: Sin la variable
Origen: sin AT propio; OWASP A05, el puesto que se entrega no deja la depuración abierta.
- **GIVEN** que `INCOMEDOR_CDP_PORT` no está definida
- **WHEN** arranca la atención
- **THEN** ningún proceso del puesto, el EXE o sus procesos del WebView2, escucha conexiones TCP

#### Scenario: Valor fuera de rango o que no es un número
Origen: sin AT propio; OWASP A05.
- **GIVEN** `INCOMEDOR_CDP_PORT` con `abc`, con `80` o con `70000`
- **WHEN** arranca la atención
- **THEN** la ventana abre en reposo y ningún proceso del puesto escucha conexiones TCP
