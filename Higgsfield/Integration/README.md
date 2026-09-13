# Integración técnica de mapas Higgsfield

Encargo del 2026-09-13 para apoyar los cinco mapas aprobados. El alfa anterior está pausado. Este lote aporta código y contrato de importación, sin autoría de geometría, generaciones, importación parcial, procesos Unity/Blender ni renders.

## Archivos nuevos

- `unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldEnvironmentImporter.cs`: importación manual de una receta y un FBX final.
- `.../HiggsfieldImportContract.cs`: validación del contrato y resolución de reglas por ruta.
- `unity/Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/HiggsfieldLowPolyWater.cs`: ondas visuales en ejecución mediante blendshapes existentes o una copia privada de MeshFilter.
- `Higgsfield/Integration/isla.recipe.DRAFT.json`: plantilla bloqueada intencionalmente; faltan FBX final, hash, límites y clasificación completa. Incluye los 37 colores del audit intermedio, pendientes de confirmar al finalizar.
- `Higgsfield/Integration/isla-audit-observations.json`: procedencia y hash de la lectura del audit intermedio; dos aguas con tres materiales y Wave_A/Wave_B cada una.
- `Verify-Offline.ps1`, `ContractChecks.cs`, `offline-validation.json`: compilación contra APIs instaladas y pruebas ejecutadas de contrato puro.

Todos los archivos Unity nuevos llevan `.meta`. Se aprovechan las dos asambleas existentes de Environment; no se modifica ningún archivo compartido, assembly contract ni configuración del proyecto.

## Contrato con Scene Builder y exportación

Blender trabaja en metros con Z arriba. El FBX debe conservar normales, nombres únicos entre hermanos, materiales con nombres únicos, EMPTYs y jerarquía; aplicar escalas negativas/cero antes de exportar. El importador habilita la conversión de ejes y la escala de unidades del FBX con multiplicador configurable `importScale`. **Esa configuración no certifica la conversión**: comparar en Unity una medida conocida, orientación vertical, muelle/cabaña y posiciones de spawns antes de aceptar el mapa. No corregir a ciegas con otro giro de 90°.

Para Isla, los nombres comunicados son `Water_Ocean`, `Water_Lake`, `Spawn_Human_01`, `Spawn_Human_02`, `Spawn_Mosquito_01`; todavía no se han verificado en el export. Las rutas de receta son exactas, relativas al root importado y sensibles a mayúsculas; un EMPTY anidado se indica por ejemplo `Markers/Spawn_Human_01`. `$root` permite clasificar una malla ubicada en el propio root. No se inventan spawns para completar cupos.

La plantilla pide explícitamente sólo 2 humanos y 1 mosquito para una revisión provisional. Los valores por defecto del contrato son 16 por rol; la receta definitiva debe fijarlos según el contrato de roles acordado con Gameplay/Online. El recibo indica `has16MarkersPerRole`, que **no equivale a capacidad de partida validada**. Se rechazan nombres repetidos, orígenes de spawns a 10 cm o menos y spawns fuera de límites; faltan pruebas con cápsulas humanas, separación suficiente, vuelo y selección para las distribuciones de roles permitidas.

`playBoundsMin/Max` son coordenadas Unity XYZ en metros y delimitan sólo tierra/espacio jugable. Alternativamente, `boundsMinEmpty/boundsMaxEmpty` toma dos EMPTYs exportados que deben quedar ordenados como mínimo/máximo en Unity. Nunca se calculan límites desde el plano oceánico de 600 m. `PlayBounds` sólo declara datos: este lote no añade paredes invisibles, lógica de caídas ni contención de jugadores.

Las cámaras `Camera_Overview/Camera_Beach/Camera_Cabin` siguen perteneciendo a la fuente artística. El importador desactiva cámaras y luces del modelo; no añade iluminación ni setup de presentación. `presentationRoot`, si se usa, referencia un grupo exportado de EMPTYs para que Presentation conecte sus cámaras después. Tool pickups y lobby son arrays opcionales de EMPTYs; no se crean herramientas ni lobby.

## Colores, colisión y agua

