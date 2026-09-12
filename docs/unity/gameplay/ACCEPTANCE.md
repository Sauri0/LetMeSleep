# Riesgos y aceptación de Gameplay alfa

Los IDs siguientes describen pruebas futuras Unity, no comprobaciones ejecutadas. Cada recibo debe indicar commit, versión Unity, mapa/hash, perfil de balance, hardware/resolución cuando renderiza, población y transporte. Headless/EditMode valida reglas/consultas; no acredita aspecto, FPS ni WAN. Director coordina editor/GPU; nadie abre otra sesión por este documento.

## Matriz de riesgos

| Riesgo | Impacto | Prevención / observación concreta | Responsable |
|---|---|---|---|
| Marcas sobreviven como dependencia oculta | Picadura no cumple nueva regla | Cero AssignedZone/Marker/RotationSeconds en DTO/runtime nuevo; bot y humano completan contacto libre | W1 + UI |
| Desmayo/ayuda/balance heredados accidentalmente | Bloqueo prolongado o extracción trivial | Perfil único versionado, 12 s iniciales y playtest; gráficos de tiempo consciente/caído/ayudado | W1 + QA |
| Todo cuerpo picable pero espalda imposible solo | Defensa injusta | Certificado continuo de superficie elegible vs alcance de ambas manos y vista; repetir en pose/crouch/giro | M1 + W1 + W2 |
| Nueva escala no coincide con física | Atascos, túneles, golpe invisible | Medidas Director 1.72/.25 humano, .055 mosquito; misma escena de colliders/mediciones de rig | M1/M2 + W1 |
| Doble reloj Unity/netcode | Velocidad/daño depende de FPS | Core único Advance 30 Hz, contador por tick; pruebas 30/60/144 FPS, ráfagas de paquetes | Director + W1 |
| Snapshot supuestamente inmutable comparte arrays | UI observa estado parcial | Capturar snapshot, avanzar host y exigir que el objeto anterior sea idéntico | Director + W1 |
| Endpoint permite suplantar actor | Cliente aplica comandos ajenos | PUID de transporte → membresía Core → ActorId; rechazar ActorId ajeno incluso con secuencia válida | Director |
| Paquetes viejos reactivan acciones | Repite palmada/picadura al recuperar | Epoch/RoundId/Sequence/ViewRevision, ACK y limpieza de borde/held | Director + W1 |
| Animator/LOD mueve o borra hitbox | Inconsistencia al ocultarse o bajar FPS | Pose de combate independiente de render; reproducir strike/ancla con renderer apagado | W1 + W2 |
| SphereCast parte dentro de collider | Cámara o insecto atraviesa pared | Overlap/depenetración antes del barrido, límites de reintento y último punto válido | W1 |
| Normal del cast incorrecta | Posado salta/gira en cantos | Normal real corroborada y marco tangente transportado; pruebas de esquinas | W1 + M2 |
| Door/collider pierde identidad tras carga | Anclas inválidas o daño ignorado | IDs estables y revision de mapa/soporte, release seguro y cota de separación | Director + W1/M2 |
| Bot lee enemigos detrás de pared | Entrenamiento engañoso | BotObservation filtrado, memoria caduca y logs de adquisición/percepción | W1 + QA |
| Ronda entra en resultado durante benchmark | FPS artificialmente alto | Comprobar phase/actorcount/HUD/maphash por muestra; no extrapolar GPU local a 1660 Ti | QA + Director |

## Pruebas mínimas implementables

