# Experiencia táctil — bocetos

Diseño preliminar con datos sintéticos. Son wireframes, no capturas de software.
En el perfil autónomo de un puesto, la conectividad se informa como estado de
sincronización, no como requisito de uso. El modo multipuesto auditado requiere
información en línea y propagación; D-A02 permite configurar continuidad local
(valor inicial) o suspensión de nuevas confirmaciones. La UI informa la política
activa, pero sólo la configuración administrada de la instalación puede cambiarla.

## Reposo e identificación

```text
INComedor                       Puesto 01 · Configuración v7

                    Identificate para comenzar
                 [ Presentar credencial o huella ]

Ayuda                            Sincronización: pendiente
```

Serie, 1:N y código+1:1 son entradas diferenciadas. En 1:1 el código sólo selecciona
al candidato: luego se requiere su huella. Identidad desconocida o no coincidente
no abre selección. Nunca se muestra la persona de la sesión anterior.

## Selección y confirmación

```text
Hola, Persona de prueba 01                  [ Cancelar ]

Elegí tu servicio de hoy
[ Almuerzo común ]  [ Almuerzo opcional ]  [ Refrigerio ]
[ Opción 4        ] [ Opción 5         ]   [ Opción 6  ]
                          [ Anterior ] [ Siguiente ]

Confirmar: Almuerzo común
Cargo informado: dato sintético de la configuración vigente
[ Volver ]                              [ Confirmar ]
```

Catálogo extensible, con navegación para más de diez opciones. Las variantes
pueden compartir cupo. El dominio local revalida vigencia y cupo al confirmar.
La suspensión multipuesto deshabilita nuevas confirmaciones, y el dominio
revalida la política y el estado al confirmar para cubrir pérdidas posteriores
a la selección. El botón queda ocupado mientras se resuelve la intención. El éxito procede del
commit SQLite, aunque INWeb no responda. Al terminar se limpia la sesión.

## Estados y mensajes propuestos

| Caso | Respuesta |
|---|---|
| Éxito local | «Consumo registrado. Retirá tu comprobante». No espera al servidor. |
| Cupo utilizado | «Ya registraste este servicio. Consultá al responsable del comedor». Sin otro original. |
| Persona inactiva (PERSON_INACTIVE) | «Tu habilitación no está activa. Consultá al responsable». No se confunde con cupo agotado. |
| Credencial revocada (CREDENTIAL_REVOKED) | «Esta credencial ya no está habilitada. Solicitá asistencia». Sin exponer su valor. |
| Credencial vencida (CREDENTIAL_EXPIRED) | «Esta credencial está vencida. Solicitá asistencia». |
| Regla explícita vigente Limit=0 (RULE_DENIED) | «Este servicio no está permitido por la regla vigente». Conserva ese motivo de auditoría. |
| Sin regla aplicable habilitante (SERVICE_NOT_ENABLED) | «No hay una habilitación aplicable para este servicio». Motivo distinto de regla cero. |
| Catálogo cambió antes del commit | Informa el cambio y solicita reconfirmación; conserva RB10/HU01-D. |
| Huella no coincidente | «No pudimos verificarte. Volvé a apoyar el dedo o solicitá ayuda». |
| Falta configuración inicial | «Puesto pendiente de configuración». No fabrica permisos. |
| Sin conexión en puesto único | Indicador «Sincronización pendiente». Continúa la operación local. |
| Multipuesto, CONTINUE_LOCAL sin comunicación | «Operando con datos locales. Sincronización pendiente». Continúa según cupos conocidos y registra incidencia. |
| Multipuesto, SUSPEND_NEW_CONSUMPTIONS sin comunicación | «Nuevos consumos temporalmente suspendidos. Recuperando comunicación». Sin confirmación nueva; conserva diagnóstico y comprobantes anteriores. |
| Red volvió, feed todavía incompleto | «Sincronizando consumos de otros puestos». El perfil suspendido aún no reanuda; continuidad local sigue atendiendo. |
| Salto del reloj detectado | Aviso en supervisión: «Cambio de hora detectado; revisar fecha de los consumos». La atención usa hora de Windows y conserva la auditoría; siguen vigentes reglas, persistencia y política de comunicación. |
| Sin papel | «Tu consumo está registrado. El comprobante queda pendiente». Conserva referencia. |
| Reinicio tras commit | Recupera intención/ticket. Si impresión incierta, operador decide copia marcada. |
| Lectura con sesión ocupada | No cambia la persona; señala ocupado. |
| Disco lleno/base dañada | Falla técnica explícita; no informa un registro que no pudo persistir. |