Cada material FBX usado necesita un swatch `{sourceName, rgb:[r,g,b], colorSpace:"srgb"|"linear"}` con valores 0..1 verificados contra la fuente. Los valores de `base_color` del audit Blender se conservan literalmente con etiqueta linear; el importador convierte a gamma para el color serializado del material Unity. No confundirlos con números sRGB ni aplicar dos conversiones. Se crean materiales URP/Lit opacos, sin texturas, metallic 0 y smoothness 0. **Cada ranura conserva su posición y remapea por nombre; no se colapsan submeshes ni colores de aguas/rocas.** El recibo registra slots, submeshes y modo de agua por malla. Se conserva la topología y las normales importadas; no se triangula ni modela arte nuevo. Vidrios, emisión y efectos especiales requieren un contrato posterior: no se simulan aquí. Comparación de color bajo iluminación equivalente pendiente.

Cada MeshRenderer debe resolver una regla de `nodes`. Categorías:

| kind | Colisión | Comportamiento |
|---|---|---|
| solid | MeshCollider estático no convexo | Capa configurada, GameplaySurface con ID y CanPerch |
| water | Ninguna | Ondas suaves; no proyecta sombras |
| foam | Ninguna | Mismo componente de ondas; no proyecta sombras |
| foliage | Ninguna | Conserva proyección y recepción de sombras |
| decoration | Ninguna | Visual estático con sombras |

`descendants:true` aplica una regla a una rama. Gana la ruta coincidente más específica, así un bosque puede ser foliage y `Forest/Tree01/Trunk` solid. No hay deducción por palabras del nombre. Las reglas inexistentes, duplicadas, sin uso y las mallas no clasificadas causan fallo. Los troncos grandes deben estar separados del follaje y la hierba; si el FBX une tronco y hojas en una sola malla, hace falta separar esa geometría en origen o acordar un proxy de colisión. No se puede filtrar parte de una malla con esta receta. Las puertas incluidas como solid quedan estáticas; puertas interactivas necesitan integración posterior.

El agua conserva vértices/triángulos, sin tocar assets o colliders sólidos. El FBX importa blendshapes: si el agua llega como SkinnedMeshRenderer sin huesos, exige Wave_A/Wave_B (nombre exacto o sufijo `.Wave_A/.Wave_B` no ambiguo), alterna sus pesos suavemente con suma 100 y restaura los pesos previos al desactivar. La amplitud procede de las formas autoradas; `waveAmplitude/waveLength` no alteran ese modo. No se admiten otros objetos skinned. Su deformación, normales y bounds de culling deben comprobarse en Unity.

Si llega como MeshFilter, el componente usa una copia privada y desplaza sólo Y mundial. Amplitud máxima 15 cm y límite 20 000 vértices por malla CPU, sin tessellation. La malla original se restaura al desactivar y se libera la copia. Recalcula normales y bounds de la copia; mantiene las divisiones de vértices existentes. Un plano sin suficientes subdivisiones sólo se inclinará suavemente: aquí no se agrega geometría. En Isla, el audit tiene 6 561 vértices de océano y 1 537 de lago, pero el conteo importado puede crecer por normales y materiales y aún no está verificado.

No crea espuma si no existe. Agua y espuma deben revisar juntas su coincidencia: los parámetros iguales ayudan únicamente cuando ambas usan la misma función CPU; una orilla CPU junto a Wave_A/B no garantiza alineación y puede requerir exportar formas equivalentes para la espuma. Se conserva Read/Write en el modelo por ahora; coste de memoria, CPU/GPU y movimiento visual pendientes de medición nativa. El tiempo es cosmético y no sincronizado por red.

## Ejecución futura en turno autorizado