| ID | Montaje | Aserciones |
|---|---|---|
| G01 Input | NaN, Inf, diagonal >1, paquete duplicado/ajeno/viejo, timeout | Rechazo/normalización apropiados; no cambia sangre/pose ajena; no acción duplicada; input expira |
| G02 Humano | Corredor, vano 1.1×2.2, ambas escaleras, salto bajo techo, crouch | Movimiento continuo y cabeza libre, step ≤.22 m; no levantarse sin espacio; velocidades del perfil |
| G03 Cámara cuerpo | 720° yaw mirando pecho/piernas, giro caminando y quieto, 30/144 FPS | Yaw nunca bloquea; cuerpo/manos visibles, sin flip 180° bajo -90° ni brazo invertido; rayo coincide con mira |
| G04 Vuelo | W a yaw/pitch variados y polos; A/D+vertical; soltar movimiento | Vector W ≤1° del aim, velocidad diagonal acotada, freno dentro de fórmula +1 tick, no deriva |
| G05 Cámara insecto | Inicio junto a pared, techo bajo, zoom, giro rápido, near-plane | Sin clipping/traspaso, retracción inmediata y expansión suave; cámara no cambia contacto host |
| G06 Posado | Piso→pared→techo y viceversa; cantos cóncavos/convexos, puerta giratoria | IDs/marcos continuos o salida explícita, W no se invierte, sin recuperar input de marco viejo |
| G07 Patio | Exterior a altura mayor al edificio; vuelo sobre tejado/descenso | Techo físico sólo casa, cielo no posable; suelo/cerco reales y acceso interior-exterior para ambos roles |
| G08 Picadura | Contacto libre en superficie válida, sin marker object ni assignment | Preparación/adhesión por proximidad; sin salto remoto; otro lado del cuerpo/puerta/mesa bloquean |
| G09 Cancelación | Soltar, desprender, golpear, mover víctima, despawn, cambio de ronda | Una transición y motivo; no extracción sin ancla, no sangre duplicada, punto seguro, requiere rearmar held |
| G10 Alcance solo | Muestreo de superficies elegibles y barrido de dominio de cada pose | Todo contacto habilitado tiene defensa manual alcanzable; no aprobar sólo por centroides o ocho puntos |
| G11 Cooperación | Dos humanos, espalda defendida por compañero, luego desconexión | Golpe manual quita insecto; al quedar uno no persiste contacto indefendible |
| G12 Strike | Mano izquierda/derecha/herramienta, insecto rápido, pared anterior | Contacto sólo en ventana barrida y alcance real; fracción más próxima gana, una vez por StrikeId |
| G13 Extracción | Uno y varios insectos sobre misma víctima, cuota/tiempo mismo tick | Trabajo acreditado = sangre, tope por víctima, progreso individual coherente; desmayo único; prioridad documentada |
| G14 Caída/ayuda | Impacto alto, aterrizaje suelo/mueble, rescate cortado/múltiple | Sólo Sangre recuperable; reloj 12 s desde apoyo, ayuda acotada, no reinicio por golpe repetido ni aparición dentro de obstáculo |
| G15 Desmayo | Extraer hasta completar; intentar repicar; techo al incorporarse | Caída visible+host, contactos se sueltan, protección y recuperación según perfil, sin cadena infinita |
| G16 Bots | Ambos roles, casa/patio/dos pisos, puertas cerradas y aliado caído | Mismos comandos y permisos, acciones reales, percepción sin omnisciencia; ronda termina por reglas |
| G17 Reinicio | Varias rondas/menú/rol alternado/desconexión host | MapId fijo, RoundId cambia, contadores/anclas/eventos viejos limpios; Core da resultado/cierre coherente |
| G18 Snapshot | Captura A, Advance varias veces, captura B, reordenar/redelivery | A permanece idéntico, B refleja host, eventos deduplicados, remotos no interpolan entre rondas |
| G19 Latencia | Dos clientes con delay/pérdida/reordenación y diferentes FPS | Daño host, vista local fluida, reconciliación no atraviesa pared; registrar distribución de errores/ticks |

G10 requiere pruebas geométricas y visuales de cuerpo real. G16 exige observaciones de acciones y resultados, no sólo «hay bots en roster». G19 sintético no sustituye dos redes independientes. Cada fallo se asigna al propietario del área; no subir umbrales para ocultar falta de contacto/alcance.

## Handoff de integración

1. Director incorpora DTO/Core o confirma nombres equivalentes, reloj 30 Hz, identidad PUID y lifecycle de GameSessionState inmutable. Worker 1 adapta interfaces, no crea Core paralelo.
2. M1/W2 entregan pivots de ojos/manos/agarre/probóscide, superficies anatómicas estables y contrato de pose; M2 mapa/colliders/IDs/flags. W1 valida dimensiones contra G02/G08/G10.
3. Lote siguiente asignado por Director: motores + cámara + estados/queries y pruebas EditMode/PlayMode. No implementar scripts/escenas con sólo este documento como propiedad ampliada.
4. Después, Sangre/contactos/defensa/desmayo/bots e integración Online/UI, con eventos y recibos por commit. Mantener fuera beta/omega salvo nuevo lote explícito.

Supuestos elegidos sin pedir más decisiones al usuario: perfil `alfa-blood-initial-1`, recuperación ligada al aterrizaje, sin acumulación de desmayo entre picaduras separadas, tope de extracción por víctima, protección corta y herramienta muestra matamoscas. Son editables por Director y pruebas reales; ninguno justifica detenerse esperando aprobación artística mientras Branko duerme.
