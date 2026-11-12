# Proposal

## Why

Con los cinco changes del corte archivados, el flujo principal funciona, pero sólo se ve corriéndolo a mano. La entrega final pide «al menos un test E2E que recorra el flujo principal de principio a fin» (L58:96), evidencia de funcionamiento (L58:111-117) y un README para revisar en local, con datos de prueba, `.env.example` y, si aplica, `docker-compose` (L58:121). Para una app de escritorio, la evidencia es un Release con el EXE (L59:57) o un video de 2 a 3 minutos (L58:64). Hoy no hay nada de eso. La devolución de la entrega 1 pidió además «un pipeline que compile, pruebe y publique», y la CI todavía no publica.

Va ahora porque es la acción 3 del plan del análisis del 28/10, que David aprobó tal cual el 29/10 para el final, sobre el flujo completo. El spike del 29/10 probó que Playwright llega por CDP al WebView2 del puesto real (4 corridas de 4), con una condición: el puerto tiene que entrar por código.

Origen: el escenario E2E de `docs/pruebas.md:46` («Monopuesto/CONTINUE_LOCAL: identificación, selección, commit local, ticket y sincronización. Circuito completo con servidor apagado y luego recuperado»), que junta HU01.1 con TK02.1, HU03.1, HU02.1 y HU02.2 con TK01.1, y sus AT01, AT02 y AT27. No agrega una historia: hace verificable de punta a punta lo que las cinco must del corte (D-C24) ya construyeron.

## What Changes

- **Tres costuras en el puesto, inertes sin su variable**:
  - el puerto de depuración de Chromium (`INCOMEDOR_CDP_PORT`), que entra por código porque una app elevada ignora la variable documentada de WebView2;
  - la carpeta de la demostración (`INCOMEDOR_DEMO_DIR`), con la base, los comprobantes, los certificados y los datos del WebView2, para que el E2E no toque la del usuario;
  - la dirección del receptor (`INCOMEDOR_RECEPTOR_URL`), para que el E2E no choque con una demostración abierta en `https://localhost:5001`.

  Sin las variables, el puesto se comporta exactamente como hoy.
- **El E2E del flujo principal**, en un proyecto de pruebas nuevo de la solución: lanza el receptor y el puesto compilados como procesos, maneja la pantalla con Playwright por CDP y recorre el escenario de `docs/pruebas.md:46`. Corre con `dotnet test INComedor.sln`, en local y en la CI.
- **Un paquete autocontenido del puesto** para Windows x64: un script lo publica, corre el E2E contra el EXE publicado y lo comprime. Un workflow nuevo, `release.yml`, lo hace al empujar un tag y deja un Release en **borrador**: publicarlo es de David.
- **«Cómo revisar» en el README**: tres caminos (el Release, el código fuente y el E2E), los códigos de prueba, los datos sembrados, `.env.example` y la línea que dice que `docker-compose` no aplica. Va con un `.env.example` de referencia y el guion del video, que graba David.
- **Evidencia y tallas**: `docs/pruebas.md`, `docs/aplicacion-del-curso.md`, `docs/cobertura-entrega.md`, `docs/plan-academico.md`, `prompts.md` y `AGENTS.md`.

Ningún cambio es **BREAKING**: no cambia una prueba existente, una migración, `api/openapi.yaml` ni `.github/workflows/ci.yml`.

## Non-goals

- **Grabar, subir o publicar**: el video, el push del tag y la publicación del borrador del Release son de David.
- **El instalador** (MSI o MSIX), la firma del EXE y la actualización automática. El paquete es un zip.
- **El receptor dentro del Release**: necesita SQL Server, así que se revisa desde el código fuente.
- **Un E2E por cada historia, o con hardware real**: la L58:96 pide uno del flujo principal. La impresora sigue simulada y la biometría es de la segunda ola (D-C24).
- **Cambios en `ci.yml`**: si la CI no puede abrir la ventana o no trae el runtime de WebView2 o LocalDB, el paso que falte es de David (punto 1 del gate).
- **Docker y la infraestructura como código**, descartados en el §7 del catálogo; el README lo dice.

