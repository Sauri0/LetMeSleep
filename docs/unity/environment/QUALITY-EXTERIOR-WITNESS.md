# Exterior testigo alfa — primera entrega Mapas

Responsable: Modelador Terreno y Mapas. Base `c888d96`, worktree `N:/LetMeSleep/Worktrees/maps`, rama `codex/unity-maps-specialist`. La reasignación de TEAM-RECOVERY-20260912 prevalece sobre el AGENTS histórico.

Estado: fuentes geométricas e importador compilado offline. **Aún no aprobado visualmente ni probado en recorrido nativo.** Blender pendiente de turno del Director; no se afirma que el `.blend` exista hasta su reconstrucción. No se inició Unity ni Blender durante esta preparación.

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

Brechas abiertas: importación y capturas nativas pendientes; aprobación de luz/materiales/composición pendiente; `.blend` pendiente de turno; ninguna medición FPS/memoria. Cerca estructural, cubierta continua y banco siguen sus fuentes existentes y todavía requieren revisión artística posterior. No se proclama patio/fachada terminados por sumar este módulo.
