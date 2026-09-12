# Exterior testigo alfa — primera entrega Mapas

## Iteración exterior2 — corrección tras vistas dedicadas

Fuente revisada tras abrir las cinco `exterior1-{window-front,window-side,patio-forward,patio-reverse,patio-mosquito-eye}.png`, central61956fa. Son cámaras estáticas, no recorrido. Los defectos observados justifican esta revisión: EX1 copas separadas como diamantes; EX2 patio plano, parches aislados y vacío detrás de la cerca; EX3 fachada sin espesor aparente. En mosquito-eye el follaje casi ocupa toda la imagen aunque la prueba anterior de distancia al spawn pasaba.

- Copas: una superficie cerrada y conectada por árbol, faldones de ramas lobulados y ramas que entran en el volumen. Tres variantes del mismo árbol; versión de patio podada con faldón sobre la línea de visión inicial y versión silvestre de falda más baja para el fondo. Se conserva tronco/pose/collider de los dos árboles del patio.
- Patio: suelo continuo con transición de color tenue, bancales de tierra continuos de borde irregular que reúnen los grupos existentes, follaje más ancho y menos tupido, camino con losas facetadas y juntas sobre el recorrido fijo. Losas hasta14mm sobre el soporte, bordes de tierra hasta20mm; nada cambia los colliders ni el corredor central.
- Perímetro: terreno real también al oeste, este y detrás de la cerca, con grupos de árboles/rocas/sotobosque a varias distancias. No existe nueva área jugable. Las piezas de terreno contiguas no se superponen en planta; los elementos toman su altura del terreno bajo cada raíz.
- Fachada: jambas y dinteles exteriores de ocho ventanas, alféizares posteriores/laterales, esquineros, bandas entre pisos y fascias que siguen la pendiente del gablete. Bevel absoluto en las piezas largas, para evitar que el bisel se escale con la longitud del tablero. Huecos/vidrios/carpintería interior permanecen intactos.

Banco EX4: Elementos confirmó su propiedad y entrega independiente `4a62d6b`; Mapas no edita el banco. Vidrio/exposición EX5 queda en Presentación; la presente revisión no altera luces ni materiales comunes.

Estado de exterior2: receta, JSON y editable regenerados; **inspección nativa Unity aún pendiente**. Son40mallas,328instancias y5,262triángulos únicos; estos números no son una aprobación artística. Distancias mínimas al spawn oeste/este:247.366mm y653.886mm. Los dos rayos centrales desde los ojos hacia `(6,1.4,14)` no chocan con la geometría cercana analizada; esto no prueba la apertura de todo el campo visual ni navegación. El receipt nativo de la sección histórica corresponde a exterior1.

Director concedió el turno BlenderCPU2 de≤60s. PID36772 terminó ExitCode0 en aproximadamente12s: `ExteriorWitnessKit.blend` actualizado y `tree-volume-exterior2.png` generado con CyclesCPU16muestras640px, sin Unity. CIM posterior no encontró el proceso y el turno fue liberado explícitamente. El importador comprobó vértices transformados contra Unity antes de guardar; error máximo3.8444e−6m. `blender_ex2_receipt.json` registra hashes, tamaños, versión, PID y logs. No se cambió geometría ni contratos de `9ea74ab` durante el turno.

La imagen aislada se abrió y revisó: la copa es continua y desaparecen los diamantes separados; todavía se leen tres estratos regulares y un tramo largo de tronco desnudo. No permite aprobar la poda desde la cámara mosquito ni la composición del patio. Esta limitación se entrega expresamente para comparación nativa; la iluminación de estudio no representa materiales ni exposición nocturna de Unity. Los avisos de Blender se limitan a futura deprecación de Material.use_nodes/World.use_nodes en6.0; ejecución actual5.2.1LTS completada.

## Registro de exterior1

Responsable: Modelador Terreno y Mapas. Base `c888d96`, worktree `N:/LetMeSleep/Worktrees/maps`, rama `codex/unity-maps-specialist`. La reasignación de TEAM-RECOVERY-20260912 prevalece sobre el AGENTS histórico.

