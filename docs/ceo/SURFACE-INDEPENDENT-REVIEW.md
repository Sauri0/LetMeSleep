# Revisión independiente de superficies — 2026-09-20

Revisor: modos_dominio. Lectura de runtime/fixtures únicamente; no se abrió Unity ni se modificó física. Revisión retomada después del cuestionario con un harness externo acotado. Estas conclusiones no cierran los 21 fallos ni sustituyen las ejecuciones nativas de CEO.

## Evidencia revisada

- Matriz completa anterior: `N:/LetMeSleep/Validation/V020/SurfaceAB85/Build/20260920-021905-335/native-results/surface-maps.json`: 64 PASS / 21 FAIL de 85; SHA256 `25D0B440E3C1D48428C1E8E7E9395A193ADE6747BFEEEC93F95C7EA53480A7F5`.
- Diagnóstico **parcial**, sólo Isla/Casa/Camp serializados: `N:/LetMeSleep/Validation/V020/SurfaceDiagnostics/Build/20260920-053106-651/native-results/surface-maps.json`; SHA256 `7E878FC45B78C5DC089A9FD66AAC19B7D5FEE3E04F31C951FCB13F0451A20A64`. No inferir datos de Yate/Puerto desde este archivo.
- Candidato `UnityGameplayWorld.SurfaceTraversal.cs`, SHA256 `DF590B40ED4BEE11D2BD395B749C9E80E723E05901C07648C9F53177BB6668FE`; diff contra HEAD 3f48faf y tests `SurfaceTraversalNonConvexPlayModeTests.cs`.
- Resultado nativo ejecutado por CEO: `N:/LetMeSleep/Validation/V020/surface-bevel-01.xml`, 7 PASS, 0 FAIL, 0 skipped, 05:42:39Z. Incluye bisel presente/ausente; es evidencia sintética, no cinco mapas.

## Candidato de biseles

El cambio es razonable para una cara intermedia real: sólo busca sobre los colliders anterior/siguiente, limita la búsqueda a 3 cm de la unión matemática, exige normal intermedia y valida ambos enlaces recursivamente. Profundidad máxima 2; no hay recursión ilimitada. Conserva los límites previos de desplazamiento y `FreeMosquito`. El movimiento posterior sigue pasando por el motor; aceptar un vecino no teletransporta al actor. No encontré un bypass directo de colisión en el delta leído.

La pareja de tests con/sin cara conecta una pared con una tapa mediante un bisel de 7,5 mm y verifica rechazo al quitarlo: cubre la intención principal. Antes de atribuir cierre general, añadir un recorrido completo de autoridad sobre ese mismo bisel y de vuelta, una variante rotada/trasladada y un fragmento diagonal cercano pero desconectado por más de 2 mm de cada extremo. Este último distingue una cara visible de una conexión real. Los tests actuales llaman `TryFollowSurface` directamente; no prueban los 20 ticks de aproximación ni el transporte de dirección del host.

Límites conservados: bisel en un tercer collider no será descubierto; geometría curva con más caras puede quedar fuera de profundidad 2. Eso es soporte limitado, no evidencia de una regresión. La búsqueda crea un array de dos colliders por intento recursivo y puede repetir el mismo collider: mejora de coste posible después de resolver corrección, sin afirmar impacto FPS medido.

## Clasificación histórica de los 21 fallos anteriores al diagnóstico completo

