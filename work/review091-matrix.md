# Matriz de revisión funcional 0.9.1 y alcance 0.9.2

Base de la preparación: `59af8ad`; candidato integrado leído: `7a40f9e`, rama
`codex/091-qa`. Este documento separa tests preparados de resultados ejecutados.
Todavía no se ejecutó Godot, Blender, import, captura ni benchmark porque el
turno de motor sigue reservado para otro integrante.

## Dictamen estático inicial

- La cobertura procedural existente valida estructura y determinismo, pero no
  acredita por sí sola el recorrido físico de casas generadas. `route_tests.gd`
  sigue físicamente rutas de la casa fija; `procedural09_world_checks.gd`
  consulta rutas de una sola semilla y no las camina.
- `procedural09_corpus_checks.gd` calcula una `layout_signature` que excluye
  semilla, nombres, colores y mobiliario, pero detecta duplicados con
  `fingerprint`. `HouseValidation.fingerprint()` incluye `seed`; por lo tanto,
  esa comprobación puede pasar aunque dos semillas produzcan la misma planta.
  `review091_house_contract.gd` comprueba directamente la firma estructural.
- El diff de superficies `bdeb726..59af8ad` conserva colisiones, huecos de
  puertas y transformaciones; amplía la sustracción a caras de pared enterradas
  o duplicadas y parte el acabado del suelo a la altura exacta de la losa. No
  encontré una regresión funcional demostrable sólo por lectura. Los resultados
  707/0, 3 mapas/0, 992/0 y 603/0 pertenecen al baseline sellado por el Director;
  todavía no son una ejecución independiente de QA ni sustituyen revisión en
  movimiento.
- El ID generado cambia a `house-v2-<seed>` y v1 deja de ser válido. Los fixtures
  no asociados a versión/red obtienen ahora IDs válidos mediante
  `Generator.map_id(seed)`. Los casos negativos conservan v1, v3, cero, ceros a
  la izquierda, prefijos incorrectos y límites fuera de rango. El candidato
  integrado ya contiene v2; falta ejecutar los gates sobre esa geometría.

## Corte entre entregas

- Para 0.9.1 bloquean publicación las fallas de circulación física, puertas,
  escaleras, rutas, movimiento/cámara, defensa, superficies o navegación de la
  personalización que impidan completar una partida con ambos roles. Un test
  indirecto o un resultado heredado no aprueba esos puntos.
- Para 0.9.2 esta revisión debe volver a comprobar las rutas sobre la zonificación
  y el amueblado final, las siluetas y expresiones de ambas especies, las
  interpenetraciones, los materiales/luces finales y el rendimiento comparable.
  Cualquier defecto injugable descubierto en ese trabajo bloquea primero 0.9.1.
- WAN real entre casas continúa pendiente por decisión de Branko. ENet loopback
  sólo acredita codec, autoridad y ciclo de partida local.

## Criterios y evidencia prevista

| Área | Criterio de aceptación | Gate independiente | Estado |
|---|---|---|---|
| Identidad procedural | Sólo v2; v1 y v3 rechazados; fingerprint determinista | `review091_house_contract.gd`, `procedural09_test.gd`, `map_tasks09_test.gd` | Preparado; v2 integrada; sin ejecutar |
| Variedad | Las 11 semillas acordadas tienen firmas de estructura distintas, excluyendo seed/cosméticos/muebles | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Pasillos | Ancho libre físico ≥ 1,50 m en colisión real | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Descansos | Ancho libre físico ≥ 1,50 m en ambos extremos de cada tramo | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Escaleras | Ancho libre físico ≥ 1,40 m; huella ≥ 0,28 m; contrahuella ≤ 0,22 m | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Puertas | Paso abierto real ≥ 1,30 m, superior al diámetro humano de 1,20 m | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Rutas humanas | Todos los spawns tienen rutas válidas a tareas/pickups; los destinos más lejanos de cada planta se completan con `Arena.step_human`, sin salto ni teleport | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Rutas mosquito | Todos los spawns tienen rutas válidas a todas las habitaciones y caben en la colisión con puertas abiertas | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Escaleras físicas | Cada tramo se sube y baja mediante autoridad real; desplazamiento vertical acotado a la contrahuella | `review091_house_contract.gd` | Preparado; sin ejecutar |
| Escala de 3 plantas | Máximo contractual 22 habitaciones, seis pasillos y cuatro tramos; luces ≤ 32 y todas las plantas iluminadas | Test del implementador + revisión World integrada | Metadatos acordados; gate visual/integración pendiente |
| Cámara/cuerpo | Dos giros completos mirando abajo, con marcha; sin pasos congelados ni contrarrotación; recenter en 1 s | `review091_motion_combat_contract.gd` a 30/60/120 Hz | Preparado; ejecución nativa pendiente porque el gate también instancia `ActorView` |
| Movimiento | Caminar, correr y agacharse activos durante giro; cuerpo siempre dentro de colisión generada | `review091_motion_combat_contract.gd` | Preparado; ejecución nativa pendiente |
| Entrada a adhesión | El salto total de raíz al entrar en `bitten` no supera un frame de sprint a 60 Hz + 5 mm (0,0883 m) | `review091_motion_combat_contract.gd` sobre `ActorView` real | Preparado para renderer nativo; el implementador midió 0,1423 m y Worker 1 prepara corrección |
| Pose/contacto | Mosquito adherido sigue exactamente la zona de la pose autoritativa; pose pública e hitbox coinciden | `review091_motion_combat_contract.gd` | Preparado; sin ejecutar |
| Defensa manual | Anticipación visible; un rayo manual produce exactamente una transición de impacto; secuencia duplicada no reinicia ataque | `review091_motion_combat_contract.gd` | Preparado; sin ejecutar |
| Default humano | Pijama clásico 0, pantuflas clásicas 0 y gorro de noche 3 | `review091_preview_views.gd` | Preparado; sin ejecutar |
| Catálogo | Cada opción de ambos roles llega a Preview, tiene mallas visibles finitas y conserva la firma de colliders | `review091_preview_views.gd` | Preparado; import/render pendiente |
| Ángulos | Cada opción se captura en frente, perfil y espalda; hash frontal distinto dentro de cada categoría | `review091_preview_views.gd` | Preparado; aprobación visual humana pendiente |
| Online integrado | Host/guest cruzan `OnlineTransport`, ACK del mismo mapa/fingerprint, movimiento, resultados, rematch y desconexión | `review091_online_cosmetics_contract.gd` | Preparado; ENet loopback pendiente |
| Cosméticos en red | Dos perfiles independientes sobreviven lobby → rol sorteado → resultados → rematch | `review091_online_cosmetics_contract.gd` | Preparado; sin ejecutar |
| EOS/WAN | No presentar fixture local como EOS, relay, NAT o WAN | Manifiesto del gate fija `eos_sdk=false`, `relay=false`, `wan=false` | WAN real pendiente de Branko |
| Superficies | Sin cortes/parpadeos desde cerca/lejos y en movimiento; puertas/marcos/suelos/techos estables | Baseline + recorrido/captura de integración | Baseline heredado; QA visual pendiente |
| Rendimiento | 1080p60 sostenido en GTX1660Ti, FPS ilimitados por defecto | `video_preferences07_test.gd` + benchmark final comparable | Default ya cubierto; hardware/candidato final pendientes |