Estado: fuentes geométricas, importador compilado offline y `ExteriorWitnessKit.blend` reconstruido. **Aún no aprobado visualmente ni probado en recorrido nativo.** Director concedió turno Blender CPU de dos hilos sin render: PID32600 terminó ExitCode0 en aproximadamente dos segundos y el turno se liberó explícitamente. Blender 5.2.1 LTS, hash9e2066aef7ef; único aviso de futura deprecación de Material.use_nodes. No se inició Unity.

## Referencias y composición

Se miraron las seis imágenes originales `N:/LetMeSleep/References/EnvironmentQuality-20260912/01.png` a `06.png`. 01/02 guían piezas con función, aristas y materiales; 03/05/06 guían grupos de vegetación, piedra irregular y profundidad por capas; 04 guía la lectura del exterior enmarcado desde un interior cálido. Las imágenes no autorizan mapas, armas ni mecánicas adicionales.

- Primer plano: dos islas bajas al pie de las ventanas del frente. Tierra de contorno irregular, piedras distintas, grupos de hojas plegadas y hierba con cresta. Sin imagen pegada al vidrio.
- Plano medio: árboles con tronco, ramas visibles y masas de follaje asimétricas. Tres variantes, giro y escala elegidos. El rayo desde el living hacia la ventana sigue hacia X negativo al salir hacia −Z; la composición tiene árboles a ambos lados de ese rayo.
- Fondo: terreno cerrado con relieve bajo y árboles más distantes. El Director autorizó esta reserva decorativa fuera de la frontera. Todo es geometría, sin colisión ni nueva zona de juego.
- Patio: cuatro islas bajas en sus bordes; dos copas reconstruidas sobre los troncos originales. Centro y acceso quedan libres. La cerca conserva su geometría estructural y suma tapas biseladas.
- Fachada: zócalo de piezas de mampostería biseladas, con interrupciones amplias en las puertas; alféizares exteriores y faldones, sin cerrar huecos. La mampostería entra 2.5 mm en el soporte para evitar piezas flotantes.

Paleta mate: verdes fríos, corteza cálida contenida, piedra gris oliva y tierra apagada. Sin emisiones ni nuevas luces. Presentación conserva distribución/exposición nocturnas. Las masas siguen siendo de pocas caras intencionales, no cubos escalados para representar plantas.

## Fuentes propias

`art_source/unity/environments/quality_exterior/`:

- `build_exterior.py`: receta determinista editable, Python estándar, geometría y colocación en metros Unity +Y/+Z.
- `generated_exterior.json`: 20 mallas con submateriales, 209 instancias; 5,934 triángulos únicos, no conteo total de escena ni rendimiento medido.
- `geometry_validation.json`: comprobación offline de índices, coordenadas, triángulos no degenerados y distancia real a los dos spawns de mosquito del patio.
- `import_blender.py`: reconstruye las mallas exactas como objetos enlazados editables por colección y material. Conversión Unity `(x,y,z)` → Blender `(x,-z,y)`; giro Y de Unity → giro Z positivo de Blender.
- `ExteriorWitnessKit.blend`: archivo editable guardado, 20 mallas y 209 objetos; `blender_reconstruction_receipt.json` registra PID, versión, hashes, tamaño y rutas de logs. No contiene un render de aprobación.
- `verify_importer.ps1` y `offline_compile_receipt.json`: compilación aislada contra Unity 6000.3.24f1 y fuentes reales de dependencias. No lanza editor, no importa assets y no equivale a prueba de integración.

Archivo Unity propio: `unity/Assets/LetMeSleep/Content/Editor/Environment/AlfaQualityExterior.cs` con `.meta`. No se editaron AlfaMapBuilder, AlfaHouseDressing, AlfaQualityLiving, AlfaQualityMeshes, AlfaLobbyDressing, hash común, escenas ni prefabs.

## Conexión del Director

