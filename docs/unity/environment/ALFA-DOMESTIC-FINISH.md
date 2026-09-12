# Lote de acabado doméstico alfa — fuente antes de generación

Autorizado por Director después del rechazo visual de alfa.2 por Branko. Referencias originales: `work/references094/expanded2/first-person-interiors.png` (estar/pasillo, dormitorio, cocina) y `furniture-and-nature.png`. Objetivo: conjuntos domésticos legibles, carpintería y materiales limpios cercanos a esa dirección; no basta con ausencia de defectos técnicos.

Este commit contiene código fuente y compilación offline, **sin abrir Unity/Blender, generar assets Unity, renderizar ni reproducir audio**. Reutiliza meshes/materiales del kit existente; los platos se construyen con una sección torneada de 16 lados mediante Mesh API. Las fuentes editables son `AlfaHouseDressing.cs` y `AlfaFloorFinish.cs`. No requiere una reexportación Blender: Director debe reservar el editor para ejecutar `AlfaMapBuilder.BuildAlfaMaps()` y versionar assets generados por separado. No publicar este lote aislado de animación/UI.

## Conjuntos y límites

- **Estar testigo:** alfombra roja con borde lino y bandas azul oscuro (2.45×2.4 m, centro x1.90/z2.35), sofá con dos cojines y paño, mesa de café exclusiva a0.480m. Siete libros solidarios con Living_Shelf, ahora contra la pared posterior libre. Matamoscas1005 conserva ID/XZ y baja a y0.485 para apoyar sobre la tapa nueva.
- **Dormitorio A:** alfombra azul al costado/pie de cama, libro sobre escritorio a cota exacta del tablero. La mesita mantiene libre el pickup1006. No se cambia cama, escala ni distribución.
- **Cocina:** tabla de cortar y paño plegado sobre encimera este, junto a dos platos con borde e interior reales. No se añade interacción ni se ocupan los dos pickups de la encimera norte.
- **Carpintería:** zócalos de 11 cm en habitaciones, recortados alrededor de vanos; el pasillo y descansos no reciben piezas que estrechen su paso. Alféizar, barra y cortinas de pocos pliegues en ventanas de estar/dormitorio A. Ventanas fijas con collider, ahora con material transparente exclusivo conforme a W2.
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

Patio y lobby mantienen sus anclas. M2 no cambia `AlfaLightingRig.cs`: W2 controla distribución, intensidad, sombras/exposición y lectura de personajes. Añadir un cuerpo visible por sí solo no elimina el hotspot de una luz puntual. `House_Diffuser` reaplica en cada build BaseColor(0.90,0.78,0.57), EmissionColor(0.18,0.10,0.035), keyword _EMISSION y Smoothness0.10. W2 debe trasladar sus ajustes a esta fuente para que sean reproducibles.

Parámetros materiales confirmados por Director (junta corregida posteriormente a5mm para corresponder al raster de2.5mm por texel): Floor_Oak tablones de **0.32×1.28m**, juntas5mm, variación tonal máxima7% (dentro del8% autorizado), alternancia de media tabla y solo dos fibras longitudinales de contraste bajo. Patrón512×1024, repetición física1.28×2.56m, mipmaps y filtrado trilineal; generado por CPU usando Texture2D al ejecutar builder. Se activa automáticamente en el Floor_Oak compartido de casa/lobby. UV0 proyectadas por metros después de corregir ejes, compensando el escalado final del lobby; UV2 se preservan.

Bases acordadas: Smoothness madera0.16, revoque0.08, textiles/lino0.10. El builder aplica esas bases; cualquier afinación de W2 debe trasladarse a este contrato fuente para persistir al regenerar. Textura ligada a BaseMap con color blanco multiplicador. No se añaden mapas de ruido a las paredes ni se simula desgaste fotorrealista.

## Muestra y evidencia pendientes

Primero capturar estar desde **Position(4.05,1.53,3.85), LookAt(1.65,1.1,1.70), FOV vertical70°**, y segunda toma desde **Position(3.9,2.2,3.6), LookAt(1.7,1.3,1.4), FOV70°**. Metros locales de HousePatio; comprobar plano contra geometría nativa antes de aceptar encuadre. Mantener además las cámaras originales human.png/mosquito.png para comparar luz/materiales, la cámara dormitorioA de ALFA-REVIEW-CAMERAS y una vista del lobby para el suelo compartido.

Criterios de revisión: sofá/mesa agrupados y estante ocupado sin abarrotar; objetos apoyados, pickup claramente diferenciable, marcos/cortinas sin penetrar muebles, suelo con escala de tabla legible, plafón presente y sin mancha quemada dominante. Evaluar después con W2 luces y animación/UI integradas. No se declara semejanza lograda con los bocetos, calidad gráfica aprobada ni FPS a partir de esta entrega de fuente.

## Correcciones de QA posteriores a la primera generación

