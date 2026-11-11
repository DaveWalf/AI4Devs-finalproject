## ADDED Requirements

### Requirement: Las opciones a la vista en su pestaña

La pestaña Opciones SHALL mostrar el catálogo activo, cada opción con su nombre, servicio, importe, cargas, franjas y una muestra de su tarjeta en la atención, con «Editar» y «Personalizar». «Editar» MUST abrir la edición de siempre en el mismo lugar, con el nombre de esa opción a mano; con INWeb como dueño, sólo para leer.

#### Scenario: El catálogo a la vista
Origen: mejora 7 de David del 10/11/2026 («Opciones editables en el lugar»).
- **GIVEN** el catálogo de la demostración
- **WHEN** se elige la pestaña Opciones
- **THEN** se ven todas sus opciones con su servicio, su importe y sus cargas, sin abrir la edición

#### Scenario: Editar desde una opción
Origen: mejora 7 de David del 10/11/2026, borde.
- **GIVEN** el catálogo a la vista
- **WHEN** se toca «Editar» en «Almuerzo opcional»
- **THEN** la edición se abre en el lugar del catálogo con el nombre de «Almuerzo opcional» enfocado, y «Guardar» crea una sola versión como siempre

### Requirement: Personalizar una opción

«Personalizar» SHALL abrir un cartel con la muestra de la tarjeta, un fondo de color de una paleta de ocho o una imagen PNG, WebP o SVG con las reglas de los logos, la transparencia (0, 25, 50 o 75 %) y «Mostrar el precio». Lo guardado MUST quedar en el puesto, sin crear versión ni depender del dueño de los datos, y en la general como un ajuste del equipo, también si falla.

#### Scenario: Un fondo de color con transparencia
Origen: mejora 7 de David del 10/11/2026 («color o imagen de fondo y transparencias»).
- **GIVEN** el catálogo a la vista
- **WHEN** se personaliza «Almuerzo opcional» con el tercer color, 25 % de transparencia, y se guarda
- **THEN** la versión activa no cambia, la general tiene `CONFIGURATION_CHANGED` del ajuste `OPTION_STYLE` con el antes, ese color y 25 después y el resultado, y al reabrir Configuración la muestra conserva el fondo

#### Scenario: Una imagen de fondo
Origen: mejora 7 de David del 10/11/2026, y las reglas de los logos de `configurar-puesto-por-pestanas`.
- **GIVEN** el cartel de personalización
- **WHEN** se elige una imagen PNG válida y se guarda
- **THEN** la muestra la usa de fondo y la general registra su huella SHA-256, no su contenido
- **AND** un archivo que no es una imagen válida se rechaza con el aviso de los logos y no se guarda nada

#### Scenario: Ocultar el precio
Origen: mejora 16 de David del 11/11/2026 («configurar si mostrar el precio de cada elemento o no, más allá de lo que esté configurado»).
- **GIVEN** «Almuerzo opcional» con un importe de 1.800,00
- **WHEN** se apaga «Mostrar el precio» y se guarda
- **THEN** su muestra no tiene importe y la general registra `PriceVisible` en falso después

#### Scenario: Guardar sin poder escribir
Origen: el estilo visual de `configuracion-puesto` («un guardado fallido también se reconstruye»), borde.
- **GIVEN** una carpeta de datos donde no se puede escribir la presentación
- **WHEN** se elige un color y se toca «Guardar»
- **THEN** el cartel sigue abierto con el aviso de que no se pudo guardar, nada cambia y la general tiene el resultado fallido con lo vigente

#### Scenario: Con INWeb como dueño
Origen: mejora 16 de David del 11/11/2026 («más allá de lo que esté configurado»), borde.
- **GIVEN** la configuración activa administrada por INWeb
- **WHEN** se personaliza una opción
- **THEN** su «Editar» dice «Ver» y la presentación se guarda igual

#### Scenario: Cancelar la personalización
Origen: mejora 7 de David del 10/11/2026, borde.
- **GIVEN** el cartel con un color elegido y sin guardar
- **WHEN** se toca «Cancelar», o Escape, que cierra Configuración entera
- **THEN** la muestra vuelve a como estaba y no se escribe nada

#### Scenario: Un archivo de presentación roto
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y el estilo visual de `presentar-atencion-tactil`, borde.
- **GIVEN** un `presentacion-opciones.json` con un color fuera de la paleta y una imagen que no existe
- **WHEN** el puesto lo lee
- **THEN** esas opciones se ven sin personalizar y la atención sigue