| Casos originales | Cantidad | Evidencia y siguiente comprobación |
|---|---:|---|
| Isla edge/0 Footings, edge/3 Structural Frames | 2 | Actor permanece exactamente en posición inicial durante adquisición. Overlap en posición final/inicial y destino registra Foundation Timbers o Roof/Gable; casts con distancia 0. El fixture no demuestra inicio libre aunque Pen=0. Conservar casos como adquisición bloqueada; agregar inicio realmente libre y verificar timeout/retorno a vuelo. |
| Casa edge/0..3 Stair Step 02..05 | 4 | Adquieren y recorren; rayos válidos muestran normal intermedia 0,7071 antes del detach. Candidato de bisel aborda un defecto plausible. La trayectoria ya llega al Nosing siguiente: ver limitación de objetivo del fixture abajo. Repetir traza completa, no sólo contar normal final. |
| Casa join/0 y join/1 Bathroom/East Wall | 2 | Son el mismo inicio, unión y traza. Último hit de suelo x≈6,674735; pared esperada x=6,76. Falta demostrar conexión en esos ≈85 mm y detectar una pieza intermedia. No afirmar que este par ya estaba conectado ni ampliar tolerancia para unirlo. |
| Camp crawl/ceiling Washroom Floor | 1 | Destino/final se superpone a Terrain Grass. La muestra no prueba un techo accesible desde aire libre. Conservar negativo de bloqueo y buscar otra cara accesible de techo. |
| Camp edge/0 Cooler, edge/1 y /2 Crates | 3 | Adquieren, caminan y luego sueltan. Overlap existe en final; `rawOverlaps` del destino anterior está vacío. El cast diagnóstico se hace desde final hacia atrás: no prueba spawn inválido. Instrumentar consulta real previa al fallo, orientación de triángulos y volumen local antes de clasificar. |
| Yate real-gap Lounge, edge/1 MainDeck | 2 | Fallan durante adquisición; Lounge inmóvil, MainDeck cambia de altura y suelta. Sin diagnóstico completo de overlaps/casts: causa pendiente. Un gap no llegó a probarse porque falló su precondición. |
| Yate edge/3 Flybridge | 1 | Sí cambia de superficie y avanza antes de soltar. Aislar punto exacto de fallo y normal vecina; no extrapolar bisel Casa. |
| Puerto edge/0 Lighthouse Stairway | 1 | Transiciona a Terrain, queda bloqueado aproximando y suelta por timeout. Revisar disponibilidad del camino de cuerpo hacia el contacto, además de continuidad de caras. |
| Puerto edge/2 y /3 BoatWorkshop | 2 | Permanece adherido y avanza sobre Terrain con 2 transiciones; nunca cumple normal objetivo. Puede ser expectativa de ruta inadecuada o soporte que intercepta ruta. No equivale a detach ni bloqueo del motor. |
| Puerto join/0 y /1 Lighthouse | 2 | Misma posición/traza duplicada; pierde adquisición pronto. Falta diagnóstico completo y comprobación de primer sólido. |
| Puerto negative/water/Well_DeepWater | 1 | Adquiere Plaza_ContinuousPaving bajo renderer sin collider. Decisión contenido/agua pendiente; no está posándose sobre un collider de agua. Fixture no distingue esa política de un fallo de raycast. |

## Defectos del diagnóstico y selección

1. `Diagnose` construye `motorHits` desde `final.position` hacia `last.contactPoint + last.normal * .057f`. Después de detach es un cast de retorno, **no la consulta que falló ni evidencia del spawn**. Registrar además posición/velocidad/query inmediatamente anteriores a `MoveMosquito`, candidatos front/under/wrap y el motivo exacto de rechazo. Mantener los campos actuales como diagnóstico posterior explícito.
2. `Witness` y `FreeLine` usan Pen como criterio de libertad. Esta propia corrida presenta overlaps con Pen=0. Añadir una precondición de volumen libre equivalente al motor y barrido de aproximación; conservar cualquier muestra rechazada y su razón. No convertirla a PASS ni borrarla; generar un reemplazo reproducible con identificador nuevo.
3. Join comprueba suelo a 20 cm de la pared y pared a 20 cm del suelo, no la unión intermedia. Hace falta testigo continuo y volumen recorrible antes de llamarla unión física. Deduplicar futuros candidatos por posición/normal/collider: Casa y Faro duplican sendas muestras, por lo que 85 filas no son 85 situaciones independientes.
4. Edge identifica dos triángulos compartidos pero no exige que la trayectoria observada siga esos contactos. En Casa espera normal +Z del Step anunciado, mientras el actor termina escalando otro Nosing con normal -Z. Eso descubre un problema real durante la ruta, pero no valida ni invalida de forma aislada la arista anunciada. Separar recorrido real de escaleras de transición de una arista identificada, registrando ambos colliders/triángulos y llegada.
5. Raycasts fallidos serializan `raw.point`, `raw.normal` y `raw.triangleIndex` pese a `hit=false`; aparecen valores indeterminados y no finitos. Serializar esos campos como null cuando no hay hit, además de proteger al escritor. Evita interpretar basura nativa como geometría.
6. La búsqueda se limita por prioridad, área y número de casos. Los negativos de agua usan nombres/bounds de renderers. Mantener ese alcance declarado; no convertir un resultado mejor de esta muestra en aceptación total de mapas.

