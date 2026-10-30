## Purpose

Entregar el puesto a quien lo revisa como un paquete para Windows que abre sin herramientas de desarrollo, sin datos ni secretos adentro, y que antes de ofrecerse pasó la prueba de punta a punta del flujo principal.

## ADDED Requirements

### Requirement: Paquete autocontenido del puesto

El puesto SHALL distribuirse como un zip para Windows x64 que contiene el EXE con el runtime de .NET a su lado, y que abre la atención en un equipo con el WebView2 Runtime aunque no tenga el SDK ni el runtime de .NET. El zip MUST NOT contener una base, comprobantes, certificados ni secretos, y MUST acompañarse de su suma SHA-256.

#### Scenario: Abre sin .NET instalado
Origen: L59:57 («Publica el artefacto (JAR, EXE) como una *Release*») y L58:111-117 (evidencia de funcionamiento).
- **GIVEN** el zip descomprimido en una carpeta nueva, y un entorno de proceso sin ninguna carpeta de `dotnet` en `PATH` y con `DOTNET_ROOT` apuntando a una carpeta vacía
- **WHEN** se abre el EXE
- **THEN** la ventana muestra la pantalla de reposo de la atención

#### Scenario: Sin datos ni secretos
Origen: L58:125 («nunca subas secretos al repositorio») y `AGENTS.md`, «Never commit secrets».
- **GIVEN** el zip recién armado
- **WHEN** se listan sus entradas
- **THEN** ninguna termina en `.db`, `.pfx`, `.key` o `.pem`, y ninguna está bajo una carpeta `comprobantes` o `certificados`

#### Scenario: Un archivo de datos colado en la publicación
Origen: sin AT propio; la condición anterior vista desde el error.
- **GIVEN** una publicación que, por un error, contiene un archivo `.db`
- **WHEN** se arma el zip
- **THEN** el armado termina con error, nombra el archivo y no deja ningún zip

#### Scenario: La suma acompaña al zip
Origen: sin AT propio; integridad del artefacto descargado (OWASP A08).
- **GIVEN** el zip y su archivo de suma
- **WHEN** se calcula el SHA-256 del zip descargado
- **THEN** coincide con el del archivo de suma

### Requirement: Paquete probado antes de ofrecerse

Un paquete SHALL ofrecerse para descarga sólo después de que la prueba de punta a punta del flujo principal pasó contra el EXE de ese mismo paquete, y sólo como borrador, hasta que una persona lo publique. Si la prueba falla, MUST NOT quedar ningún Release nuevo. Un push que no es un tag de versión MUST NOT armar ni ofrecer ningún paquete.

#### Scenario: Prueba en verde
Origen: el E2E de `docs/pruebas.md:46` y el pedido de la devolución de la entrega 1, «un pipeline que compile, pruebe y publique».
- **GIVEN** un tag de versión `v*` empujado sobre un commit cuyo E2E pasa
- **WHEN** corre la publicación
- **THEN** queda un Release en borrador con el zip y su suma, y nadie lo ve publicado hasta que una persona lo publique

#### Scenario: Prueba en rojo
Origen: sin AT propio; L58:96, el E2E recorre el flujo principal.
- **GIVEN** un EXE publicado contra el que el E2E falla
- **WHEN** corre la publicación
- **THEN** termina con error antes de crear un Release, y no queda ningún borrador nuevo

#### Scenario: Push a una rama
Origen: sin AT propio; la publicación es de David (`AGENTS.md`, «Push y merge son de David»).
- **GIVEN** un push a una rama `feature/**`
- **WHEN** corren los workflows del repo
- **THEN** ninguno arma un paquete ni crea un Release
