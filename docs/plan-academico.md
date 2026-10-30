# Plan académico y trazabilidad de aceptación

Diseño de ejecución del MVP. El alcance que se presenta en la entrega 2 y la final es el primer corte de D-C24, que enmienda D-C19: cinco historias **must** y una **should** alrededor de un flujo de punta a punta, como pide el curso (de 3 a 5 must y 1 o 2 should). F01–F04 conservan sus ID. Lo que queda fuera es la **segunda ola**: diseñada en este paquete, no demostrada, y el producto la sigue requiriendo. El alcance utiliza adaptadores de prueba y SQLite real durante la implementación. Las fechas y tallas son estimaciones de planificación; no representan evidencia de implementación.

## Prioridad dentro de las funcionalidades

| Prioridad | Historia | Función | Papel en el flujo |
|---|---|---|---|
| must | HU01.1 Confirmar un servicio sin red y sin duplicarlo | F01, F02 | Selección y confirmación; la identificación simulada por código es su precondición |
| must | HU01.3 Recibir la decisión según cupo y reglas locales | F02 | Cupo y fecha de negocio deciden la confirmación |
| must | HU02.1 Recuperar el resultado tras una caída | F02 | Replay por `IntentId`: RB06 atómica ante una caída |
| must | HU02.2 Reenviar lotes sin duplicar, sin el escenario F | F04 | Outbox y receptor idempotente cierran el flujo hacia el centro |
| must | HU03.1 Recuperar o reimprimir un comprobante | F03 | Salida ante un fallo de papel, con permiso `OPERATOR` |
| should | HU01.2 reducida al código, escenario J | F01 | Rechazos por credencial vencida, revocada, todavía no vigente o desconocida |

**Segunda ola, diseñada y no demostrada:** biometría 1:N y 1:1 (HU01.2-E y F); multipuesto con ambas políticas y feed (HU02.2-F, HU02.4, HU02.5 y HU02.6); configuración por snapshot (HU02.3); administración, diagnóstico y cuenta de mantenimiento (HU03.2, HU03.3 y HU03.4); bitácoras mensuales (HU03.5); auditoría de saltos del reloj (AT48 y AT49); escalas 125/150 %. Homologación física, emisión para terceros, compensaciones económicas e interfaces empresariales siguen perteneciendo al producto posterior.

Lo should se difiere primero y, si se difiere, la entrega lo declara pendiente. La segunda ola no se recorta del producto: se conserva su diseño y se declara no demostrada.

## Hitos y criterios de salida

| Hito | Fecha | Resultado | Criterio verificable de salida |
|---|---|---|---|
| M0 Preparación de implementación | 06/10/2026 | Contratos complementarios, diseño de persistencia, estados y datos de prueba definidos | Dependencias de preparación de M1 disponibles: contratos/fixtures, criterios AT y pruebas diseñadas; certificados sintéticos y fixtures de credenciales, permisos y mantenimiento. Las dependencias de código integrado se completan en M1, no se declaran implementadas en M0 |
| M1 Vertical autónoma — Entrega 2 | 22/10/2026 | Scaffolds conectados (pantalla Razor, dominio, SQLite y receptor) y flujo principal casi completo | Change `registrar-consumo-atomico` (TK03.1) con RB06, doble toque, caída tras el commit y replay probados sobre SQLite real; CI que compila y prueba; README y `prompts.md` actualizados |
| M2 Primer corte completo — Entrega final | 12/11/2026 | Las cinco must de D-C24 de punta a punta | Identificación simulada→selección→confirmación→comprobante sin red, reimpresión con permiso y envío idempotente; unitarias, integración y un E2E del flujo principal, evidencia de despliegue y registro de IA actualizado. La should terminada se agrega; si no, se declara. La segunda ola se declara diseñada y no demostrada |

## Unidades de trabajo y tallas