## Orden de trabajo propuesto

Conservar matriz anterior y completar diagnóstico actual. Evaluar el bisel contra los mismos IDs sin tocar criterios. Después agregar fixtures de inicio libre y rutas validadas, manteniendo fallos históricos y negativos de bloqueo por separado. Priorizar Casa bisel, Casa hueco/intermedio y Camp geometría previa al detach; esperar evidencia completa para Yate/Puerto. Ninguna propuesta de este informe autoriza cambiar las reglas de agua ni aceptar colisiones omitidas.


## Actualización: diagnóstico completo y harness preparado

El reporte `SurfaceDiagnostics/Build/20260920-053637-101/native-results/surface-maps.json` ya terminó con 64 PASS / 21 FAIL, sin errores y cleanup=true. Esta revisión conserva su contenido íntegro. En Casa edge/0..3, el candidato de bisel **ya no se desprende**: llega adherido al tick 106, con nueve transiciones y sobre Step05..08. El fallo todavía refleja la normal objetivo incompatible del fixture; no hay base para llamarlo PASS ni para decir que el cambio no mejoró el movimiento. La tabla histórica superior se mantiene como registro de la evidencia anterior.

Se preparó exclusivamente en `N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes`:

- `SurfaceMapChecks.cs`: copia del harness original, conserva selección/criterios originales; añade identificación de suite/baseline, observador y null para campos de raycasts fallidos.
- `TracingGameplayWorld.cs`: decorador transparente de IGameplayWorld, SurfaceTraversal, Tools, Bounds y Mode. Captura las MotorQuery/TrySurface/TryFollowSurface reales antes de delegar al mundo integrado y el resultado después. Casts auxiliares se etiquetan como consultas informativas, no como iteraciones internas del motor.
- `ValidatedRoutes.cs`: cinco slots positivos nuevos (Casa pared, techo y ascenso Step02→Step03; Camp pared y techo real), precondiciones de esfera completa libre, barrido, huella física y unión de hitPoints reales; tres negativos separados sobre los inicios bloqueados originales. Cada rechazo geométrico queda en selection. Sin candidato válido = COVERAGE_GAP. Una vez ejecutado el primer candidato válido, su fallo no permite buscar otro más fácil.
- `baseline85.json`: copia byte a byte de los 85 resultados completos; cada ejecución la preserva como otro artefacto y no recalcula esos resultados a partir de los nuevos. `original-preservation.json` registra hash y comparación exacta de regiones originales de selección y criterios.
- `Compile-ValidatedRoutes.ps1`, configs focused/replay85, README y recibos. No se copian fuentes del juego para crear una simulación alternativa; se referencian los DLLs Unity integrados y se registran sus hashes.

Precondiciones positivas: radio 55 mm, OverlapSphere vacío, SphereCastAll con extensión de 1 mm, play bounds y testigos de superficie cada 1 mm. Unión escalera: extremos observados mediante raycast a no más de 2 mm. Durante el recorrido real también se rechazan overlaps de cuerpo de 54 mm aunque Pen=0. El perfil de escalera es una ruta explícita de la geometría actual; si cambió o no se valida, se registra hueco de cobertura. No ampliar tolerancias para que pase.

Evidencia offline: ambos runners compilaron con 0 warnings / 0 errores. Auditoría de baseline byte idéntico (85 filas, 64/21) y regiones de selección/criterios originales sin cambios: PASS. No ejecución de Unity por este trabajador.

- Focused final preparado: `N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes/Build/20260920-062546-157/run-in-coordinator-slot.cs`.
- Replay85 opcional: `N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes/Build/20260920-062549-010/run-in-coordinator-slot.cs`.