## Capabilities

### New Capabilities

- `distribucion-puesto`: cómo se entrega el puesto a quien lo revisa. Un paquete autocontenido para Windows x64 que abre sin el SDK de .NET, sin datos ni secretos adentro, y probado de punta a punta antes de ofrecerse.

### Modified Capabilities

- `modo-demostracion`: se agregan tres requirements, para la carpeta de la demostración, la dirección del receptor y el puerto de depuración, cada uno elegido por una variable de entorno y sin efecto sin ella. Los requirements existentes no cambian.

## Impact

- **Código**:
  - `src/Intelektron.INComedor.Kiosk`: la resolución del entorno de la demostración, en una clase nueva, y su uso en `MainWindow`.
  - `src/Intelektron.INComedor.Sync.Api` no cambia: ya toma la carpeta, la base y el puerto de su configuración.
- **Pruebas**: un proyecto nuevo, `tests/Intelektron.INComedor.E2E.Tests`, y un archivo nuevo en `Kiosk.Tests`. No cambia ninguna prueba existente.
- **Dependencia nueva**: `Microsoft.Playwright` 1.64.1, sólo en las pruebas E2E. Trae su driver y no baja navegadores: el navegador es el WebView2 Runtime del equipo.
- **CI**: el E2E entra al job `build-test` sin tocar `ci.yml`. Nunca corrió en `windows-latest`; ver el punto 1 del gate.
- **Workflow nuevo**: `.github/workflows/release.yml`, que se dispara sólo con un tag `v*` y tiene `contents: write` sólo en su job.
- **Documentos**: `readme.md` (§1.4 y §2.6), `.env.example` y `docs/guion-video.md` nuevos, `AGENTS.md` (comandos), `docs/pruebas.md`, `docs/plan-academico.md`, `docs/aplicacion-del-curso.md`, `docs/cobertura-entrega.md` y `prompts.md`.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David, 30/10/2026 (en el gate) | **S** | 2 a 4 h | 2,9 a 5,7 h de capacidad |
| La IA (Claude Code, Opus 5.5), 30/10/2026 | **S** | 2 a 4 h | 2,9 a 5,7 h de capacidad |

El colchón es el de la lección 50 («Buffers realistas: 30-40%, no 10%»): se compromete el 70 % de la capacidad, así que cada talla pide su valor dividido por 0,7.

**Por qué la IA da S.** Parte de las tres mediciones del plan académico, sin promediar:
- **Change 3**: la IA dijo M, y el apply le llevó 0,2 h para 10 tareas.
- **Change 4**: dijo S, estimó 1 a 1,5 h de reloj, y le llevó 1,0 h para 24 tareas y 81 pruebas.
- **Change 5**: dijo S, estimó 1,5 a 2,5 h, y le llevó 2,0 h para 28 tareas y 138 pruebas, con dos fricciones de entorno (LocalDB y un transitivo vulnerable).

Este change tiene menos código que el 5: tres costuras, una clase y del orden de 20 pruebas. Pero tiene más fricciones de entorno, como el change 5:
- la ventana real, con tiempos de arranque, el ciclo de envío de 30 s y la carpeta de datos del WebView2;
- la publicación autocontenida, con `wwwroot` y las bibliotecas nativas de SQLite y WebView2;
- un workflow que no puede correr hasta que David empuje un tag.

La estimación de reloj de la IA es de 1,5 a 2,5 h, en el piso de la S. **La S no cubre lo tuyo**: grabar el video, empujar el tag, publicar el borrador y seguir «Cómo revisar» desde un clon limpio.

No se promedia (lección 50: «Se converge por discusión»): si tu talla difiere, la diferencia se conversa en el gate.

## Para el gate de David

**Resultado del gate, 30/10/2026:** David lo aprobó tal cual, los doce puntos, y puso talla **S**, la misma de la IA, así que no hay diferencia que conversar.

Cada punto lleva la opción que se tomó y la que se descartó. El detalle está en el design.