- **Platos:** inversión del winding de ambos triángulos de cada sector. Se comprueban triángulos no degenerados, 16 triángulos de base hacia−Y, 16 de cuenco hacia+Y y volumen firmado positivo. Verificación numérica CPU:128 triángulos, volumen0.0009077762m³. El mismo gate se ejecuta al crear mesh y después de guardar/reabrir prefab.
- **Alféizar/estantería:** alféizar reduce fondo de0.18 a0.10m, centroz0.205; límitesz0.155..0.255. Panel de estantería empiezaz0.275:20mm libres, sin mover el mueble ni sus libros. Se añade comprobación de intersección positiva entre todos los colliders de RoomCarpentry y los colliders de Furnishings, antes/después de serializar.
- **Juntas de suelo:** contrato explícito5mm, dos texeles de2.5mm a512×1024, sin aumentar resolución. Sustituye el objetivo inicial3mm que el raster binario representaba como5mm. Mipmaps/filtrado pueden suavizar el límite en pantalla;5mm es el ancho autorado al nivel base.
- **Difusor:** los cuatro valores autorados se reaplican también a materiales existentes. Se marca el asset como modificado; cualquier ajuste futuro de W2 requiere cambio de fuente.

Compilación offline contra Unity6000.3.24f1 correcta. No se abrió editor ni se generaron assets en esta corrección. QA debe revisar el delta; la regeneración y comprobación visual corresponden al Director.
Actualización coordinada con W2 tras menu-1080.png: House_Diffuser y Lobby_LanternGlow reducen emisión a(0.18,0.10,0.035), persistida en fuente; ver LOBBY-DRESSING.md. No cambian las anclas de luz.

## Corrección compositiva posterior a living-round2.png

Captura real revisada: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/living-round2.png`. Confirmó que el estante bloqueaba visualmente la ventana pese a los20mm de separación de la corrección anterior, y que la mesa de0.81m correspondía a comedor. La ausencia de intersección no había resuelto el uso del estar.

- Living_Shelf pasa de(1.9,0,0.45),yaw0 a(1.9,0,4.24),yaw180, contra la pared posterior. Los siete libros se expresan en coordenadas locales de ese mueble y comparten su posición/rotación; lomos hacia−Z. Envolvente aproximada x1.30…2.50,z4.065…4.415, lejos de la puerta cuyo centro x3.86/z4.67. Reserva de acceso a ventana x0.605…2.155,z0.18…1.08,y0…2.4, libre de muebles y libros.
- Living_Table se construye como variante propia desde el mesh biselado del kit: tapa1.35×0.06×0.70, top0.480, root(2.4,0,2.35),yaw90. Patas0.07×0.412×0.07, base0.008 sobre campo de alfombra y unión superior0.420 bajo la tapa. Huella de tapa en mundo x2.05…2.75,z1.675…3.025. Se conserva mesa Dining_Table y pickups comedor a0.815; no se modifica el prefab Kit_Table ni FBX.
- Pickup1005 conserva identidad, tipo, orientación y XZ; Y0.815→0.485,5mm sobre la nueva tapa. La reducción de altura es necesaria para evitar que quede flotando. QA aceptó estas cotas, incluida la diferencia de95mm entre tapa y asiento de sofá0.575.
- Living_Sofa conserva pose y collider. Dos cojines azul/lino de0.38×0.32×0.16 y paño0.38×0.012×0.60 descansan en y0.575; banda apoyada sobre paño. Son visuales sin collider y quedan dentro de la superficie del asiento.
- Nuevos gates antes/después de serializar: acceso a ventana libre; aproximación a puerta x2.70…4.98,z3.40…4.58 libre; posición/orientación del estante; apoyo y huella de todos los libros/textiles; unión patas/tapa/alfombra; pickup a tapa+5mm; comedor permanece a0.810. Continúan gates de pasillos, spawns, carpintería y siete pickups. Alfombra no cambia ni recibe collider.

### Ventana: investigación y cambio autorizado

La fuente build_sources.py ya sustrae ocho huecos reales de HouseShell y coloca panes finos separados; el problema visual es Glass_Blue_Opaque. No se modifica el FBX, el hueco, el marco ni la colisión. Ese material opaco también pertenece a horno/lavadora: se conserva.

Por autorización del Director y contrato W2, EnsureWindowGlassMaterial crea/reaplica Window_Glass URP/Lit: BaseColor(0.22,0.42,0.55,0.32), Surface1, AlphaBlend0, SrcAlpha/OneMinusSrcAlpha, ZWrite0, renderQueue3000, RenderTypeTransparent, keyword _SURFACE_TYPE_TRANSPARENT, sin premultiply, Metallic0, Smoothness0.55, ReceiveShadows0 y _RECEIVE_SHADOWS_OFF, ShadowCaster desactivado. Sólo los renderers Window_*_Pane del shell de casa reciben el material; no reciben/proyectan sombras ni usan reflection probes. No se añade shader, SSR ni luz.

Gate: ocho panes, asignación determinista, alpha/cola/estados del material, collider habilitado no trigger, layerWorldStatic, transform y dimensiones iguales al manifest de fuente, bounds físico/visual coincidentes; horno/lavadora siguen opacos. El gate genérico de SavePrefabAndInstantiate conserva GameplaySurface/SurfaceId y bounds de todos los colliders durante serialización. Transparencia visual no significa ventana transitable.

Pendiente nativo: regenerar con Unity6000.3.24f1; comprobar vidrio contra cielo/exterior desde ambos lados, profundidad/ordenamiento/bloom y conservación física del pane. La ventana del estar mira hacia el frente de la casa, no al patio posterior; el exterior visible depende del contenido que realmente exista fuera. Capturar estar desde ambas esquinas para incluir nueva estantería y sofá/mesa, además del primer plano de vidrio. Fuente y compilación offline no acreditan apariencia final ni jugabilidad.