Pendiente de CEO: ejecutar focused con su turno Unity, comprobar las cinco rutas solicitadas y negativos, y diagnosticar cualquier COVERAGE_GAP/FAIL sin ocultarlo. Los positivos nuevos no reemplazan los fallos históricos ni certifican navegación completa de cinco mapas.

## Revisión del delta nativo 03 — aceptación limitada

Lectura independiente de `surface-mode-witness-03.xml`: 16/16 PASS, cero omitidos, ejecutados a las 06:35:45Z. Son **11 casos de superficies y 5 de modos**; no 16 casos de superficies. Ejecución realizada por CEO. El helper de runtime conserva SHA256 `DF590B40ED4BEE11D2BD395B749C9E80E723E05901C07648C9F53177BB6668FE`. Tests revisados: SHA256 `2CAA4B95038D3B88253815862B892896FEB7C289176D8C335D9B9EF39A298A2D`.

Conclusión: **favorable a integrar el cambio acotado de bisel**, sin cerrar los fallos de mapas. La selección de cara intermedia sigue limitada a los dos colliders elegibles; cada rama valida sus dos enlaces; profundidad, normales y distancias están acotadas; la llegada conserva FreeMosquito y la autoridad usa el motor real. No encontré un bypass de colisión introducido por el delta leído.

Los tests nuevos cubren los huecos principales señalados antes: autoridad real con inputs, aproximación inicial, continuidad de attachment, transporte de dirección, llegada a la otra normal y estabilidad durante cinco ticks. Ejecutan ambos sentidos como casos independientes, no como una ida y vuelta consecutiva del mismo actor. Limitan cada desplazamiento observado a menos de 4 cm. El caso rotado/trasladado prueba conectividad positiva y rechaza una cara diagonal que permanece visible pero está separada 7,5 mm de sus extremos. La pareja con/sin bisel y los negativos anteriores de hueco, placa y llegada bloqueada siguen pasando.

Límite de la afirmación de penetración: `Fixture.PenetratesMap` continúa usando ComputePenetration. El PASS afirma que esa consulta no detectó penetración en este fixture; no demuestra ausencia general de overlaps por caras posteriores. Mejora concreta recomendada: sumar OverlapSphere filtrado a colliders activos del mapa por tick, sin ignorar el soporte ni sustituirlo por Pen=0. El focused externo ya comprueba overlaps de 54 mm durante positivos. Este límite no invalida las comprobaciones de movimiento/continuidad que sí hace el test ni constituye por sí solo un bloqueo del delta acotado.

Permanecen fuera de la aceptación: biseles en un tercer collider, cadenas que excedan la profundidad dos, escalas no uniformes, geometría móvil durante el cruce, rutas humanas y aceptación completa de los cinco mapas. No se midió coste/FPS de las consultas. Mantener los 21 FAIL históricos y resolverlos con causas/rutas separadas, aunque estos tests sintéticos pasen.

## Revisión de clearance y runners v2 — 07:04 UTC

Lectura independiente del delta de `GameplayAuthority.cs` (SHA256 `EA2419AF9E32243B0944EFC06EC701828FF67120A51DAAB00A46E110C3C0311B`), `UnityGameplayWorld.cs` (`75F1EC3F17410ADB456A334B40328A6346CC691C8C30E259A51DDB007783F51A`) y `SurfaceDestinationClearancePlayModeTests.cs` (`D7609042E6CE8B8656E33781EEC8A3D38914709E3056FFD5582931F542BF8119`). No se editó runtime ni se abrió Unity por este revisor.

Dictamen: favorable al control acotado de destino ocupado, con límite anatómico explícito pendiente de prueba. `TrySurface` conserva el primer collider válido de `FirstRay`; rechazar su destino no busca una superficie posterior. Verifica esfera de 54 mm en el centro de apoyo a 57 mm, con valores finitos y normal unitaria. Filtra colliders del mundo, ignora el actor propio y rechaza otros actores registrados con collider no trigger, incluida la cápsula motora humana. No modifica la selección de picadura. `TryFollowSurface` también pasa por la guardia mediante `TrySurface`; conserva comprobaciones de conexión, distancias y volumen del vecino. La autoridad comprueba adquisición y cada tick de ApproachingSurface, incluso al aproximarse al vecino durante una transición; al fallar despega antes de mover hacia ese destino.