En `AlfaMapBuilder.BuildAlfaMaps`, sobre casa recién creada, **después de `AddHouseDressing(house,data,plan)` y antes de `BindGameplay(house,10000,1)`**:

```csharp
AlfaQualityExterior.Build(house);
```

No llamar directamente sobre el prefab ya generado: la receta exige casa sin raíz `QualityExterior`. El build general crea otra casa y vuelve a aplicar el módulo. La creación de assets se hace por AssetDatabase; salida propia `Assets/LetMeSleep/Content/Environment/AlfaMaps/QualityExterior/{Meshes,Materials}`. Director conserva integración y generación de escenas/prefabs.

Añadir al cálculo de content hash de casa estas entradas, sin cambiar el hash de lobby por geometría que no usa:

1. `art_source/unity/environments/quality_exterior/generated_exterior.json`
2. `unity/Assets/LetMeSleep/Content/Editor/Environment/AlfaQualityExterior.cs`

Si el esquema común exige incluir fuentes de autoría, sumar `build_exterior.py`; no es necesario incluir receipts con fecha. No se modificó el hash unilateralmente.

El importador resuelve por nombre exacto `Patio_Pine_W` y `Patio_Pine_E`, desactiva sus LODGroups/renderers y añade las copas/troncos visuales nuevos. **No destruye ni mueve las entidades originales.** Conserva colliders, matrices, capas y GameplaySurface mediante snapshot antes/después, y conserva PlayBounds, arrays y poses de spawns/pickups. No añade colisiones ni luces: se mantiene el orden de asignación de IDs del builder.

Ventana testigo, acordada con Elementos: centro `(1.38,1.70,.09)`, hueco X `.78..1.98`, Y `1.15..2.25`, vista hacia −Z. Sus ocho vidrios, colliders, montantes y carpintería interior quedan intocados. Frente jugable hasta Z `−2`, frontera `−2.1`; relieve decorativo comienza en `−2.23`. Accesos X `4.8..7.3` despejados.

## Verificación y captura pendiente

La primera revisión geométrica detectó follaje a 58 mm del centro del spawn mosquito oeste. Se giró sólo la copa visual: distancia final mínima **110.652 mm** al spawn `(2,1.8,17)`; spawn este `(10,1.8,17)`, **878.128 mm**. Radio físico conocido 55 mm. Esto verifica ausencia de intersección inicial contra triángulos nuevos; no demuestra navegabilidad, visibilidad durante vuelo ni comportamiento runtime.

El build nativo emitirá `LMS_QUALITY_EXTERIOR_BUILT` y `unity_import_receipt.json` con invariancia de colliders/IDs/anchors. Después, capturar con luz nocturna real:

1. Living equivalente a round5/crafted1: posición `(4.05,1.53,3.85)`, objetivo `(1.65,1.1,1.70)`, FOV70, confirmado Director. Comparar específicamente LC5 del informe visual.
2. Ventana cerca desde el interior, dos posiciones laterales; comprobar parallax, que ramas no tapan todo el hueco y que el vidrio conserva transparencia.
3. Patio a altura humana desde su puerta hacia banco y esquina oeste; luego vista inversa. Revisar apoyo de plantas/piedras, rutas libres, copas y cerca.
4. Patio a altura mosquito, incluyendo sus dos posiciones de aparición; comprobar hojas en ambos lados y ausencia de aparición visual dentro de ramas.
5. Fachada frontal como vista de autor adicional, claramente distinta del recorrido jugable. No usar esa cámara exterior como evidencia de cámara runtime.

Brechas abiertas: importación y capturas nativas pendientes; aprobación de luz/materiales/composición pendiente; ninguna medición FPS/memoria. Cerca estructural, cubierta continua y banco siguen sus fuentes existentes y todavía requieren revisión artística posterior. No se proclama patio/fachada terminados por sumar este módulo. Los pendientes de geometry_validation.json reflejan el momento de la prueba geométrica anterior; los receipts posteriores documentan compilación y reconstrucción completadas.
