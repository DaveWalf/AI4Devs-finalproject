# Tasks

Sólo cambian las tres pruebas existentes del punto 3 del gate, con la aprobación de David. Al cerrar cada grupo, `git diff --diff-filter=M --name-only <base> -- tests/` lista sólo `PantallaAtencionTests.cs`, `PantallaDistribucionTests.cs` y `ConfiguracionPorPestanasE2ETests.cs`, donde `<base>` es la punta de la ampliación del 1 sobre la que se apila este change. Si otra prueba existente falla, se para y se le pregunta a David. Cada prueba nueva se ve fallar antes de implementar lo que la hace pasar, y su `DisplayName` empieza con la capability y el nombre del escenario. Las de `Kiosk.Tests` y la E2E corren con `INCOMEDOR_SQLSERVER` apuntando a la instancia del equipo, como pide `AGENTS.md`.

## 1. El cartel de la elección

- [ ] 1.1 Agregar a `Kiosk.Tests` el archivo nuevo `CartelEleccionTests.cs`, con «Elegir abre el cartel con lo elegido», «Elegir otra vuelve a la selección», «Escape en el cartel», «Opción sin cargo» (cartel sin importe) y «Treinta segundos sin actividad» con el cartel abierto. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~CartelEleccion"`.
- [ ] 1.2 Cambiar `PantallaAtencionTests.Controles_de_la_seleccion_son_botones` y `PantallaDistribucionTests.Con_nueve_los_controles_son_botones` para que toquen una opción antes de buscar «Confirmar», y sumar a la primera que «Cancelar» está fuera del cartel y que, con el cartel abierto, el título y la lista están `inert`. Verlas fallar con el filtro `FullyQualifiedName~Controles_de_la_seleccion|FullyQualifiedName~Con_nueve_los_controles`.
- [ ] 1.3 Implementar el cartel (D1), «Cancelar» en `.titulo` sin la barra del pie (D2) y la inercia y Escape (D3) en `PantallaAtencion.razor`, con su CSS en `wwwroot/css/puesto.css` (D4). Verificar con los filtros de 1.1 y 1.2 que pasan, y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que no hay ninguna otra falla.

## 2. El cartel en el puesto compilado

- [ ] 2.1 Agregar a `E2E.Tests` el archivo nuevo `CartelEleccionE2ETests.cs`: en 1024 × 768, sin opción elegida no hay «Confirmar»; al tocar una, el foco está en «Confirmar», el cartel y sus dos botones miden al menos 56 px y entran en la ventana; «Elegir otra» lo cierra; y confirmar desde el cartel llega a «Consumo registrado». Cambiar `ConfiguracionPorPestanasE2ETests.La_ventana_angosta_sigue_desplazandose` para que toque la última opción y mida «Confirmar» del cartel dentro de la ventana. Verlas fallar con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~CartelEleccionE2E|FullyQualifiedName~La_ventana_angosta"` contra el EXE publicado de la ampliación del 1.
- [ ] 2.2 Publicar con `pwsh .github/scripts/publicar-puesto.ps1 -Version 0.0.0-local` y verificar con el filtro de 2.1, con `INCOMEDOR_E2E_KIOSK_EXE` apuntando al EXE publicado, que pasan. Correr también `PresentacionTactilTests`: si `Nueve_opciones_en_1024_por_768_y_una_columna_en_600` falla, se para y se le pregunta a David (riesgo del design).

## 3. Documentos y evidencia

- [ ] 3.1 Actualizar el paso 1 del recorrido de `readme.md` («Almuerzo común» y «Confirmar» en el cartel) y sumar la evidencia a la fila que corresponda de `docs/aplicacion-del-curso.md`, sin enlace propio a la carpeta del change. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, que hay 0 rotos.
- [ ] 3.2 Desde la raíz del repo, correr y pegar las salidas: `dotnet build INComedor.sln -warnaserror` con 0 advertencias, `dotnet test INComedor.sln` con 0 fallas, `openspec validate --all --strict` sin errores y el `git diff` de pruebas modificadas del encabezado, con sólo los tres archivos.

## Workflow follow-up

- La revisión de David (el gate de `proposal.md`) va antes del apply, y lo que decida queda escrito en `proposal.md`.
- Después de su prueba en el ejecutable, con los tres cambios juntos, `/openspec-verify-change` y el archivado en el mismo PR, que apunta a `feature/simular-identificacion-por-lista-dw` y se integra después de ella. El push y el merge son de David.
