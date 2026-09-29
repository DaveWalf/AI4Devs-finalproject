# Nomenclatura e integración con INWeb

Estado: convención de diseño adoptada para la implementación del MVP (D-C18; ADR11). Los nombres de esta sección expresan diseño; no acreditan tablas, migraciones o clases ya implementadas.

## Criterio de pertenencia al ecosistema

INComedor mantendrá el vocabulario de negocio y las convenciones de datos del ecosistema INWeb, conservando la separación de responsabilidades de su arquitectura. La correspondencia entre modelo lógico, almacenamiento, C# y contrato de intercambio será explícita y versionada.

La convención observada en el sistema anfitrión usa tablas como `tbPersonas`, columnas como `perCodigo` y objetos de negocio como `Persona.CodigoPersona`. El diseño del módulo nuevo adopta ese criterio de separación. Los nombres `tbComedor...` y sus abreviaturas son nuevos; no afirman que esas tablas ya existan en INWeb.

## Convención adoptada

| Superficie | Regla |
|---|---|
| Tablas nuevas | `tbComedor` más sustantivo español en PascalCase. El módulo diferencia sus tablas de los maestros existentes. |
| Columnas propias | Abreviatura mnemónica registrada por entidad más nombre descriptivo: `comconFechaNegocio`, `commenNombre`. Sin tildes, espacios ni abreviaturas improvisadas. |
| Identidad y referencias | Nombre estable del concepto referido: `comperCodigo`, `comicCodigo`, `comconCodigo`. Un rol distinto se expresa en el nombre: `compueCodigoOrigen`. La palabra Codigo no determina el tipo. |
| Claves y restricciones | `PK_<tabla>`, `FK_<hija>_<padre>_<rol>`, `IX_<tabla>_<camposOProposito>` y `DF_<tabla>_<campo>`; `UQ_` y `CK_` se reservan como convención del módulo. SQLite y SQL Server aplicarán sólo objetos que soporten y sean necesarios. |
| C# de negocio | Sustantivos españoles, clases singulares y miembros PascalCase; sin `tb` ni prefijos de columna. Métodos expresivos, como `ConfirmarConsumoAsync`; parámetros camelCase y colecciones plurales. |
| Namespaces | Raíz `Intelektron.INComedor`; los proyectos previstos son los del [árbol único de la solución](../readme.md#23-descripción-de-alto-nivel-y-estructura-de-ficheros): `.Domain`, `.Application`, `.Contracts`, `.Infrastructure.Sqlite`, `.Kiosk`, `.Biometrics.Broker`, `.Devices`, `.Sync.Api` e `.Integration.INWeb`. |
| JSON y API | Nombres, rutas, valores de enums y versiones del contrato explícitos. Un renombre de propiedad C# no cambia su nombre serializado. Los DTO y el adaptador realizan la traducción. |
| Diagrama lógico | `L_`, `C_` y `B_` identifican el almacén local, central y biométrico. No son prefijos de tabla física. |

No se trasladan irregularidades heredadas de nombres, nulabilidad o tipos. Se conservan dinero exacto en unidades menores, hora UTC y fecha de negocio separadas, identidades distribuidas y restricciones de integridad del modelo.

## Identidades y significado

`perCodigo` identifica la persona de INWeb; `empCodigo` identifica al empleado. No son intercambiables. Documento, legajo y código capturado son datos con semántica propia. El resultado de un identificador debe conservar el tipo de persona y el ámbito de origen cuando corresponda.

`PersonId` sigue siendo la identidad UUID de INComedor, denominada `comperCodigo` en la base diseñada y `CodigoPersona` en `PersonaComedor`. Esa propiedad es de tipo Guid, distinta del entero `Persona.CodigoPersona` del sistema anfitrión. El adaptador usa referencias explícitas, por ejemplo `CodigoPersonaINWeb` y `CodigoEmpleadoINWeb`, para evitar conversiones por similitud de nombre.

El enlace existente `PersonExternalKey` conserva `SourceSystem` y `ExternalKey`: el origen debe distinguir instancia y tipo de entidad. Un formato posible, aún por fijar en el adaptador, es `INWEB:<instancia-logica>:PERSONA` con el código externo en texto. Los nombres de servidor, contraseñas, documentos y credenciales no se usan como origen. Un código numérico se valida al convertirlo al tipo requerido por INWeb; no se recodifica un UUID ni se supone una equivalencia con el legajo.

La misma referencia debe resolver a un UUID estable entre snapshots y puestos. No se genera una identidad de persona nueva en cada descarga. Se conservan los UUID de intención, consumo, evento y lote durante reintentos y sincronización. `IdentitySessionId` mantiene su tipo de token opaco; no se transforma en UUID por su nombre.

`OrganizationId` y `SiteId` tampoco pasan a ser automáticamente `orgCodigo` o un código de lector. `KioskId` identifica el puesto de INComedor; el adaptador debe declarar cualquier correspondencia con lectores o controladores. No hay claves foráneas físicas entre la SQLite del puesto y SQL Server.

La existencia de `tbJornadasComidas` en el sistema anfitrión no equivale a un catálogo de platos: representa franjas y duración de comida dentro de jornadas. `tbMarcaciones` tampoco reemplaza el consumo idempotente del producto. Sus integraciones requieren una traducción funcional explícita.

## Ejemplos entre capas

| Concepto/contrato | Nombre físico de diseño | Miembro C# de diseño | Tipo y significado conservados |
|---|---|---|---|
| `PersonId` | `tbComedorPersonas.comperCodigo` | `PersonaComedor.CodigoPersona` | UUID propio, no `perCodigo` central. |
| `ExternalKey` | `tbComedorClavesExternasPersonas.comcepClaveExterna` | `ClaveExternaPersona.ClaveExterna` | Texto acompañado de origen; no se pierde el espacio de identidad. |
| `IntentId` | `tbComedorIntencionesConsumo.comicCodigo` | `IntencionConsumo.CodigoIntencion` | UUID estable durante el reintento local. |
| `ConsumptionId` | `tbComedorConsumos.comconCodigo` | `Consumo.CodigoConsumo` | UUID del hecho confirmado. |
| `BusinessDate` | `tbComedorConsumos.comconFechaNegocio` | `Consumo.FechaNegocio` | Fecha de negocio; no es un instante UTC. |
| `LocalCommittedAtUtc` | `tbComedorConsumos.comconFechaConfirmacionLocalUtc` | `Consumo.FechaConfirmacionLocalUtc` | Instante observado y registrado por el puesto, en UTC. |
| `EventId` | `tbComedorEventosSalida.comevCodigo` | `EventoSalida.CodigoEvento` | UUID del evento; la bandeja central conserva esa identidad. |
| `ConfigurationVersion` | `tbComedorVersionesConfiguracion.comvcVersionConfiguracion` | `VersionConfiguracion.NumeroVersionConfiguracion` | Número de versión; no se confunde con el UUID del snapshot. |

## Correspondencia de entidades

Esta tabla cubre las 44 entidades del ER. La localización sigue siendo parte del contrato de persistencia; compartir una convención no fusiona almacenes ni autoriza escrituras directas sobre tablas existentes de INWeb.

| Entidad lógica | Almacén | Tabla de diseño | Prefijo propio | Clase de negocio de diseño |
|---|---|---|---|---|
| `L_KioskConfiguration` | local | `tbComedorConfiguracionPuestos` | `comcp` | `ConfiguracionPuesto` |
| `L_ConfigurationSnapshot` | local | `tbComedorVersionesConfiguracion` | `comvc` | `VersionConfiguracion` |
| `L_PersonReference` | local | `tbComedorPersonas` | `comper` | `PersonaComedor` |
| `L_PersonExternalKey` | local | `tbComedorClavesExternasPersonas` | `comcep` | `ClaveExternaPersona` |
| `L_PersonCredential` | local | `tbComedorCredencialesPersonas` | `comcre` | `CredencialPersona` |
| `L_LocalOperatorGrant` | local | `tbComedorPermisosOperadores` | `compop` | `PermisoOperador` |
| `L_PersonGroup` | local | `tbComedorGruposPersonas` | `comgrp` | `GrupoPersonas` |
| `L_GroupMembership` | local | `tbComedorGruposPersonas_Personas` | `comgpp` | `PertenenciaGrupo` |
| `L_ServiceType` | local | `tbComedorTiposServicio` | `comsrv` | `TipoServicio` |
| `L_QuotaScope` | local | `tbComedorAmbitosCupo` | `comac` | `AmbitoCupo` |
| `L_QuotaBucket` | local | `tbComedorCategoriasCupo` | `comcat` | `CategoriaCupo` |
| `L_MenuItemVersion` | local | `tbComedorOpcionesMenu` | `commen` | `OpcionMenu` |
| `L_MenuQuotaCharge` | local | `tbComedorOpcionesMenu_CategoriasCupo` | `commcc` | `CargoCupoMenu` |
| `L_EntitlementRule` | local | `tbComedorReglasHabilitacion` | `comreg` | `ReglaHabilitacion` |
| `L_IdentitySession` | local | `tbComedorSesionesIdentidad` | `comsid` | `SesionIdentidad` |
| `L_ConsumptionIntent` | local | `tbComedorIntencionesConsumo` | `comic` | `IntencionConsumo` |
| `L_ConnectivityIncident` | local | `tbComedorIncidenciasComunicacion` | `comicm` | `IncidenciaComunicacion` |
| `L_Consumption` | local | `tbComedorConsumos` | `comcon` | `Consumo` |
| `L_QuotaUsage` | local | `tbComedorUsosCupo` | `comuc` | `UsoCupo` |
| `L_ObservedConsumption` | local | `tbComedorConsumosConocidos` | `comcc` | `ConsumoConocido` |
| `L_ObservedQuotaCharge` | local | `tbComedorCargosCupoConocidos` | `comccc` | `CargoCupoConocido` |
| `L_PeerInbox` | local | `tbComedorRecepcionesPares` | `comrp` | `RecepcionPar` |
| `L_FeedCursor` | local | `tbComedorCursoresDistribucion` | `comcd` | `CursorDistribucion` |
| `L_Ticket` | local | `tbComedorComprobantes` | `comcmp` | `Comprobante` |
| `L_PrintJob` | local | `tbComedorTrabajosImpresion` | `comimp` | `TrabajoImpresion` |
| `L_OutboxEvent` | local | `tbComedorEventosSalida` | `comevs` | `EventoSalida` |
| `L_SyncBatch` | local | `tbComedorLotesSincronizacion` | `comlot` | `LoteSincronizacion` |
| `L_SyncBatchItem` | local | `tbComedorLotesSincronizacion_Eventos` | `comle` | `EventoLoteSincronizacion` |
| `L_AuditRecord` | local | `tbComedorBitacora` | `combit` | `RegistroBitacora` |
| `L_AuditFileDelivery` | local | `tbComedorEntregasBitacora` | `comeb` | `EntregaBitacora` |
| `L_LogDeliveryIncident` | local | `tbComedorIncidenciasBitacora` | `comib` | `IncidenciaBitacora` |
| `L_LocalSettingsRevision` | local | `tbComedorRevisionesConfiguracionLocal` | `comrcl` | `RevisionConfiguracionLocal` |
| `C_KioskRegistry` | central | `tbComedorPuestos` | `compue` | `PuestoComedor` |
| `C_ConfigurationPublication` | central | `tbComedorPublicacionesConfiguracion` | `compc` | `PublicacionConfiguracion` |
| `C_SyncBatchReceipt` | central | `tbComedorAcusesLotes` | `comacl` | `AcuseLote` |
| `C_InboxEvent` | central | `tbComedorEventosEntrada` | `comeve` | `EventoEntrada` |
| `C_ConsolidatedConsumption` | central | `tbComedorConsumosConsolidados` | `comcco` | `ConsumoConsolidado` |
| `C_ReconciliationCase` | central | `tbComedorCasosConciliacion` | `comcas` | `CasoConciliacion` |
| `C_CentralVendorCost` | central | `tbComedorCostosConcesionario` | `comcos` | `CostoConcesionario` |
| `C_ConsumptionFeed` | central | `tbComedorDistribucionConsumos` | `comdc` | `DistribucionConsumos` |
| `C_CentralConnectivityIncident` | central | `tbComedorIncidenciasComunicacionCentrales` | `comicmc` | `IncidenciaComunicacionCentral` |
| `C_QuotaAuditAlert` | central | `tbComedorAlertasCupo` | `comalc` | `AlertaCupo` |
| `C_AlertConsumption` | central | `tbComedorAlertasCupo_Consumos` | `comalco` | `ConsumoAlertaCupo` |
| `B_BiometricTemplateEnvelope` | biometria | `tbComedorPlantillasBiometricas` | `combio` | `PlantillaBiometricaProtegida` |

## Claves compuestas y alcance del mapa

El cambio de nombre conserva todas las claves compuestas y unicidades del modelo. Por ejemplo, una persona versionada se identifica por `(comvcCodigo, comperCodigo)`. Una pertenencia a grupo referencia a persona por ese par y a grupo por `(comvcCodigo, comgrpCodigo)`; no alcanza una FK que omita la versión de configuración. La incidencia central conserva `(compueCodigo, comicmCodigo)`.

Las abreviaturas de tabla no obligan a que toda PK use el prefijo propio: una tabla de relación o proyección conserva la identidad referida. `tbComedorConsumosConocidos` y `tbComedorConsumosConsolidados`, por ejemplo, conservan `comconCodigo`; no crean un consumo distinto por cambiar de almacén.

La matriz de campos derivada del ER contiene los atributos representados en ese diagrama, incluidos los que transporta el contrato de intercambio. El diccionario completo prevalece para nulabilidad, FK agrupadas, destinos, acciones y restricciones; los campos que todavía sólo figuran en él están [listados en el modelo](modelo-datos.md#diagrama-er-de-entidades-y-claves) y se mapean en M0.

### Prefijos de concepto sin tabla propia

Además de los 44 prefijos de entidad, tres prefijos nombran conceptos que varias tablas comparten y que no tienen una tabla con ese prefijo:

| Prefijo | Concepto | Columnas que lo usan |
|---|---|---|
| `comev` | Identidad de un evento de sincronización, la misma en la salida del puesto y en la entrada central | `comevCodigo` y `comevCodigoOrigen` |
| `comInstalacion` | Instalación (`SiteId`), ámbito de la política multipuesto | `comInstalacionCodigo` |
| `comOrganizacion` | Organización (`OrganizationId`) del registro de provisión | `comOrganizacionCodigo` |

Los demás usos de un prefijo fuera de su tabla son referencias a la entidad dueña del prefijo, como `compueCodigo` o `comdcCodigoFlujo`, y conservan su identidad. Este documento no es un script DDL ni una migración. Esos detalles y la representación física de UUID en SQLite se completan y prueban en M0.

## Verificación prevista

- Correspondencia unívoca de entidades, columnas y propiedades; ausencia de colisiones con tablas del sistema anfitrión y dentro de cada clase.
- Conservación de tipos lógicos, claves compuestas, unicidades, UUID de operación, ámbitos y reglas de negocio.
- Resolución estable de referencias INWeb, con casos que distingan persona/empleado e instancias con el mismo código numérico.
- Serialización de DTO que conserva nombres, valores, ejemplos y hashes del contrato versionado.
- Migraciones locales y mappings centrales revisados contra el diccionario y esta convención, sin modificar maestros existentes.

El trabajo pertenece a los tickets de datos e integración del MVP; no agrega otra funcionalidad ni cambia las cuatro comprometidas. Las condiciones de aceptación funcionales, la política de conectividad y la de reloj permanecen vigentes.

## Mapas verificables y trazabilidad

El [mapa de entidades](nomenclatura-entidades.csv) registra 44 correspondencias y sus prefijos. El [mapa de atributos del ER](nomenclatura-campos-er.csv) registra 256 correspondencias, tipos lógicos y marcas de clave. Sus encabezados identifican entidad lógica, tabla, campo lógico, columna, clase y propiedad; no renombrarán el JSON. Estos mapas complementan el [diccionario](modelo-datos.md); no lo sustituyen.

RNF15 y AT51 en la [SDD](especificacion-sdd.md) exigen esta correspondencia. TK03 es responsable de persistencia y TK01 del adaptador y DTO; el [plan académico](plan-academico.md) prepara los contratos en M0 y verifica la ejecución al implementar. `ActorId` guarda el `PersonId` del operador o administrador autenticado (RB26), que no se confunde con el comensal atendido.
