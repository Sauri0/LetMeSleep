# Navegación de entrenamiento alfa

## Causa observada y alcance

En el primer entrenamiento nativo los mosquitos aparecían en Living a y=2.4, separados del humano del GroundHall por una pared continua. La IA sólo elegía dirección horizontal cada cuatro segundos; no consumía el grafo del mapa ni comprobaba progreso. LivingDoor estaba cerrada y su vano mide 2.2 m: no existía salida física para un mosquito que no puede abrir puertas.

Director autorizó a M2 guardar puertas inicialmente abiertas para alfa, incluido online. Este cambio de mapa es externo a Gameplay. Un humano puede cerrarlas después: la navegación respeta el bloqueo y nunca mueve actores directamente ni abre puertas como mosquito.

## Integración

Asignar `GameplayRuntime.NavigationData = map.SpatialData` antes de BeginRound. World.MapRoot debe ser el mapa activo y su origen debe coincidir con las coordenadas del documento. El adaptador lee `schema_version=1`, `map_id`, `zones`, `portals` y `stair`; rechaza versión/MapId incompatibles. Gameplay no referencia Content.

Cada bot mosquito mantiene visitas propias a regiones. Elige el paso disponible hacia la región adyacente menos visitada; no recibe destinos basados en enemigos invisibles. Sigue puntos de aproximación, centro y salida del vano, a la altura authored de su centro (1.1 m sobre suelo en HousePatio). La escalera usa tramos y descansillo reales, a .85 m sobre peldaños. El exterior sin región y el hueco de servicio sellado no generan conexiones. La percepción de adversarios sigue exigiendo distancia y LOS y toma prioridad al ver un objetivo.

Las puertas se enlazan por `GameplayDoor.name == portal.id` dentro del World registrado; el ID de red continúa siendo DoorId. Puerta ausente o ambigua se considera cerrada. Un paso con puerta necesita al menos 60 grados de apertura; el barrido físico y motor comprueban además el hueco efectivo y los muebles. Esta cota es conservadora, no certifica por sí sola que pase un cuerpo.

Sondeos de esfera/cápsula prueban la dirección deseada y alternativas laterales/verticales, favoreciendo el desvío anterior para reducir oscilación. Ignoran proxies de actores para permitir acercamiento de picadura; el motor conserva todas sus colisiones normales. Sondeos sólo consideran colliders del World activo. Tras 5 s sin avanzar .08 m hacia un waypoint se suspende ese paso durante 6 s y se explora el interior. Begin/Stop limpian rutas y desvíos. Humanos mantienen exploración reactiva con sondeos; Use se emite sólo ante puerta cerrada, para no cerrarla otra vez durante la aproximación.

## Evidencia y límites

Cinco casos CPU: descenso y ruta Living→GroundWestLanding→GroundHall desde tres posiciones (incluidas las dos posiciones atascadas reportadas); permanencia en habitación cerrada; cancelación cuando cierran la puerta; abandono temporal de paso bloqueado; sensor que detiene movimiento sin inventar acciones. El caso de recorrido verifica que cada muestra permanece en las regiones o vanos authored, sin cruzar el tabique directo. Son pruebas cinemáticas de lógica con dimensiones reales, no simulación PhysX.

La compilación externa incluye JsonUtility y Physics contra DLL Unity 6000.3.24f1. La carga del JSON dentro de Unity, conservación de pose de puertas tras recargar prefab, desvíos con muebles, escalera y comportamiento completo de ambos entrenamientos siguen pendientes de un playtest nativo coordinado. No se ejecutó Unity/editor/render para esta entrega.