## Archivos de QA preparados

- `game/tests/review091_house_contract.gd`: corpus corto determinista, medidas
  derivadas de colisión real, rutas estáticas y seguimiento físico.
- `game/tests/review091_motion_combat_contract.gd`: continuidad por frecuencia,
  pose compartida, mosquito adherido y ataque manual exacto.
- `game/tests/review091_preview_views.gd`: catálogo completo en tres vistas,
  mallas finitas, colliders invariantes y manifiesto de capturas.
- `game/tests/review091_online_cosmetics_contract.gd`: dos contextos reales de
  `SceneMultiplayer` y `OnlineTransport` sobre ENet loopback, con sesión EOS
  sustituida de forma explícita por fixture.

Fixtures anteriores migrados a `Generator.map_id()`: `door09_body_query_test`,
`door09_equivalence_test`, `house09_ceiling_test`, `house09_surfaces_test`,
`map_tasks09_test`, `navigation09_connections_test`, `performance09_profile`,
`procedural09_test`,
`render09_backend_probe`, `render09_batch_probe`, `render09_light_range_probe` y
`voice09_acoustics_checks`. `network09_voice_checks.gd` y las pruebas explícitas
de protocolo/versionado quedan con el Director.

## Orden de ejecución solicitado

Cuando el Director conceda turno exclusivo de motor:

1. Importar primero el worktree con Godot 4.5.2 mediante `--headless --editor
   --import --quit --frame-delay 800`, con timeout de 55 segundos y logs únicos.
2. Ejecutar en headless, por separado, identidad procedural, casa, movimiento y
   conexión. Registrar exit code, stdout/stderr y SHA256 de fuentes.
3. Ejecutar `review091_preview_views.gd` con renderer nativo y directorio de
   salida nuevo. Revisar visualmente frente/perfil/espalda; un exit 0 sólo prueba
   que las imágenes y contratos mecánicos se generaron.
4. Tras integrar todos los dueños, repetir rutas/puertas/ataques/online y revisar
   superficies en movimiento. Medir rendimiento sólo con sesión exclusiva y
   candidato final.

La prueba WAN con un amigo no fue ejecutada ni simulada como evidencia real.

## Ejecución parcial del 11 de septiembre

Turno secuencial de Godot 4.5.2, headless. La importación completa terminó con
código 0 en 18,2 s. Resultados válidos del candidato integrado:

- `procedural09_test.gd`: 53/53 después de integrar `a8dd799`. La primera
  corrida había detectado cuatro semillas con metadata de descansos obstruida;
  ese resultado rojo se conserva y la repetición confirmó la corrección.
- `map_tasks09_test.gd`: 88/88.
- `contact_orientation06_test.gd`: 469/469.
- `network_privacy_audit_test.gd`: 48/48.
- `review091_online_cosmetics_contract.gd`: 27/27 en la repetición. El primer
  intento sólo encontró un tipo no inferible en el fixture; no llegó al runtime.

`review091_house_contract.gd` ejecutó 5345 checks sobre las once semillas y
produjo 205 fallos, todos añadidos por el barrido recto conservador contra hojas
abiertas. No fallaron validación, firmas, capacidad, anchos, vuelos físicos ni
rutas físicas lejanas. Un probe sobre semillas 1 y 2 confirmó que
`Arena.step_human` rodea las hojas señaladas y llega sin estancarse (321 y 220
ticks). El gate corregido exige ahora completion física cuando detecta ese
cruce; el corpus final corregido sigue pendiente de un nuevo turno. En las once
semillas no apareció ningún fallo equivalente para mosquito, pero el resultado
final tampoco se declarará hasta repetir el gate completo.