Las épicas técnicas son TK01 (backend), TK02 (interfaz) y TK03 (datos). Las unidades siguientes las descomponen; los tickets de tarea TK03.1, TK02.1 y TK01.1 son la primera tarea de U02, U03 y U05. Cada unidad se implementa y revisa con cambios acotados, evidencia y pruebas propias.

Tallas de trabajo asistido: **S** de 2 a 4 h, **M** de 4 a 8 h y **L** de 8 a 16 h.

| Unidad | Hito | Responsable principal | Dependencias | Entregable y aceptación | Talla | Prioridad |
|---|---|---|---|---|---|---|
| U01 Contratos y fixture | M0 | TK01 | Ninguna | Identidad, credenciales y permisos locales, contrato de mantenimiento, ámbitos de claves INWeb, DTO explícitos, configuración, fixture coherente y validación de referencias; AT07/19–23/36/51 | M | must |
| U02 Persistencia y reloj | M0/M1 | TK03 (tarea TK03.1) | U01 | Migración inicial y transacción RB06 (TK03.1), diccionario físico según mapa, intención, reglas de fecha y observaciones; AT03–05/25/35/48–51 | L | must |
| U03 Flujo táctil e identidad | M1 | TK02 (tarea TK02.1) | U01/U02 | Selección y confirmación (TK02.1), adaptadores, credenciales, sesión/cancelación y reconfirmación; AT01/08/16/20/21/26/47/52/53 | L | must para selección, confirmación e identificación simulada; biometría y escalas 125/150 %, segunda ola |
| U04 Comprobante y recuperación | M1 | TK01 | U02/U03 | Worker, estado previo al envío, incertidumbre y copia autorizada con permiso local; AT06/24/41/54 | M | must |
| U05 Configuración y lotes | M1 | TK01 (tarea TK01.1) | U01/U02 | Receptor de lotes (TK01.1), configuración con validación en staging, cliente sincronizador y cola SQLite integrados; AT09/12/13/27/36/50/55 | M | must para receptor, cliente y cola; configuración por snapshot y AT12, segunda ola |
| U06 Archivos y recuperación | M1/M2 | TK03 | U02/U04 | Escritores de dos archivos mensuales, cola y reparación; AT41–44 | M | segunda ola |
| U07 Feed y multipuesto | M2 | TK01 | U05/contrato de feed | Difusión, ledger, `CONTINUE_LOCAL` y aviso de exceso; suspensión y cambio de política; AT30–34 | L | segunda ola |
| U08 Administración y diagnóstico | M2 | TK02 | U01, U04/U06/U07 | Roles, acceso individual de mantenimiento, parámetros, diagnóstico y estados; AT37–39/45/56 | M | segunda ola |
| U09 Calidad y demostración | M2 | TK02 | U03–U08 | Pruebas integradas, cambio de adaptadores, UI, evidencias Windows y registro de IA; AT10/24/28/46/48/49 | M | must |

### Capacidad y reestimación

David confirmó el 29/09/2026 **6 h semanales** hasta 22/10 y otras **6 h semanales** hasta 12/11, con M0 **objetivo 06/10**. Prorrateo desde 29/09, sin duplicar días entre períodos: días × 6 / 7. Las sesiones reales pueden cambiar la distribución.

| Período | Días | Capacidad disponible | Trabajo que debe imputarse |
|---|---:|---:|---|
| 29/09 → 06/10, M0 | 7 | 6,00 h | Corrección y verificación documental, U01 y diseño de U02; contratos, mantenimiento y fixtures |
| 06/10 → 22/10, M1 | 16 | 13,71 h | Implementación U02, U03–U05 y tramo de U06 requerido por AT41; pruebas y correcciones |
| 22/10 → 12/11, M2 | 21 | 18,00 h | Resto de U06, U07 con ambas políticas, U08 incluido mantenimiento y U09 |
| Total | 44 | 37,71 h | Toda la dedicación; no suma horas autónomas de IA como horas humanas adicionales |

