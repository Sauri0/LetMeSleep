# Asiento, recorrido y herramienta del menú vivo

Entrega fuente de Elementos, 2026-09-12. Parte de MAIN-MENU-LIVING-SCENE.md; requiere integración y secuencia nativa. No constituye aprobación de apoyos, agarre, animación, iluminación ni encuadre.

## Mueble real y apoyos

Se utiliza Lobby_BackSofa_3p3, posición lobby (3.3, 0, 5.35), yaw 0, escala 1. El humano mira hacia -Z del lobby. Asiento existente: ancho 1.82 m, profundidad .72 m, grosor .17 m; bounds X 2.39–4.21, Y .405–.575, Z 4.95–5.67. Bisel de fuente .045 m: el borde de bounds Z 4.95 no equivale a superficie plana a Y .575; el plano superior comienza aproximadamente en Z 4.995. Respaldo frontal Z 5.615, altura máxima 1.175 m. No se modifica el sofá ni sus colliders.

La manta decorativa del sofá derecho se desplaza a X 2.68 (extremos 2.48–2.88); el ribete acompaña. El cojín permanece en X 3.85. Queda libre el centro para el humano. El sofá izquierdo no cambia.

Humanos detectó que el rig a escala 1 no alcanza el suelo desde una pelvis situada al fondo del asiento: muslo .34 m, tibia .32 m. La pose debe sentarse al borde y distinguir contacto de tela/pelvis de pivote óseo Hips. BackSupport es una referencia del mueble, no un contacto obligatorio para esa postura.

Contrato final confirmado por Humanos para el primer nativo:

| Anchor | Posición lobby | Posición en espacio del root humano |
|---|---|---|
| HumanMenuSeatedRoot | (3.3, 0, 5.35), yaw 180°, escala 1 | Origen |
| MenuSeatSurface | (3.3, .575, 5.005) | (0, .575, .345) |
| MenuSeatFrontEdge | (3.3, .575, 4.95) | (0, .575, .40), límite de bounds; no superficie plana |
| MenuSeatBackSupport | (3.3, .93, 5.615) | (0, .93, -.265), informativo |
| MenuSeatLeftFoot | (3.46, 0, 4.65) | (-.16, 0, .70), contacto de suela |
| MenuSeatRightFoot | (3.14, 0, 4.65) | (.16, 0, .70), contacto de suela |

Humanos propone Hips Y .640, UpperLeg Y .670, tobillo Y .120 / Z .63 en espacio actor: alcance cadera–tobillo aproximado .6205 m, inferior a .66 m. Esto acredita alcance geométrico, no ausencia de penetración de piernas, ropa o piel. El gate muestrea el contacto y los .10 m de asiento que quedan detrás, evitando exigir apoyo plano por delante del borde biselado.

Los anchors son hijos directos de PresentationAnchors para enlace explícito mediante Find, sin búsquedas globales. Los puntos de apoyo comparten la orientación del root humano (+Y arriba, +Z frente, +X derecha del actor). No son spawns ni pivotes de huesos. MainMenuCamera, MainMenuLookAt, HumanMenuStage y MosquitoMenuStage conservan sus contratos anteriores hasta que Director conecte el nuevo runtime.

## Ruta y luces de presentación

| Anchor | Posición en lobby, metros |
|---|---|
| MenuMosquitoPath_00 | (2.63, 1.40, 4.72) |
| MenuMosquitoPath_01 | (2.15, 1.65, 4.45) |
| MenuMosquitoPath_02 | (2.35, 1.90, 4.15) |
| MenuMosquitoPath_03 | (3.20, 2.00, 4.20) |
| MenuMosquitoPath_04 | (4.05, 1.85, 4.40) |
| MenuMosquitoPath_05 | (4.50, 1.60, 4.90) |
| MenuMosquitoPath_06 | (4.10, 1.85, 5.20) |
| MenuMosquitoPath_07 | (2.90, 1.95, 5.00) |
| MenuWarmLight | (4.60, 1.75, 4.90) |
| MenuFillLight | (2.10, 2.15, 4.20) |