1. **El E2E corre en la CI y en local** (D6). Tomada: entra a la solución, así que `dotnet test INComedor.sln` lo corre en los dos lados sin tocar `ci.yml`, y el evaluador lo ve en verde en la CI. El runner `windows-latest` nunca abrió una ventana WPF con WebView2 en este repo, y LocalDB sigue sin probar (WARNING 4 del change 5). Si la primera corrida falla por el entorno, el E2E lo dice con un mensaje propio y no se saltea. El paso que falte en `ci.yml` (instalar el runtime de WebView2, arrancar LocalDB, o filtrar el E2E) es tuyo. Descartada: sólo en local, fuera de la solución, que deja el E2E sin barrera automática y sin evidencia en la CI.
2. **El Release lo arma un workflow y lo publicás vos** (D8). Tomada: `release.yml` se dispara cuando empujás un tag `v*`. Compila, prueba, publica el paquete, corre el E2E contra el EXE publicado y deja un **borrador** con el zip y su SHA-256. Tu parte: empujar el tag y publicar el borrador. Responde al pedido de la devolución de «un pipeline que compile, pruebe y publique». Descartada: `gh release create` desde tu equipo, que no arma el paquete en un entorno limpio ni deja la publicación en la CI.
3. **El Release sale después del SDK estable** (D8 y D-C25). Tomada: el tag va después del paso a .NET 11 estable del 10/11, para no publicar dos veces. El workflow y el script se prueban antes, en local, con la RC. Descartada: publicar ya con la RC2 y republicar el 10/11.
4. **El video lo grabás vos, con el guion del change** (D9). Tomada: `docs/guion-video.md` lleva el recorrido en pasos con sus tiempos (2 a 3 minutos), y el video va como archivo del Release, enlazado desde el README. Descartadas:
   - que lo arme la IA con capturas: un video sin narración muestra menos que el E2E y sus capturas;
   - YouTube no listado (L58:176, que es de la opción B): agrega una cuenta y una plataforma fuera del repo.
5. **`.env.example` como archivo de referencia** (D10). Tomada: un archivo con las variables vacías y comentadas, como pide la L58:125 («Deja un `.env.example` con las variables vacías»). El README aclara que nada lo carga, porque el puesto lee el entorno, y muestra cómo fijarlas en PowerShell. Descartada: sólo una línea en el README, que cumple la L58:121 pero no la L58:125.
6. **`docker-compose` no aplica, y el README lo dice** (D10). Es una app de escritorio con su base local, y Docker está fuera por el §7 del catálogo. La L58:121 lo pide «si aplica», y el análisis (líneas 63 y 121) pide decirlo.
7. **El paquete es la carpeta autocontenida en un zip, no un único archivo** (D7). Tomada: `dotnet publish` autocontenido para `win-x64`, que deja el EXE con el runtime al lado. Descartado: `PublishSingleFile`, que tiene que extraer `wwwroot` y las bibliotecas nativas de SQLite y WebView2, y no se probó.
8. **La costura de CDP va en todos los builds, inerte sin la variable** (D2). Tomada: el EXE del Release es el mismo que prueba el E2E. El puerto escucha sólo en `127.0.0.1` y sólo con un número entre 1024 y 65535, y su riesgo (OWASP A05) está en el design. Descartado: `#if DEBUG`, que deja al Release sin la costura y lo saca del E2E.
9. **Tres variables y no una** (D3). Cada una tiene su motivo: la carpeta aísla los datos, la dirección evita el choque de puertos y el puerto de CDP abre la depuración. Descartada: una sola variable «modo E2E», que mezcla aislamiento con depuración, cuando una demostración aislada sin puerto abierto también sirve.
10. **Ninguna prueba existente cambia.** `git diff --stat -- tests/` sólo puede mostrar archivos nuevos.
11. **Aplicar con `opus` · `xhigh`, supervisado.** El apéndice manda la línea de seguridad cuando hay seguridad en alcance: un puerto de depuración y un workflow con permiso de escritura. Descartado: `opus` · `medium`.
12. **Tu talla**: S (arriba, en «Tallas»).
