# Proposal

## Why

LIDR devolvió la entrega 1 con luz amarilla: documentación completa y alcance excesivo. El paquete compromete doce historias must en cuatro funcionalidades para unas 38 h de capacidad, cuando el curso pide de 3 a 5 historias must y 1 o 2 should alrededor de un flujo de punta a punta (L58:43, L58:59). El 18/10/2026 David aceptó el recorte y enmendó D-C19; el 19/10 eligió aplicarlo con OpenSpec antes de escribir código, para que cada change siguiente parta de un contexto corto y de un alcance cerrado.

Origen: devolución de LIDR de la entrega 1 y decisión D-C24. No nace de una historia ni de un ticket: ordena el backlog del que salen TK03.1, TK02.1 y TK01.1.

## What Changes

- **D-C24** en `docs/decisiones.md`: el recorte es la versión que se presenta en la entrega 2 y la final. Enmienda D-C19 sin borrarla.
- **Historias del corte**, de doce must a cinco must y una should, cada una por su papel en el flujo identificación → selección → confirmación → comprobante → envío:
  - must **HU01.1** Confirmar un servicio sin red y sin duplicarlo: la selección y la confirmación son el centro del flujo; la identificación simulada por código entra como su precondición.
  - must **HU01.3** Recibir la decisión según cupo y reglas locales: sin cupo ni fecha de negocio la confirmación no decide nada.
  - must **HU02.1** Recuperar el resultado tras una caída: el replay por `IntentId` es lo que hace atómica a RB06 frente a una caída.
  - must **HU02.2** Reenviar lotes y consolidarlos sin duplicar, sin el escenario F: outbox y receptor idempotente cierran el flujo hacia el centro (backend y base de la plantilla).
  - must **HU03.1** Recuperar o reimprimir un comprobante con permiso: el comprobante con reimpresión es parte del corte; sin él, un fallo de papel no tiene salida.
  - should **HU01.2** reducida al código: escenario J (código vencido, revocado, todavía no vigente o desconocido). El adaptador simulado alcanza para el flujo; validar la credencial agrega los caminos de rechazo.
- **Segunda ola, diseñada y no demostrada**, que el producto sigue requiriendo:
  - biometría 1:N y 1:1 (HU01.2-E y F);
  - multipuesto: HU02.2-F, HU02.4, HU02.5 y HU02.6, con ambas políticas y el feed;
  - configuración por snapshot (HU02.3);
  - administración y diagnóstico (HU03.2, HU03.3 con la cuenta de mantenimiento y HU03.4);
  - bitácoras mensuales (HU03.5);
  - auditoría de saltos del reloj (AT48 y AT49).
- F01–F04 conservan sus ID; F04 queda reducida a la sincronización.
- **Pantallas sólo Razor**: WPF es la ventana host del BlazorWebView. WPF XAML queda como contingencia, no como segunda tecnología (ADR01 y tabla de componentes).
- **Protección en reposo del corte** en `readme.md` §2.5.
- **OpenSpec en el repo**: `openspec/config.yaml` con el contexto del corte; `AGENTS.md` mínimo y `CLAUDE.md` que lo importa; plantilla de PR; sección «Entrega 2» de `prompts.md`.

## Non-goals

- Sin código, ni proyectos .NET, ni CI: llegan con `registrar-consumo-atomico` y los changes siguientes.
- Sin push, sin PR y sin tocar el PR #1: publicar es de David.
- No se borra ni se reescribe el diseño de la segunda ola: queda en `docs/` como visión del producto.
- No se renumeran historias, AT, RF ni RB, ni se reescribe la SDD.
- No se pasan a `openspec/specs/` los documentos previos: la spec viva crece por change.

## Capabilities

### New Capabilities

Ninguna. El change declara `skip_specs: true`: decide alcance y contexto, no comportamiento del sistema.

### Modified Capabilities

Ninguna.

## Impact

- Documentos: `docs/decisiones.md`, `docs/plan-academico.md`, `docs/cobertura-entrega.md`, `docs/arquitectura.md` (ADR01), `readme.md` (§1.2, §2.2 y §2.5) y `prompts.md`.
- Archivos nuevos: `AGENTS.md`, `CLAUDE.md` y `.github/pull_request_template.md`; `openspec/` con su configuración y las skills de `.claude/skills/` y `.agents/skills/`.
- Sin efecto en el OpenAPI ni en el modelo de datos: el design nombra las entidades que usa el corte, sin quitar ninguna del diccionario.