Presentación acordó curva cuadrática cerrada por tramo: mid(prev, Pi) → Pi → mid(Pi, next). Cada curva queda dentro del triángulo de esos tres puntos. El gate nuevo usa su AABB expandida .18 m por lado, frente a colliders estáticos y renderers del mobiliario. Esta envolvente debe cubrir el mosquito a su escala final, sin aumento añadido. El punto de paso coordinado es .125 P7 + .75 P0 + .125 P1 = (2.60375, 1.50, 4.72125). No se acredita aquí despeje respecto al humano animado o barrido del matamoscas.

Presentación comunicó una propuesta de Humanos para el pase en espacio actor (.40, 1.25, .78), equivalente a lobby (2.90, 1.25, 4.57). Difiere aproximadamente .416 m del paso de esta ruta. Se conserva la ruta entregada hasta medir mano/barrido: la sincronización temporal en fase .5 no demuestra proximidad espacial correcta. Director/Humanos/Presentación conocen esta discrepancia pendiente; no dar por cerrada la reacción de espantar.

Los dos anchors de luz no crean luces ni fijan intensidad/color. Presentación controla sus componentes, sombras, mezcla y ciclo de vida. UI/Director ajustan cámara para la composición aprobada; Elementos no la mueve.

## Matamoscas existente

Reutilizar `Assets/LetMeSleep/Content/Characters/Prefabs/LMS_Flyswatter.prefab`, ToolView.Grip / ToolView.Impact. Fuente actual: `art_source/unity/characters/build_characters.py`, función flyswatter; medidas contrastadas con flyswatter/audit.json y fbx_roundtrip.json. No se crea una herramienta, collider, pickup ni ID de gameplay adicional.

| Medida | Valor |
|---|---|
| Longitud total | .535 m |
| Cabeza exterior | .170 m de ancho × .210 m longitudinal |
| Grip → centro de impacto | .365 m longitudinal, offset normal .005 m |
| Funda del mango | .100 m longitudinal, desde -.045 a +.055 respecto Grip |
| Sección nominal máxima de funda | .034 × .024 m; audit de espesor mallado .022825 m |
| Geometría | 844 triángulos, 1 renderer, 3 huesos |

Socket humano: `CharacterView.GetAnchor("ToolSocket_R")`, hueso fuente Socket.Grip.R. Conservar el pivot del matamoscas y el socket humano. El enlace debe igualar transformaciones de los grips, no asumir que los ejes Blender se importan sin corrección. Reutilizar el criterio de GameplayVisualPresenter.AttachFlyswatter: instanciar bajo el socket, desactivar colliders, obtener ToolView.Grip, aplicar delta `socket.rotation * Quaternion.Inverse(tool.Grip.rotation)` al root y trasladarlo por `socket.position - tool.Grip.position`. Escala 1; animar la mano y dejar que el objeto siga el socket. No usar un anchor estático del lobby como agarre durante el movimiento.

## Verificación y límites

El builder exige los anchors después de creación y serialización, contacto contra triángulos del mesh real, coincidencia de referencia de respaldo, zona central sin manta/cojín, volumen de pantuflas libre y fuera de circulación, separación de spawns y despeje estático del recorrido cuadrático. Esos gates quedan preparados para ejecución por Director; compilar C# no los ejecuta.

Verificación propia: compilación offline C# contra Unity 6000.3.24f1 PASS, hashes de contenido recalculados y git diff --check sin errores. Director debe recalcular los hashes centrales al integrar las demás entregas.

Pendientes nativos: apoyo de ropa y suelas en todas las poses, longitud y pliegue de piernas sin penetración del borde, agarre y dedos durante el barrido, curva con mosquito real y humano animado, lectura de luz/cara/mano a 720/1080 y entrada/salida/personalización sin objetos duplicados. No se ejecutó Unity ni Blender para esta entrega.