Las tallas de las unidades son hipótesis anteriores a esta descomposición: sumaban **48–96 h**, con un desajuste nominal de **10,29–58,29 h** frente a la capacidad. No constituyen una estimación revisada ni una viabilidad demostrada. La bolsa anterior de should (6–12 h) incluía suspensión y ya no sirve como ahorro: falta medir por separado diagnóstico ampliado y escalas. Falta estimar el incremento de mantenimiento y el esfuerzo ya consumido en la corrección; no se sumarán dos veces trabajos reutilizados.

| Desglose obligatorio en M0 | Responsable | Regla de estimación incremental |
|---|---|---|
| U01 y preparación U02 | TK01/TK03 | Separar contratos/fixtures de U01 del diccionario y diseño de persistencia de U02; imputar preparación a M0 e implementación a M1 |
| U03 y HU01.1 | TK02/TK01/TK03 | Separar interfaz de dominio/datos compartidos; una talla M de historia no es la suma adicional de sus tickets |
| U05 | TK01/TK03 | Receptor TK01.1, configuración y cliente/cola son tres trabajos; M de receptor no estima automáticamente toda U05 |
| U06 | TK03 | Imputar a M1 el escritor/cola mínimo de AT41 y a M2 recuperación restante; no cargar todo U06 en M2 |
| U08 y calidad | TK02/TK01/TK03 | Estimar mantenimiento, componentes must, should separados, integración y evidencia; reservar explícitamente tiempo de correcciones |

Se reestima con una primera vertical medida y esfuerzo restante, conservando M1/M2 como fechas del curso. M0 no se declara cerrado por haber llegado al 06/10: debe cumplir sus criterios. A 6 h/semana, 48 h ocuparían ocho semanas (hasta 24/11 desde 29/09), referencia de riesgo y no nueva fecha aprobada.

#### Tallas por change y colchón

Desde `evaluar-habilitacion-local` cada change declara dos tallas, la de David y la de la IA, y no se promedian: la diferencia va al gate de David (lección 50, «Se converge por discusión»). Cada talla se divide por 0,7 para dejar un colchón del 30 % (lección 50, «Buffers realistas: 30-40%, no 10%»): se compromete el 70 % de la capacidad.