1. Esperar confirmación del FBX terminado. Verificar nombres, materiales, dimensiones y hash; completar una copia de la receta. `sourceFinal:false`, hash incorrecto o límites vacíos bloquean la importación antes de escribir assets.
2. En el Editor residente e inactivo, usar `Let Me Sleep > Higgsfield > Import Final Environment Recipe`, o invocar `LetMeSleep.Content.Editor.Higgsfield.HiggsfieldEnvironmentImporter.Import(rutaAbsoluta)`.
3. El importador copia el FBX y la receta a una carpeta **nueva** `Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/<mapId>/`. Genera materiales, `Prefabs/<mapId>.prefab`, `Scenes/<mapId>.unity`, `Data/import-receipt.json`. Si ya existe la carpeta, falla para evitar sobrescribirla: usar un ID de revisión nuevo.
4. La escena contiene sólo el prefab ambiental. En interactivo se valida que todas las escenas abiertas tengan ruta guardada, se trabaja en escena aditiva temporal y se restaura la escena activa previa; no se guardan escenas ajenas ni se inicia Play, render o bake. Una escena Untitled bloquea antes de escribir assets, sin diálogo de guardado automático. En batch, únicamente si hay una sola escena inicial sin ruta, sin objetos y sin cambios, se permite Single: esa escena vacía se reemplaza y la generada queda abierta hasta terminar el proceso. Si hay objetos o datos sin guardar, falla. Un fallo posterior deja `Data/import-incomplete.txt` para inspección y no hace rollback destructivo. No registrar carpetas incompletas. Ante fallo se conserva la evidencia antes de decidir una limpieza explícita.
5. Revisar el recibo, las posiciones importadas y los límites. Probar escalas/ejes, colisión de rutas/escaleras, spawns, lectura visual, agua y costes. Ni la compilación offline ni el recibo de importación constituyen aprobación visual o funcional.

Entrada para un proceso batch que el encargado lance **sólo en su turno**: `-executeMethod LetMeSleep.Content.Editor.Higgsfield.HiggsfieldEnvironmentImporter.ImportFromCommandLine -higgsfieldRecipe "N:/ruta/final.recipe.json" -quit`. La ruta es obligatoria y única; un error termina batch con código 1. No se incluye ni se ejecuta un lanzador de Unity en este lote.

## Puntos concretos para integración posterior

Identidades propuestas, aún sin registrar:

| Mapa | ID definitivo propuesto |
|---|---|
| Isla del Laguito | hf-isla-del-laguito-v1 |
| Casa del Patio | hf-casa-del-patio-v1 |
| Campamento Pinar | hf-campamento-pinar-v1 |
| Yate a la Deriva | hf-yate-a-la-deriva-v1 |
| Puerto del Faro | hf-puerto-del-faro-v1 |

- **Carga / RoomRules:** registrar explícitamente el ID definitivo y la ruta de escena/prefab después de validar; conectar `EnvironmentMapDefinition` y validar distribución de roles/cupo 16. Este lote no cambia selección aleatoria, reglas, modos ni ciclos de partida.
- **Gameplay:** usar los arrays de Transform ya serializados; herramientas sólo tienen puntos. Conectar contención por `PlayBounds`, caídas/agua, navegación, puertas y comprobación real de cápsulas/flight. Los colliders reciben IDs deterministas por ruta ordenada desde `firstSurfaceId`, registrados en recibo. Coordinar rangos entre mapas. Una modificación de sólidos puede renumerarlos: tratarla como revisión de contenido incompatible, nunca conservar partidas activas cruzando versiones.
- **Online:** `ContentHash` combina contrato, SHA256 del FBX y bytes exactos de receta (incluidos colores, reglas y spawns). Es una huella de entrada; antes de usarla como hash autoritativo de red incorporar versión de importador/runtime y validación de assets generados según el pipeline central. Validar hash e ID en carga y no admitir revisión provisional como mapa definitivo.
- **UI:** asociar nombre visible y miniatura real aprobada al ID registrado; no derivar catálogo de todas las carpetas de Assets. La plantilla y los mapas de revisión deben quedar fuera de selección pública.
- **Presentation:** conectar sus cámaras/luces/sonido a la escena ambiental o a `PresentationAnchors`. Revisar día/noche según cada boceto sin imponer una iluminación común desde este importador.

## Evidencia disponible

`Verify-Offline.ps1` compila runtime, superficie y Editor por separado contra Unity 6000.3.24f1 y ejecuta 24 comprobaciones de contrato con .NET, sin iniciar Unity/Blender. Comprueba clasificación por jerarquía/override, exclusión de vegetación/agua, rechazo de fuente parcial, hash ausente, rutas inseguras, límites, spawns insuficientes/duplicados y swatches/encoding inválidos. El recibo contiene hashes exactos de fuentes y dependencias. No se ha importado ningún FBX ni se han generado escenas, prefabs o capturas.
