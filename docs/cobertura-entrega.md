# Cobertura de la primera entrega

La entrega 1 presenta el diseño técnico del MVP. Sigue las secciones de la
plantilla oficial y reúne descripción, arquitectura, datos, API, historias,
tickets y registro de uso de IA.

| Requisito o sección | Evidencia | Contenido |
|---|---|---|
| Ficha del proyecto | [README 0](../readme.md#0-ficha-del-proyecto) | Autor, producto, descripción, repositorio y modalidad académica. |
| Objetivo, características y funciones | [README 1](../readme.md#1-descripción-general-del-producto), [SDD](especificacion-sdd.md) | Problema, usuarios, cuatro funcionalidades obligatorias y límites del MVP. |
| Stack y arquitectura | [README 2](../readme.md#2-arquitectura-del-sistema), [arquitectura](arquitectura.md) | Componentes, responsabilidades, tecnologías, decisiones y alternativas. |
| Experiencia de usuario | [Bocetos](experiencia.md), [flujo](../diagrams/flujo.svg) | Pantallas y estados diseñados para el recorrido principal y la recuperación. |
| Estructura e infraestructura | [README 2.3–2.4](../readme.md#23-descripción-de-alto-nivel-y-estructura-de-ficheros), [despliegue](../diagrams/despliegue.svg) | Estructura documental y futura solución, topología y puesta en marcha prevista. |
| Seguridad | [Arquitectura](arquitectura.md), [SDD](especificacion-sdd.md) | Identidad de equipo, permisos locales, minimización, biometría y protección de datos. |
| Pruebas | [Plan de pruebas](pruebas.md) | Casos de dominio, persistencia, contratos, recuperación e integración. Su ejecución corresponde a las siguientes entregas. |
| Modelo de datos | [README 3](../readme.md#3-modelo-de-datos), [diccionario](modelo-datos.md), [ER](../diagrams/datos.mmd) | Entidades, atributos, tipos, claves, relaciones y restricciones. |
| Coherencia con INWeb | [Convención y mapas](nomenclatura-integracion.md), ADR11, D-C18, RNF15/AT51 | Correspondencia de 44 entidades y 256 atributos representados en el ER; identidades y JSON conservados. Diccionario físico completo y pruebas de ejecución previstos en M0/implementación. |
| API: tres operaciones principales | [OpenAPI 3.1](../api/openapi.yaml) | Configuración, envío de hechos y recuperación de acuses; ejemplos y errores. |
| Tres historias de usuario | [README 5](../readme.md#5-historias-de-usuario), [historias y épicas](historias-y-tickets.md) | HU01.1, HU02.1 y HU02.2, de tamaño real, dentro de las épicas HU01–HU03; enunciados, precondiciones, escenarios y terminado. El backlog de historias hijas cubre los 35 escenarios. |
| Tres tickets de trabajo | [README 6](../readme.md#6-tickets-de-trabajo), [tickets](historias-y-tickets.md#tickets-de-trabajo) | TK01.1 backend, TK02.1 frontend y TK03.1 datos, de tamaño tarea, con 3 o 4 AT propios, responsable, dependencias disponibles y terminado en lenguaje llano. |
| Registro de IA | [prompts.md](../prompts.md) | Herramientas, modelos, skills, subagentes, prompts representativos y correcciones humanas. |
| Priorización y secuencia | [Plan académico](plan-academico.md), [D-C24](decisiones.md) | Primer corte: cinco historias must y una should, segunda ola diseñada y no demostrada, tallas por unidad, regla de recorte, entregas y evidencia prevista. |

## Alcance de esta etapa

La lección 1 del Proyecto Final establece que E1 es completamente documental.
El código funcional, los comandos de instalación probados, los resultados de
pruebas operativas y las revisiones de código se incorporarán en las etapas de
implementación. Los diagramas, pantallas y contratos de este paquete expresan
el diseño que se desarrollará.

El curso recomienda planificar de tres a cinco historias imprescindibles y
una o dos historias deseables; para la entrega final requiere de tres a cinco
funcionalidades completas. La entrega 1 comprometía doce historias must; la
devolución de LIDR marcó ese alcance como excesivo y D-C24 (18/10/2026) lo
recortó a cinco must y una should alrededor de un flujo de punta a punta:
HU01.1, HU01.3, HU02.1, HU02.2 sin el escenario F y HU03.1, más HU01.2 reducida
al código. El resto es la segunda ola, diseñada y no demostrada. Las ampliaciones
fuera del MVP no forman parte del compromiso de aceptación académico.

## Fuentes de la consigna

- [Plantilla oficial del proyecto](https://github.com/LIDR-academy/AI4Devs-finalproject).
- [Plantilla de registro de prompts](https://github.com/LIDR-academy/AI4Devs-finalproject/blob/main/prompts.md).
- “Proyecto Final Master AI4Devs”: ficha, entregas progresivas y registro de IA.
- “Lección 1 — Todo lo que necesitás saber para arrancar tu Proyecto Final”:
  alcance de E1, prioridades y criterios del proyecto.

Las lecciones se consultaron en la copia del curso actualizada al 26/09/2026.
La especificación SDD y las tablas de trazabilidad son el método elegido para
mantener coherencia entre requisitos, decisiones y pruebas.

## Decisiones confirmadas y límites de preparación

El autor confirmó el 29/09 ambas políticas multipuesto como must (D-C19), capacidad de 6 h semanales y M0 objetivo 06/10; el 18/10, D-C24 enmendó esa prioridad y pasó multipuesto a la segunda ola, conservando la capacidad ([plan y riesgo de capacidad](plan-academico.md#capacidad-y-reestimación)). Códigos no reasignables, revocación al activar snapshot sin programación futura, dos roles sin herencia y mantenimiento con cuenta local individual se recogen en D-C20–D-C23. AT56 agrega aceptación de mantenimiento a HU03.3/U08, sin ampliar las cuatro funcionalidades.

El modelo conserva 44 entidades, 256 atributos y 47 relaciones representados. Los 19 atributos existentes pendientes y las estructuras de cuenta/historial aún por diseñar se declaran en el diccionario; no se cuentan como implementados. Esta entrega acredita diseño documental; contratos complementarios de M0, software, pruebas de producto y homologación siguen pendientes.
