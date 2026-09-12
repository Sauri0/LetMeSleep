# Manos y matamoscas por actor

Cada humano empieza con `GameplayTools.Hands`. `ActorSnapshot.EquippedToolId` es la fuente única para representar lo equipado: `hands` o `flyswatter`. Se eliminó la propiedad global `GameplayAuthority.ToolId`; ningún cosmético concede herramienta. El mosquito siempre tiene hands como ausencia de herramienta y no puede recoger ni dejar.

## Mapa e integración

M2 reserva siete markers en mesas con IDs1001–1007. Añadir `GameplayToolPickup` en cada marker, `PickupId` estable, `ToolId=GameplayTools.Flyswatter`. `Initialize()` crea BoxCollider trigger de interacción si falta (.19×.05×.40 m, centro local0/.025/.18). El root es la pose de agarre en el mundo; se conserva su pose inicial para reiniciar. No marcar estos triggers como superficies perchables.

`VisualRoot` y `InteractionCollider` son opcionales. M2 deja VisualRoot vacío; W2 crea una única representación del matamoscas desde ToolPickups. No instanciar un segundo objeto visual en el marker. El componente se mantiene activo al recoger; desactiva el collider interactuable. Si se le asigna VisualRoot, aplica su visibilidad con la misma regla de dueño, sin reemplazar la autoridad.

El coordinador asigna primero `World.MapRoot` al Transform del mapa activo y después construye `GameplayRoundConfig(..., doors: World.GetDoorDefinitions(), tools: World.GetToolDefinitions())` tanto en host como en cliente. MapRoot es obligatorio y activo; el registro sólo recorre descendientes activos. Pickups, superficies y puertas de otros mapas/escenas aditivas quedan fuera; consultas físicas, interacción, golpes y cámaras auxiliares también filtran con `World.IsWorldCollider`. Colliders de actores sólo pertenecen al mundo que creó ese proxy, aun si otro mundo reutiliza el mismo ID.

El nuevo argumento tools es opcional al final del constructor. El mundo exige que toda la lista coincida con los markers locales antes de aplicar ninguna pose: hasta32 unidades, IDs no repetidos, sólo flyswatter, posición dentro1mm y rotación dentro0.1° de `GameplayToolPickup.Definition` autorada. La comparación normaliza el producto de quaterniones y acepta q/-q equivalentes; no acepta rotación nula/no finita. Los colliders/Hinge/Leaf referenciados deben estar dentro de MapRoot. Un mapa con markers y config sin ellos aborta el inicio, no simula herramientas fantasma. `ToolDefinitionValidation.Matches` expone la comprobación numérica pura para el coordinador antes de ACK.

`ToolPickupDefinition(PickupId,ToolId,Position,Rotation)` es inmutable. `GameSessionState.ToolPickups` copia readonly de `ToolPickupSnapshot(PickupId,ToolId,Position,Rotation,OwnerActorId,Revision)`. OwnerActorId0 significa disponible en mundo; dueño no cero significa unidad consumida/equipada y oculta del mundo. Hay como máximo una unidad por humano. `IGameplayToolWorld` es capacidad física separada: BeginTools, TryToolInteraction, TryDropTool y ApplyToolState; fakes/mapas sin herramientas siguen funcionando sin implementar esa interfaz.

## Interacciones y autoridad

- **F / ActionKind.Use**: humano Active mira la unidad dentro1.5m. Raycast de host respeta el primer obstáculo; no hay PickupId enviado por cliente. Host comprueba ID/revisión/disponibilidad y que el humano tenga manos libres y no esté golpeando. Primera aceptación consume la unidad; otra acción simultánea no puede duplicarla. Si no hay pickup bajo mira, conserva el uso de puerta.
- **G / ActionKind.DropTool**: humano equipado deja el matamoscas. El host busca apoyo delante, comprueba línea, pendiente y volumen libre y publica la pose resultante. Si está bloqueado conserva la herramienta y devuelve Obstructed. Durante un golpe devuelve Cooldown, evitando cambiar la herramienta a mitad del barrido.
- Un humano equipado golpea con flyswatter; otro que conserva hands golpea con manos. El plan manual mantiene distintas geometrías: cara del matamoscas radio.085m, Grip→Impact.365m, alcance total de hombro1.05m, mano derecha. No sumar dos veces longitud del objeto.
- Al salir el dueño, el host deja la unidad en una pose segura. Si ninguna es válida, vuelve a su marker inicial autorizado. La siguiente ronda restaura todos los markers y todos los humanos empiezan con manos.
- No hay tres slots, crafting, lanzamiento, catálogo o inventario general. Cambiar a manos consiste en dejar la unidad.

La visibilidad equipada se reconstruye de cada ActorSnapshot, no del último StrikeState/evento. W2 mantiene el clon de mano oculto hasta EquippedToolId==flyswatter. La representación de mundo usa ToolPickups y sólo se ve con OwnerActorId0. La posición de una unidad equipada conserva el último apoyo pero no se dibuja; su clon en mano sigue ToolSocket_R.

## Red y evidencia

GameplayWireCodec pasa a **versión2**; incluye EquippedToolId y ToolPickups, ActionKind.DropTool e InteractionHint.Tool. Max16 actores/128 puertas/32 pickups dentro16384 bytes. Comprueba dueño humano único, equipo coincidente, IDs/tipos/rotaciones y ausencia de herramientas huérfanas. ReplicaStateGate comprueba IDs de puertas/pickups frente al contenido local antes de aplicar al mundo. Pares con codec v1 se rechazan explícitamente.

Compilación externa contra Unity6000.3.24f1:0 errores/0 advertencias. Suite CPU:58/0. Cinco casos nuevos de autoridad cubren consumo exclusivo/duplicado/inmutabilidad, mosquito/alcance, dejar bloqueado y recoger otro, herramienta de golpe/no dejar en golpe, y salida/reset. Un caso de codec rechaza equipo sin unidad o dueño mosquito; los casos máximos ahora usan16/128/32 y prueban rechazo de33 pickups. Dos pruebas adicionales cubren frontera de1mm/0.1°, equivalencia q/-q, identidad/tipo y números inválidos. El aislamiento jerárquico/físico con escenas aditivas todavía exige Unity Runner; la compilación no acredita esa ejecución.

No se abrió Unity. Pendientes de la escena integrada: línea de visión real y volumen del objeto sobre mesas M2, coincidencia de agarre/impacto M1-W2, teclado/contexto UI, réplica de pickup/drop entre dos clientes y comportamiento bajo latencia. Los fakes no certifican estos gates físicos ni WAN.
