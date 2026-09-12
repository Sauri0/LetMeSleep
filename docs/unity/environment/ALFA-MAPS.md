# Casa/patio y lobby alfa — fuentes y builder

Lote autorizado por Director después de que la muestra pasó importación nativa el 12 septiembre 2026 a las 09:54:43Z. Solo casa fija de dos pisos con patio y lobby separado. No se generan los otros cuatro mapas del ciclo.

## Entrada y carga

En el editor residente, después de integrar y compilar:

```csharp
LetMeSleep.Content.Editor.AlfaMapBuilder.BuildAlfaMaps();
```

Prefabs raíz generados:

- `Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/HousePatio.prefab`
- `Assets/LetMeSleep/Content/Environment/AlfaMaps/Prefabs/PrivateLobby.prefab`

Escenas de revisión: mismo directorio base, `Scenes/HousePatio.unity` y `Scenes/PrivateLobby.unity`. No se modifican Build Settings, paquetes o TagManager. El método usa escenas aditivas temporales, preserva la activa y rechaza reconstruir con esas escenas abiertas. Las luces y cámaras de revisión son objetos de escena, fuera del prefab fuente; Worker 2 sustituye la iluminación provisional.

Raíz de cada prefab contiene `LetMeSleep.Content.Environment.EnvironmentMapDefinition`, ensamblado `LetMeSleep.Content.Environment`:

| Campo público | Contrato |
|---|---|
| MapId | `house-patio-v1` / `private-lobby-v1` |
| ContentHash | `sha256-` + 64 hex; contrato exacto en `ALFA-CONTENT-HASHES.json` |
| SpatialData | TextAsset del plano fijo; límites, zonas, portales, escalera y reservas |
| HumanSpawnPoints | 5 Transform en casa; origen a nivel de pies con 0.02 m de margen |
| MosquitoSpawnPoints | 16 Transform en casa; **centro de esfera** radio 0.055 m, sin sumar altura |
| LobbySpawnPoints | 16 Transform solo en lobby; origen de pies con 0.02 m de margen |
| ToolPickupPoints | 7 Transform sobre mesas/mesitas de casa; lista estable, todavía sin componente de recogida no entregado por W1 |
| PresentationAnchors | Nodos de zonas AudioZone, ReflectionVolume, LightAnchor y CameraCollision |
| PlayBounds | Envolvente de juego local a la raíz; no sustituye las colisiones de muebles/arquitectura |
| GeometryContract | `lms-environment-alfa-1` |

Director instancia el prefab, obtiene este componente y selecciona anchors según roles ya sorteados. M2 no crea sesión ni vuelve a sortear equipos. `World.RegisterGeometry()` pertenece a la integración de Gameplay posterior a instanciar el mapa.

## Planta fija

Envolvente de casa 12.80 × 11.40 m; la propuesta inicial 12 × 10 m se amplió para conservar dormitorio muestra de 4.80 × 4.40 m, pasillos de 1.80 m y escalera de 1.60 m sin estrecharlos. Piso terminado en y=0/3 m, altura interior 2.80 m. Techo exterior a dos aguas, cumbrera y=8.20 m. Patio de 12.80 × 8.00 m detrás de la casa.

| Planta | Función y conexiones |
|---|---|
| Baja | Vestíbulo/pasillo longitudinal, estar, comedor, cocina, descansos laterales y escalera. Entrada frontal y salida al patio |
| Alta | Pasillo y descansos, dos dormitorios, baño y lavado; acceso desde escalera sin atravesar dormitorios |
| Patio | Sendero 1.80 m desde salida, banco y dos pinos fuera de circulación; cerca visible |
| Lobby independiente | Interior 10 × 8 × 3.20 m, centro libre 6 × 4 m y circulación perimetral 1.80 m; decoración en paredes sin bancos que estrechen esquinas |

Escalera en U: 18 contrahuellas de 1/6 m, dos tramos de 9 con 8 huellas de 0.28 m por tramo. Ancho útil 1.60 m, rellano intermedio 1.60 m, aproximación/salida 1.80 m. Hueco de forjado real sobre toda la caja, barandas en el separador de 0.30 m y protección del borde superior; no se pone baranda atravesando la salida de escalera. El volumen bajo escalera no se presenta como ruta humana.

Nueve puertas usan hoja probada de 1.10 m de paso libre × 2.20 m. Portales de distribución sin hoja mantienen 1.80 m. Los huecos estructurales dejan lugar para jambas reales. Los muebles no se instancian como habitaciones completas: solo se reutilizan cama, mesita, escritorio, silla y puerta de la muestra. Un único shell controla encuentros de paredes/pisos/techo, con huecos reales y sin superposición de módulos cerrados.