| Change | Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|---|
| [`evaluar-habilitacion-local`](../openspec/changes/archive/2026-10-29-evaluar-habilitacion-local/proposal.md#tallas) | David, 29/10/2026 | S | 2 a 4 h | 2,9 a 5,7 h |
| [`evaluar-habilitacion-local`](../openspec/changes/archive/2026-10-29-evaluar-habilitacion-local/proposal.md#tallas) | La IA (Claude Code, Opus 5.5), 29/10/2026 | M | 4 a 6 h | 5,7 a 8,6 h |
| [`atender-seleccion-y-confirmacion`](../openspec/changes/archive/2026-10-30-atender-seleccion-y-confirmacion/proposal.md#tallas) | David, 29/10/2026 | L | 8 a 16 h | 11,4 a 22,9 h |
| [`atender-seleccion-y-confirmacion`](../openspec/changes/archive/2026-10-30-atender-seleccion-y-confirmacion/proposal.md#tallas) | La IA (Claude Code, Opus 5.5), 29/10/2026 | S | 2 a 4 h | 2,9 a 5,7 h |
| [`recibir-lotes-idempotente`](../openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/proposal.md#tallas) | David, 30/10/2026 | S | 2 a 4 h | 2,9 a 5,7 h |
| [`recibir-lotes-idempotente`](../openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/proposal.md#tallas) | La IA (Claude Code, Opus 5.5), 30/10/2026 | S | 2 a 4 h | 2,9 a 5,7 h |
| [`demostrar-flujo-de-punta-a-punta`](../openspec/changes/demostrar-flujo-de-punta-a-punta/proposal.md#tallas) | David, 30/10/2026 | S | 2 a 4 h | 2,9 a 5,7 h |
| [`demostrar-flujo-de-punta-a-punta`](../openspec/changes/demostrar-flujo-de-punta-a-punta/proposal.md#tallas) | La IA (Claude Code, Opus 5.5), 30/10/2026 | S | 2 a 4 h | 2,9 a 5,7 h |

Horas reales del apply de ese change, medidas con el reloj de la sesión del 29/10/2026: **0,2 h** de la IA (12 minutos de reloj, de las 20:32 a las 20:44), con los cuatro grupos de tareas y sin la propuesta, el gate ni el informe de `/openspec-verify-change`. Quedan fuera, y no se midieron, la revisión del gate, el archivado y el PR, que son de David. La talla M de la IA sobreestimó el trabajo de la IA por un orden de magnitud; la S de David cubre además esas horas humanas. La reestimación del próximo change parte de esta diferencia y no de promediar.

Horas reales del apply de `atender-seleccion-y-confirmacion`, medidas con el reloj de la sesión del 30/10/2026: **1,0 h** de la IA (62 minutos de reloj, de las 06:29, al empezar la tarea 1.1, a las 07:31, al cerrar la 7.3). Cubren 21 tareas en siete grupos, 81 pruebas nuevas, la corrida del puesto real y una pausa por un pedido de David a mitad del apply: quitar el hook de confirmación de tests, unos 5 minutos. La IA había estimado 1 a 1,5 h de reloj y declaró S (2 a 4 h) porque la escala no tiene una talla menor: la medición cayó en el piso de su propia estimación. Igual que en el change 3, la L de David cubre el tiempo humano, que no se midió: el gate, la aprobación de las dos pruebas de D1, mirar el puesto andando y el PR.

Horas reales del apply de `recibir-lotes-idempotente`, medidas con el reloj de la sesión del 30/10/2026: **2,0 h** de la IA (118 minutos de reloj, de las 08:52, al empezar la tarea 1.1, a las 10:50, al cerrar la 9.3). Cubren 27 tareas en nueve grupos, 138 pruebas nuevas, el recorrido del puesto real con el receptor de demostración y dos pausas para decisiones de David a mitad del apply: la línea 85 de `SemillaAtencionTests` y la instancia de SQL Server. Antes, desde las 08:41, fueron el gate, el archivado del change 4 y el requirement partido. La IA había estimado 1,5 a 2,5 h de reloj, y la medición cayó en el medio, en el piso de la S que declararon los dos. De las dos fricciones que la talla preveía, LocalDB costó un diagnóstico y un cambio de D1 (la instancia de este equipo nunca arrancó), y el TLS mutuo con claves persistidas no costó nada. Apareció una tercera: un transitivo vulnerable de `Microsoft.Data.SqlClient`. Como en los changes anteriores, la S de David cubre además el tiempo humano, que no se midió.

Horas reales del apply de `demostrar-flujo-de-punta-a-punta`, medidas con el reloj de la sesión del 30/10/2026: **1,0 h** de la IA (60 minutos de reloj, de las 12:48, al empezar la tarea 1.1, a las 13:47, al cerrar la 8.2). Cubren 19 de las 21 tareas en ocho grupos: la 8.3 y el informe de `/openspec-verify-change` quedan fuera de la medición. Suman 33 pruebas nuevas (27 casos sin ventana y 6 con la ventana real), el paquete publicado y probado, y una pausa para una decisión de David: correr la 3.4 y borrar después la carpeta de demostración que creaba. La IA había estimado 1,5 a 2,5 h de reloj, y la medición cayó por debajo de esa estimación, en el piso de la S que declararon los dos. De las fricciones que la talla preveía:
- la ventana real no costó: el E2E pasó a la primera;
- la publicación autocontenida tampoco: el zip salió en la primera corrida del script;
- el workflow sigue sin correr hasta el primer tag.

Aparecieron tres del entorno de la sesión: Git Bash no arrancaba (el escaneo de secretos se trasladó a PowerShell y la copia limpia se armó con `git archive`), una DLL nativa de `Microsoft.Data.SqlClient` no carga en una ruta de 254 caracteres, y una primera corrida de gitleaks escaneó a medias con exit 0. Como en los changes anteriores, la S de David cubre además el tiempo humano, que no se midió: el gate, el video, el tag y la publicación del borrador.

### Regla de recorte

1. Al cerrar M0 y cada hito, se compara la estimación restante de lo must del hito siguiente con la dedicación disponible hasta su fecha, que se registra en ese momento.
2. Si no alcanza, se difiere la should HU01.2-J.
3. Si M0 no cierra el 06/10, cada día de atraso se descuenta de M1 y se aplica el punto 2 antes de iniciar U02. (Histórico: M0 no se ejecutó; D-C24 lo reemplazó por los changes de OpenSpec.)
4. Si aún no alcanza, se aplica D-C24: el alcance ya está recortado a las cinco must del corte, se conserva el orden de dependencias, se entrega lo terminado con sus AT ejecutados y lo pendiente se declara no terminado. Un AT diseñado nunca se presenta como ejecutado.
5. Cada aplicación de la regla se registra en [decisiones](decisiones.md) con fecha, unidades afectadas y AT diferidos.

## Contratos complementarios y preparación técnica

Los siguientes contratos forman parte del diseño de implementación. No son operaciones adicionales dentro de la selección académica de tres endpoints. Para cada uno se produce en M0 especificación, fixture y criterios de prueba antes de construir su unidad.

| Contrato / decisión técnica | Responsable | Debe quedar definido antes de | Contenido de cierre |
|---|---|---|---|
| Identidad local y adaptadores | TK01/TK02 | U03 | Sesión, 1:N/1:1, rechazo, cancelación, expiración y liberación; templates sintéticos y serial parser |
| Provisión inicial y snapshot | TK03/TK01 | U02/U05 | Ámbito, firma/integridad del paquete local, referencias, instalación atómica y recuperación; configuración publicada válida no se inventa localmente |
| Credenciales y permisos locales | TK01/TK03 | U03/U04 | Modelo y transporte definidos en la [SDD §7.4](especificacion-sdd.md#74-credenciales-y-control-de-acceso-local) y ADR12. M0 fija normalización, ámbito e historial de titularidad no reasignable, provisión de equipo nuevo/restaurado, fixture y pruebas de abuso/cierre de sesión |
| Feed y estado de comunicación | TK01/TK03 | U07 | SiteId/ámbito de cupo, origen, secuencia/cursor, señal de vida, huecos, umbrales, corte de recuperación y eventos incompatibles |
| Ciclo de intención e impresión | TK01/TK03 | U02/U04 | PENDING/abandono, estado durable previo al envío, caída con resultado incierto y recuperación sin reimpresión automática dudosa |
| Backup y restauración | TK03 | U02 y aceptación de recuperación | Copia consistente, nueva generación de auditoría, conciliación de hechos posteriores al backup e IDs sin colisiones; pérdida de disco separada |
| Auditoría y archivos | TK03 | U06 | Retención, capacidad, pérdida/truncado de archivo exportado, reparación y recuperación por IDs; permisos de lectura/exportación |
| Reloj y fecha de negocio | TK01/TK03 | U02 | D-C17: continuar hora Windows; detector, umbral y contexto de observación, reinicio e incertidumbre, AT48/49 |
| Versiones y almacenamiento | TK03 | U02 | Microsoft.Data.Sqlite, motor efectivo, transacciones y claves externas; SQL Server receptor, migraciones SQL y consultas parametrizadas |
| Nomenclatura e identidad del ecosistema | TK03/TK01 | U02/U05 | [Mapa adoptado](nomenclatura-integracion.md), campos que hoy sólo figuran en el diccionario, tipos físicos, FK agrupadas y serialización; RNF15/AT51. Formato definitivo de origen y nombres de proyectos en M0 |

Mantenimiento individual local se incorpora al contrato de credenciales y permisos, sin crear un cuarto endpoint académico. U01/TK01/TK03 definen provisión, vínculo de actor, protección y recuperación; U08/TK02 integra acceso y sesión. Los [pendientes de M0](administracion-y-bitacoras.md#mantenimiento-alternativo-y-pendientes-de-m0) y la designación del custodio deben cerrarse antes de implementar; no están resueltos por la sola aprobación de usuario/contraseña.

## Matriz de aceptación

Cada fila asigna un AT definido en la [SDD](especificacion-sdd.md) a una historia y a un único responsable principal: el ticket de tarea cuando existe (TK01.1, TK02.1 o TK03.1) o la épica técnica. Apoyo identifica colaboración, sin duplicar el trabajo; las dependencias de integración sí condicionan el cierre integral.

Con D-C24, el hito de una fila rige sólo si su historia o su escenario están en el corte (tabla de prioridad). Los AT de la segunda ola, entre ellos AT07, AT09, AT12, AT19–AT24, AT30–AT34, AT37–AT39, AT41–AT46, AT48, AT49, AT55 y AT56, conservan su diseño y no se ejecutan en M1 ni en M2.

| AT | Historia | Ticket principal | Apoyo | Hito / prioridad |
|---|---|---|---|---|
| AT01 | HU01.1 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT02 | HU01.3 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT03 | HU01.3 | TK03.1 | TK01 | M1; cierre integral M2 |
| AT04 | HU01.3 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT05 | HU02.1 | TK03.1 | TK01/TK02 | M1; cierre integral M2 |
| AT06 | HU03.1 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT07 | HU01.2 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT08 | HU01.2 | TK02 | TK01/TK03 | M1; cierre integral M2 |
| AT09 | HU02.3 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT10 | HU02.3 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT11 | Producto futuro | Por especificar | — | Fuera del MVP |
| AT12 | HU02.2 | TK01.1 | TK03 | M1; cierre integral M2 |
| AT13 | HU02.2 | TK01.1 | TK03 | M1; cierre integral M2 |
| AT14 | Producto futuro | Por especificar | — | Fuera del MVP |
| AT15 | Producto futuro | Por especificar | — | Fuera del MVP |
| AT16 | HU01.1 | TK02.1 | TK01/TK03 | M1; cierre integral M2 |
| AT17 | HU01.3 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT18 | Producto futuro | Por especificar | — | Fuera del MVP |
| AT19 | HU01.2 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT20 | HU01.2 | TK02 | TK01/TK03 | M1; cierre integral M2 |
| AT21 | HU01.2 | TK02 | TK01/TK03 | M1; cierre integral M2 |
| AT22 | HU01.2 | TK02 | TK01/TK03 | M1; cierre integral M2 |
| AT23 | HU01.2 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT24 | HU03.2 | TK02 | TK01/TK03 | M2; cierre integral M2 |
| AT25 | HU02.1 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT26 | HU01.1 | TK02.1 | TK01/TK03 | M1; cierre integral M2 |
| AT27 | HU02.2 | TK01.1 | TK02/TK03 | M1; cierre integral M2 |
| AT28 | HU01.3 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT29 | HU01.3 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT30 | HU02.5, HU02.6 | TK01 | TK02/TK03 | Segunda ola (D-C24): ambas políticas y su cambio por snapshot |
| AT31 | HU02.4 | TK01 | TK02/TK03 | M2; cierre integral M2 |
| AT32 | HU02.4 | TK01 | TK02/TK03 | M2; cierre integral M2 |
| AT33 | HU02.4 | TK03 | TK01/TK02 | M2; cierre integral M2 |
| AT34 | HU02.4 | TK03 | TK01/TK02 | M2; cierre integral M2 |
| AT35 | HU02.1 | TK03.1 | TK01/TK02 | M1; cierre integral M2 |
| AT36 | HU01.3 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT37 | HU03.3 | TK02 | TK01/TK03 | M2; cierre integral M2 |
| AT38 | HU03.4 | TK02 | TK01/TK03 | Segunda ola (D-C24) |
| AT39 | HU03.4 | TK02 | TK01/TK03 | Segunda ola (D-C24) |
| AT40 | Producto futuro | Por especificar | — | Fuera del MVP |
| AT41 | HU03.5 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT42 | HU03.5 | TK03 | TK01/TK02 | M2; cierre integral M2 |
| AT43 | HU03.5 | TK03 | TK01/TK02 | M2; cierre integral M2 |
| AT44 | HU03.5 | TK03 | TK01/TK02 | M2; cierre integral M2 |
| AT45 | HU03.2 | TK02 | TK01/TK03 | M2; cierre integral M2 |
| AT46 | HU01.2 | TK01 | TK02/TK03 | M2; cierre integral M2 |
| AT47 | HU01.1 | TK02.1 | TK01/TK03 | M1; cierre integral M2 |
| AT48 | HU01.3 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT49 | HU01.3 | TK03 | TK01/TK02 | M1; cierre integral M2 |
| AT50 | HU02.2 | TK01.1 | TK03 | M1; cierre integral M2 |
| AT51 | HU01, HU02 (transversal) | TK03 | TK01 | M0: diccionario y casos; M1: persistencia, adaptación y serialización; cierre integral M2 |
| AT52 | HU01.2 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT53 | HU01.2 | TK01 | TK03 | M1; cierre integral M2 |
| AT54 | HU03.1 | TK01 | TK02/TK03 | M1; cierre integral M2 |
| AT55 | HU02.3 | TK03 | TK01 | M1; cierre integral M2 |
| AT56 | HU03.3 | TK02 | TK01/TK03 | M0: contrato y fixture; M2: mantenimiento alternativo ejecutado |

## Evidencia de terminado

Cada ejecución de producto registra versión, ambiente, datos, comando, resultado y artefacto observable. Las pruebas usan SQLite y filesystem reales para durabilidad; los adaptadores biométricos y de impresión se identifican como simulados. La integración física registra evidencia y homologación por separado. El cierre de M2 exige las cinco must de D-C24 con sus AT, la suite de pruebas y una demostración verificable del flujo completo.

## Cobertura de requisitos por aceptación

La columna AT vincula requisitos con escenarios definidos. Las extensiones de producto sin escenario detallado se especifican antes de incorporarlas al MVP. La clasificación de alcance prevalece sobre la existencia de un ejemplo de aceptación.

| Requisito | AT asociados | Alcance |
|---|---|---|
| RF01 | AT01, AT52 | Núcleo |
| RF02 | AT08, AT19, AT20, AT21 | Contrato; doble de prueba en MVP |
| RF03 | AT01, AT17, AT29, AT35, AT36, AT52, AT53 | Núcleo |
| RF04 | AT16, AT47 | Núcleo |
| RF05 | AT16, AT26 | Núcleo |
| RF06 | AT02, AT03, AT17, AT30 | Núcleo |
| RF07 | AT04, AT29, AT48, AT49 | Núcleo |
| RF08 | AT02 | Núcleo |
| RF09 | AT01, AT47 | Núcleo |
| RF10 | AT06, AT35 | Núcleo |
| RF11 | AT03, AT05, AT25, AT26, AT35, AT53 | Núcleo |
| RF12 | AT06 | Núcleo |
| RF13 | AT06, AT54 | Núcleo |
| RF14 | AT15, AT40 | Producto futuro; detalle único en RF42 |
| RF15 | AT11, AT18 | Producto |
| RF16 | AT09 (publicación de fixtures; administración completa posterior) | Producto; fixtures publicados en MVP |
| RF17 | AT45 | Núcleo operativo; reportes completos, Producto |
| RF18 | AT12 | Producto; conflictos básicos de sincronización en Núcleo |
| RF19 | AT11 | Producto |
| RF20 | AT09, AT30, AT36, AT53, AT55 | Núcleo |
| RF21 | AT01, AT10, AT28, AT30 | Núcleo |
| RF22 | AT05, AT10, AT27, AT34 | Núcleo |
| RF23 | AT07 | Contrato/parser con datos sintéticos |
| RF24 | AT30, AT45 | Núcleo |
| RF25 | AT13 | Núcleo |
| RF26 | AT13, AT24, AT45, AT48, AT49 | Núcleo |
| RF27 | AT14 | Producto |
| RF28 | AT46 | Núcleo |
| RF29 | Ampliación futura: aceptación por especificar | Producto |
| RF30 | AT12, AT27, AT30, AT50 | Núcleo |
| RF31 | AT09, AT55 | Contrato; fixtures en MVP |
| RF32 | Ampliación futura: aceptación por especificar | Producto |
| RF33 | AT19, AT22 | Contrato biométrico |
| RF34 | AT19, AT20, AT21, AT22 | Contrato biométrico |
| RF35 | AT23 | Contrato biométrico |
| RF36 | AT08, AT24 | Núcleo y contrato biométrico |
| RF37 | AT30, AT31, AT33, AT34, AT50 | Núcleo de sincronización |
| RF38 | AT30, AT32 | Núcleo de auditoría |
| RF39 | AT30, AT31, AT32 | Núcleo de sincronización |
| RF40 | AT37, AT54, AT56 | Núcleo F04 |
| RF41 | AT38, AT39, AT56 | Núcleo F04 con adaptadores; homologación física, Producto |
| RF42 | AT15, AT40 | Producto futuro; fuera del MVP |
| RF43 | AT38, AT41, AT44, AT48, AT49 | Núcleo transversal |
| RF44 | AT41, AT44, AT48, AT49 | Núcleo transversal |
| RF45 | AT42, AT44 | Núcleo transversal |
| RF46 | AT42, AT43, AT44 | Núcleo transversal |

| Regla | AT asociados |
|---|---|
| RB01 | AT07, AT52 |
| RB02 | AT01, AT09, AT17, AT29, AT36, AT52, AT53 |
| RB03 | AT02, AT03, AT30, AT31 |
| RB04 | AT04, AT29, AT48, AT49, AT50 |
| RB05 | AT01, AT26, AT45 |
| RB06 | AT01, AT03, AT35, AT44 |
| RB07 | AT03, AT05, AT25, AT26, AT30, AT35, AT40, AT53 |
| RB08 | AT06, AT45, AT54 |
| RB09 | AT11, AT18 |
| RB10 | AT47 |
| RB11 | AT15 |
| RB12 | AT11 |
| RB13 | AT19, AT20, AT22 |
| RB14 | AT19, AT21, AT35 |
| RB15 | AT09, AT10, AT23, AT28, AT29, AT30, AT55 |
| RB16 | AT12, AT27, AT30, AT45, AT50 |
| RB17 | AT05, AT10, AT35 |
| RB18 | AT30, AT31, AT33, AT34, AT50 |
| RB19 | AT31, AT33 |
| RB20 | AT30, AT32 |
| RB21 | AT37, AT39, AT54, AT56 |
| RB22 | AT15, AT40 |
| RB23 | AT41, AT43, AT44 |
| RB24 | AT41, AT42, AT43, AT44, AT48, AT49 |
| RB25 | AT52, AT53, AT55 |
| RB26 | AT54, AT55, AT56 |

## Trazabilidad de coherencia

RNF15 → AT51 → HU01/HU02 → TK03 (principal) y TK01 (adaptador/DTO). M0 completa el diccionario físico y diseña los casos; M1 implementa y ejecuta las pruebas de persistencia e intercambio, con cierre integral en M2. La convención está adoptada; la implementación y su verificación de ejecución siguen pendientes.