Evidencia nativa ejecutada por CEO: `N:/LetMeSleep/Validation/V020/clearance-voice-native-01.xml`, 07:03:19Z, 19/19 PASS y cero omitidos. Desglose verificado: **2 clearance, 11 superficies, 5 modos y 1 voz**. Los dos tests nuevos prueban soporte visible como primer hit con obstáculo fuera del rayo y ocupación sobrevenida por otro mosquito durante aproximación. El segundo coloca físicamente el proxy ajeno antes del tick; verifica que la autoridad despega ante ese estado del mundo, no un recorrido completo de ese otro jugador por inputs. Los once casos previos conservan cobertura sintética de vecinos conectados/bloqueados, biseles, transformaciones y tránsito autoritativo en ambos sentidos.

Límite concreto comunicado a CEO/técnica: la guardia usa `QueryTriggerInteraction.Ignore`, pero `BlocksMotor` trata los `GameplayBodySurface` trigger como sólidos para mosquitos. Un brazo extendido fuera de la cápsula humana puede ocupar el destino sin aparecer en esa consulta. No hay reproducción nativa de ese caso todavía. Añadir fixture donde sólo la anatomía trigger intersecte la esfera y la cápsula humana quede fuera; si se exige paridad con el motor, evaluar `Collide` más el filtro físico correspondiente. No cambiarlo por inferencia mientras las fuentes están congeladas. Tampoco los dos tests nuevos aíslan triggers de mapa, colliders ajenos al mundo o una primera superficie no elegible con otra válida detrás; el código conserva sus filtros, pero la ejecución actual no certifica esos casos específicamente.

La guardia comprueba destino, no todo el corredor de aproximación. El motor mantiene radio de 55 mm y el timeout de aproximación continúa como salida para una ruta obstruida con destino libre. El margen entre 54 y 55 mm conserva la tolerancia preexistente de FreeMosquito; no afirmar que sea una prueba exacta del volumen completo ni ausencia universal de penetración.

El primer focused completo permanece en `SurfaceValidatedRoutes/Build/20260920-062546-157/native-results/surface-maps.json`: 5 PASS, 2 COVERAGE_GAP, 1 FAIL; errores vacíos y cleanup=true. El negativo Camp mostró spawn sin overlap, luego aproximación hasta una posición superpuesta a Terrain y aceptación de Surface: evidencia de destino inseguro, sin usar el cast posterior al detach como prueba de spawn. Casa perfil y Camp techo quedaron como huecos de selección; no se convirtieron a PASS.

V2 conserva ese resultado byte a byte y el baseline85 (64/21); sólo renombra Casa perfil y Camp techo. Corrige tick de observación a CurrentTick, obtiene el perfil Casa de triángulos reales con extremos/tolerancias/recorrido corporal/aserciones de llegada intactos, y distribuye candidatos entre objetos de techo estrictos en Camp. Registra rechazos y ejecuta sólo el primer candidato geométricamente válido, sin buscar otro tras un fallo de gameplay. `TracingGameplayWorld` implementa y delega `ISurfaceClearanceWorld` sin fallback para no ocultar la guardia integrada.

Ambos runners finales compilados offline contra los assemblies centrales importados, 0 advertencias / 0 errores:

- Focused: `N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes/Build/20260920-070404-168/run-in-coordinator-slot.cs`.
- Replay85: `N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes/Build/20260920-070409-172/run-in-coordinator-slot.cs`.

Cada build incluye fuentes, hashes de dependencias, `v1-v2.diff` y `v2-preservation.json`. Auditoría PASS: dieciséis métodos originales de selección/aserción, método del negativo bloqueado, inicio/final, corredor y gameplay de escalera conservados. La ejecución nativa de estos nuevos runners queda a cargo de CEO; no hay resultado v2 declarado en esta revisión. Los 21 FAIL históricos siguen abiertos.