Los once muebles nuevos son mesa, sofá, módulo de cocina, cocina/horno, heladera, lavabo, inodoro, lavadora, estantería, banco y pino. Son geometría funcional y superficies de apoyo; cocinar/lavar/tareas no se habilitan como mecánicas nuevas por modelarlos. Follaje visual no bloquea vuelo; el tronco sí tiene colisión. Vidrios opacos provisionales sellan ventanas y no proyectan sombra.

La envolvente física exterior incluye límites laterales y superior de vuelo, explícitos bajo `WorldBoundary_NoPerch`; no son superficies posables ni una continuación invisible del suelo. Se debe revisar su lectura exterior/cámara en movimiento. El suelo y el techo físico de la casa siguen sus colisiones reales.

## Gameplay y puntos de herramienta

Dependencias entregadas por W1: `1ebb459` + `3dced63`, incorporadas a este worktree como `1883cac` y `70a67d8` solo para compilar el puente. No se alteró Gameplay. Cada collider no trigger lleva GameplaySurface con ID único/no cero/revisión1. IDs ordenados por ruta estable dentro de la versión: casa desde10000, lobby desde20000. El ContentHash cambia cuando cambia el plano o builder. No se asignan IDs por instanceID de Unity.

Cada GameplayDoor tiene DoorId único, SurfaceId de su Leaf, Hinge/Leaf/Handle explícitos, OpenSign=-1, OpenDegrees=100, InitialDegrees=0. El centro se comprueba con `DoorDefinition.LeafCenterLocal` derivado del collider real. La puerta permanece cerrada en prefab; el runtime aplica el estado autoritativo.

ToolPickupPoints, en orden estable: `Pickup_KitchenCounter_A`, `Pickup_KitchenCounter_B`, `Pickup_DiningTable_A`, `Pickup_DiningTable_B`, `Pickup_LivingTable`, `Pickup_BedroomANightstand`, `Pickup_UtilityCounter`. Los markers están 5 mm por encima de superficies y tienen up=+Y. W1 añadirá GameplayToolPickup/ToolId/validación/estado al entregar su componente; aquí no se inventa inventario ni se declara recogida funcionando. `TaskFuture_Utility` es marcador futuro, inactivo para alfa.

## Fuentes, reproducción y evidencia

Fuentes editables y exportaciones en `art_source/unity/environments/alfa_maps/`: `house_alfa_static.blend/.fbx`, `lobby_alfa_static.blend/.fbx`, `furniture_kit_alfa.blend/.fbx`, `build_sources.py`, `source_manifest.json` y `source_validation.json`. Plano en `room_sample/house_layout_plan.json`, reproducible con `build_house_plan.ps1`.

Blender 5.2.1 CPU, sin render: **811 checks / 0 fallos**, incluyendo manifold/volumen, colliders positivos y reimportación de los tres FBX (jerarquía/límites a tolerancia0.1mm). Casa119 meshes/4612tri; lobby4/60tri; kit50/2140tri antes de instancias. No son conteos finales de escena ni mediciones de rendimiento.

El builder y los tipos exactos del puente compilan offline contra APIs de Unity6000.3.24f1. `verify_unity_builder.ps1` compila también los tipos entregados GameplayDoor/GameplaySurface y el nuevo descriptor; no sustituye pruebas de todo Gameplay. Los `.meta` de scripts/asmdefs están incluidos; Unity creará los de FBX, meshes, prefabs y escenas generados.

`compute_content_hash.ps1` reproduce exactamente ContentHash: ID de mapa + lista ordenada de rutas y SHA256; texto UTF8 con saltos LF, FBX sin modificar. Incluye fuentes FBX, manifests, layout y C# de generación/descriptor; no incluye timestamps del recibo. Mantener esos inputs y assets generados vinculados al mismo commit.

Al ejecutar, el builder verifica bounds/ejes y hornea la conversión FBX probada en copias Mesh, conservando UV2 y corrigiendo normales/tangentes/winding. Comprueba IDs, referencias de puerta, posiciones de spawn contra AABB conservadoras de geometría y conservación de colliders/IDs tras guardar y reabrir el prefab. Escribe `ALFA-MAPS-IMPORT-RECEIPT.json` únicamente tras completar ambos mapas. Ese recibo no existe como evidencia hasta ejecución por Director.

Pendientes nativos: generar ambos mapas, revisar luces/UV2 y pérdida de luz, recorrer escalera/puertas con ambos roles, cámara, spawns, recogida real de herramienta y partida. Sin editor/render/playtest propio ni afirmación de WAN/FPS en este lote.
