# Lote de acabado doméstico alfa — fuente antes de generación

Autorizado por Director después del rechazo visual de alfa.2 por Branko. Referencias originales: `work/references094/expanded2/first-person-interiors.png` (estar/pasillo, dormitorio, cocina) y `furniture-and-nature.png`. Objetivo: conjuntos domésticos legibles, carpintería y materiales limpios cercanos a esa dirección; no basta con ausencia de defectos técnicos.

Este commit contiene código fuente y compilación offline, **sin abrir Unity/Blender, generar assets Unity, renderizar ni reproducir audio**. Reutiliza meshes/materiales del kit existente; los platos se construyen con una sección torneada de 16 lados mediante Mesh API. Las fuentes editables son `AlfaHouseDressing.cs` y `AlfaFloorFinish.cs`. No requiere una reexportación Blender: Director debe reservar el editor para ejecutar `AlfaMapBuilder.BuildAlfaMaps()` y versionar assets generados por separado. No publicar este lote aislado de animación/UI.

## Conjuntos y límites

- **Estar testigo:** alfombra roja con borde lino y bandas azul oscuro (2.45×2.4 m, centro x1.90/z2.35), agrupando sofá y mesa existentes. Siete libros de alturas y lomos diferenciados sobre dos baldas de Living_Shelf; páginas, tapas y lomo tienen volumen. Matamoscas1005 conserva pose y superficie libres.
- **Dormitorio A:** alfombra azul al costado/pie de cama, libro sobre escritorio a cota exacta del tablero. La mesita mantiene libre el pickup1006. No se cambia cama, escala ni distribución.
- **Cocina:** tabla de cortar y paño plegado sobre encimera este, junto a dos platos con borde e interior reales. No se añade interacción ni se ocupan los dos pickups de la encimera norte.
- **Carpintería:** zócalos de 11 cm en habitaciones, recortados alrededor de vanos; el pasillo y descansos no reciben piezas que estrechen su paso. Alféizar, barra y cortinas de pocos pliegues en ventanas de estar/dormitorio A. Se conservan los vidrios sellados.
- **Luminarias:** 13 plafones compactos de madera y difusor cálido, uno por zona interior ya iluminada. Dimensiones0.58×0.58m, descenso máximo0.16m desde el techo. Anclas de luz debajo del cuerpo, sin añadir componentes Light runtime.

Alfombras y zócalos son detalles visuales de pocos milímetros/centímetros; el suelo y la arquitectura existentes conservan su colisión. Libros tienen cajas ajustadas, ventanas/cortinas y plafones tienen colisión. El builder comprueba los dos pasillos principales de1.8m, conserva los spawns y verifica que ninguna pieza nueva tape las siete reservas de pickup. Estas comprobaciones no sustituyen mirar y recorrer el resultado.

## Contrato con Worker 2

Se conservan nombres `PresentationAnchors/LightAnchor_<ZoneId>`. Posición XZ: centro existente de cada zona del plano. Altura nueva: techo−0.38m, por tanto **y2.42 planta baja / y5.42 planta alta**; orientación local **forward=−Y, up=+Z**. El difusor ocupa y=techo−0.15..−0.11m; la ancla queda0.23m debajo de su cara inferior. Posiciones XZ por zona:

| Zona | X | Z |
|---|---:|---:|
| GroundHall / UpperHall | 6.06 | 5.70 |
| GroundWestLanding / UpperWestLanding | 2.58 | 6.07 |
| GroundEastLanding / UpperEastLanding | 9.88 | 5.66 |
| Living / BedroomA | 2.58 | 2.38 |
| Dining / BedroomB | 9.88 | 2.38 |
| Kitchen | 9.88 | 8.98 |
| Bathroom | 8.32 | 8.98 |
| Utility | 11.15 | 8.98 |

Patio y lobby mantienen sus anclas. M2 no cambia `AlfaLightingRig.cs`: W2 controla distribución, intensidad, sombras/exposición y lectura de personajes. Añadir un cuerpo visible por sí solo no elimina el hotspot de una luz puntual. `House_Diffuser` se crea una sola vez con emisión moderada y el builder conserva ajustes posteriores de W2.

Parámetros materiales confirmados por Director: Floor_Oak tablones de **0.32×1.28m**, juntas3mm, variación tonal máxima7% (dentro del8% autorizado), alternancia de media tabla y solo dos fibras longitudinales de contraste bajo. Patrón512×1024, repetición física1.28×2.56m, mipmaps y filtrado trilineal; generado por CPU usando Texture2D al ejecutar builder. Se activa automáticamente en el Floor_Oak compartido de casa/lobby. UV0 proyectadas por metros después de corregir ejes, compensando el escalado final del lobby; UV2 se preservan.

Bases acordadas: Smoothness madera0.16, revoque0.08, textiles/lino0.10. El builder aplica esas bases; cualquier afinación de W2 debe trasladarse a este contrato fuente para persistir al regenerar. Textura ligada a BaseMap con color blanco multiplicador. No se añaden mapas de ruido a las paredes ni se simula desgaste fotorrealista.

## Muestra y evidencia pendientes

Primero capturar estar desde **Position(4.05,1.53,3.85), LookAt(1.65,1.1,1.70), FOV vertical70°**, y segunda toma desde **Position(3.9,2.2,3.6), LookAt(1.7,1.3,1.4), FOV70°**. Metros locales de HousePatio; comprobar plano contra geometría nativa antes de aceptar encuadre. Mantener además las cámaras originales human.png/mosquito.png para comparar luz/materiales, la cámara dormitorioA de ALFA-REVIEW-CAMERAS y una vista del lobby para el suelo compartido.

Criterios de revisión: sofá/mesa agrupados y estante ocupado sin abarrotar; objetos apoyados, pickup claramente diferenciable, marcos/cortinas sin penetrar muebles, suelo con escala de tabla legible, plafón presente y sin mancha quemada dominante. Evaluar después con W2 luces y animación/UI integradas. No se declara semejanza lograda con los bocetos, calidad gráfica aprobada ni FPS a partir de esta entrega de fuente.