## Supervisión y accesibilidad

Panel autorizado: configuración aplicada, última sincronización, pendientes,
acuses, periféricos, política activa, incidencia y estado separado de envío/feed,
estado del reloj y búsqueda de comprobante. Reimprimir exige permiso y motivo,
mantiene TicketId y registra número de copia. No expone biometría ni secretos.

Se ensayarán controles amplios, contraste, teclado, foco y escalas 100/125/150 %.
Timeout previo a confirmar propuesto: 30 s, con aviso y opción de ampliar. No cancela
un consumo registrado. Son criterios para probar, no mediciones realizadas.

## Administración del Sistema — diseño local

```text
Administración · Sesión autorizada                       [ Cerrar ]
[ Equipo ] [ Diagnóstico ] [ Versiones ] [ Bitácoras ]
Configuración efectiva: revisión 4 · Reglas INWeb: versión 7
[ Editar parámetros del equipo ] [ Validar ] [ Aplicar revisión ]
Diagnóstico: [ API ] [ Táctil ] [ BioMini ] [ Serie ] [ Impresora ]
             [ SQLite ] [ Red ] [ Inventario de versiones ]
Adaptador: SIMULADO · Destino: Puesto de prueba
Comando completo y parámetros: <según adaptador de impresora>
[ Ejecutar prueba autorizada ] [ Cancelar ]
Resultado · Duración · Código de error · DiagnosticRunId
Bitácora general: OK · Interacciones: DEGRADADA · Pendientes: 12
```

La sesión privilegiada se abre con código y verificación 1:1 o con usuario y contraseña individual local de mantenimiento, aun sin lector ni red. El rol vigente del snapshot decide cada acción (RB26); administrador no hereda OPERATOR. La sesión no habilita consumo. Intentos limitados, mensajes sin secretos y cierre/expiración se concretan en M0; ver [mantenimiento](administracion-y-bitacoras.md#mantenimiento-alternativo-y-pendientes-de-m0).
Los permisos se verifican en el caso de uso, no sólo ocultando botones. Cambios de
equipo no editan cupos/precios/políticas maestras. Se informa si una prueba exige
mantenimiento y se espera a que no haya sesión activa; las pruebas de impresora
se identifican como PRUEBA. El visor serie muestra eventos con tiempos y campos
técnicos, con identidades enmascaradas. Cierre/timeout libera dispositivos.

Ante fallo de archivos: «Bitácora pendiente de recuperación; respaldo local activo»
en supervisión; el comensal puede seguir si SQLite persiste. Se muestran ruta/mes,
primera falla, pendientes y recuperación por archivo. Ante fallo también de SQLite,
no se muestra éxito de consumo nuevo. El visor presenta tiempos originales y de
exportación, correlación y estado desconocido cuando corresponda.

## Emisión para terceros — pantalla futura, fuera del MVP

```text
Emitir para tercero · Operador identificado
Tipo: [ Interno | Externo ]  Beneficiario: [ Buscar identidad ]
Servicio: [ Habilitados para el beneficiario ]  Cantidad: [ 1 ]
Observaciones: [ Obligatorias                            ]
Resumen: quién opera · para quién · qué · cargo · cupo disponible
[ Cancelar ]                       [ Confirmar para beneficiario ]
```

Sin ingreso por nombre libre que otorgue derechos, sin botón de ignorar cupo.
Un rechazo identifica la causa sin convertir el privilegio del operador en permiso
de consumir. Registro y bitácora conservan actor y beneficiario por separado.
