# sincronizacion-lotes Specification

## Purpose

Definir cómo responde el receptor central a los lotes de hechos que envían los puestos, de modo que ninguna respuesta haga avanzar la cola de un puesto sin un resultado durable (RB16), y nunca autorice, deniegue ni revierta un consumo ya confirmado.

## Requirements

### Requirement: Ningún acuse sin ingesta durable

Mientras la ingesta de lotes no esté implementada, el receptor SHALL responder `501` con un cuerpo `application/problem+json` a `POST /api/incomedor/v1/sync-batches` y a `GET /api/incomedor/v1/sync-batches/{batchId}`. MUST NOT responder un código 2xx, persistir nada ni incluir en la respuesta contenido del pedido.

#### Scenario: Envío de un lote
Origen: TK01.1 (operación declarada) y AT27, cláusula «la cola sólo avanza con resultado durable» (RB16), sin cerrar AT27.
- **GIVEN** el receptor en marcha
- **WHEN** un cliente envía un lote sintético por `POST /api/incomedor/v1/sync-batches`
- **THEN** la respuesta es `501` con tipo de contenido `application/problem+json`
- **AND** ningún código 2xx acusa el lote

#### Scenario: Consulta de un lote
Origen: TK01.1, trabajo incluido 6, sin cerrarlo.
- **GIVEN** el receptor en marcha
- **WHEN** un cliente consulta `GET /api/incomedor/v1/sync-batches/{batchId}` con un UUID sintético
- **THEN** la respuesta es `501` con tipo de contenido `application/problem+json`

#### Scenario: Cuerpo que no es JSON
Origen: sin AT propio; OWASP A03 (inyección) y A05 (configuración insegura): una entrada inválida no produce un error interno ni se refleja.
- **GIVEN** el receptor en marcha
- **WHEN** un cliente envía por `POST` un cuerpo que no es JSON y que contiene una cadena reconocible
- **THEN** la respuesta es `501` con `application/problem+json`, no `500`
- **AND** la respuesta no contiene esa cadena

#### Scenario: Ruta no declarada
Origen: sin AT propio; el receptor expone sólo las operaciones del OpenAPI.
- **GIVEN** el receptor en marcha
- **WHEN** un cliente pide una ruta que el OpenAPI no declara
- **THEN** la respuesta es `404`
