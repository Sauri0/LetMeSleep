# Lobby alfa amueblado y fondo de menú

Director autorizó pulir el lobby después de la importación nativa correcta de los mapas y siete pickups. El shell inicial de cuatro meshes era una sala vacía. Este delta reutiliza los FBX y meshes existentes mediante `AlfaLobbyDressing.cs`, parte del mismo `AlfaMapBuilder.BuildAlfaMaps()`. No requiere exportación Blender ni nuevos modelos. La generación y revisión visual nativas de este delta quedan pendientes del Director.

## Espacio y composición

La instancia del shell del lobby se escala (1.4, 1, 1.5): interior **14 × 12 × 3.2 m**, límites x=±7/z=±6. El FBX fuente conserva 10×8; el plano y `EnvironmentMapDefinition.PlayBounds` describen la instancia final. Los muebles conservan escala y dimensiones originales. La casa no cambia geométricamente.

El centro 6×4 y los 16 spawns se conservan exactamente. Una reserva de circulación de **9.6×7.6 m** incluye el centro y un anillo continuo de 1.8 m. Los bancos, pinos y demás sólidos quedan fuera de esta reserva hasta 2.2 m de altura. El builder comprueba tanto colliders como bounds visuales del mobiliario antes y después de serializar. Suelo y vigas superiores están permitidos; las piezas del suelo añadidas son visuales y la colisión sigue siendo el suelo del shell.

| Elemento existente | Colocación local (m) |
|---|---|
| Cuatro bancos laterales | x=±6.25, z=±2.3, mirando al centro |
| Dos sofás del fondo | x=±3.3, z=5.35, mirando hacia −Z; sustituyen bancos en el lote doméstico |
| Cuatro pinos | x=±5.85, z=±4.75; altura original aprox. 3.05 m |
| Estantería | (0,0,5.7), bajo el panel central |
| Inserto textil azul | 6×4 m en el suelo central; 1 cm de espesor visual, sin collider |
| Zócalos y barandillas | Zócalos x=±6.95/z=±5.95; barandillas x=±6.85 |
| Vigas de madera | y=3.10, apoyadas contra el techo; laterales x=±4.7 y travesaño z=4 |
| Tres faroles | x=−4.8/0/4.8, y=2.45, z=5.74 |

Las piezas arquitectónicas y faroles reutilizan el mesh biselado y UV2 del tablero `Kit_Table_Top`, adaptado por dimensiones y materiales existentes. Solo el material cálido emisivo `Lobby_LanternGlow` es nuevo. Los faroles tienen marco sólido; el núcleo emisivo es visual. No contienen luces runtime: las tres anclas de luz quedan para Presentation. La escena de revisión sí incluye iluminación provisional cálida, separada del prefab como antes.

## Contrato para menú

Los siguientes Transform cuelgan de `EnvironmentMapDefinition.PresentationAnchors`. Son locales a `PrivateLobby`; usar TransformPoint/TransformDirection si la raíz tiene transformación. No son nuevos spawns ni actores registrados en Gameplay.

| Ancla | Posición local | Uso |
|---|---|---|
| MainMenuCamera | (0,1.6,0.4) | Cámara ya orientada al LookAt; FOV vertical inicial sugerido 55°, near 0.05/far 100, 16:9 |
| MainMenuLookAt | (0.8,1.1,4.85) | Objetivo del encuadre, hacia el fondo amueblado |
| HumanMenuStage | (1.65,0.02,4.7) | Origen de pies; yaw hacia cámara |
| MosquitoMenuStage | (0.8,1.85,4.5) | Centro del mosquito; orientación hacia cámara |
| LightAnchor_Lobby_Lantern_m4p8 | (−4.8,2.45,5.55) | Luz delante del farol izquierdo |
| LightAnchor_Lobby_Lantern_0 | (0,2.45,5.55) | Luz delante del farol central |
| LightAnchor_Lobby_Lantern_4p8 | (4.8,2.45,5.55) | Luz delante del farol derecho |

Director/Presentation colocan clones cosméticos en los stages para el fondo del menú. Mantener deshabilitados los proxies/colliders de esos clones durante el lobby jugable para no ocupar su circulación. El builder valida espacio libre para el humano (r=0.25/alto=1.72), mosquito (r=0.055) y cámara (r=0.05); preserva sus anclas al guardar/reabrir. La transparencia y composición final de UI pueden requerir ajustar cámara/LookAt; se debe comprobar en captura real, no asumir desde coordenadas.

## Verificación y captura

El método nativo sigue siendo `LetMeSleep.Content.Editor.AlfaMapBuilder.BuildAlfaMaps()`, con las escenas generadas cerradas. Además de sus comprobaciones existentes, aborta si el mobiliario invade la reserva central/anillo o si se pierden las anclas. El `.meta` del nuevo script se entrega; Unity crea los assets derivados por sus APIs.

Para revisión, usar primero la cámara de menú con fondo transparente de UI, verificar que cabeza/pies y mosquito no se corten, que ningún pino o farol tape las siluetas y que la luz permita leer el pijama. Después observar desde (0,2.5,−5.3) hacia (0,1,1), FOV vertical 75°, para revisar ambos laterales y la continuidad del suelo. Los 16 actores, colisiones, encuadre y luz reales requieren ejecución nativa. No se afirma rendimiento ni calidad visual aprobada a partir de la compilación offline.

## Corrección de caras interiores tras captura nativa

La captura `N:/LetMeSleep/Artifacts/review/alfa-ui-main-dressed.png`, tomada con MainMenuCamera, mostraba cielo en lugar de techo/paredes. Las franjas azules eran paneles decorativos; el shell no tenía muros bajos ni huecos geométricos. La inspección CPU del blend entregado en c98783b encontró **las seis caras de la cavidad invertidas**, cada una con dot −1 respecto a la normal esperada hacia el interior.

Causa: el recálculo global de normales orientaba la cavidad cerrada, desconectada de la piel exterior, como otro volumen positivo. Así todas sus caras resultaban ocultas desde dentro por el descarte de caras posteriores. Manifold y volumen total positivo no detectaban este error.

`build_sources.py` ahora orienta cada cara de shell desde el sólido ocupado hacia el espacio vacío y conserva ese winding, incluida la cavidad. `repair_lobby_normals.py` aplica esa misma corrección únicamente al blend/FBX de lobby existente: invierte seis caras y comprueba que ningún vértice cambie de posición. Los FBX de casa y kit permanecen intactos. No se habilita material de doble cara ni se añade un segundo techo.

`check_lobby_normals.py` documenta antes/después en `lobby_normals_before.json` y `lobby_normals_after.json`: seis planos pasan de dot −1 a +1. La validación de fuentes/reimportación sigue en 811 checks / 0 fallos. El builder añade una comprobación nativa después de la conversión FBX: cada uno de los seis planos interiores debe tener triángulos y normales de sombreado orientados hacia el cuarto. Quedan pendientes su ejecución y una nueva captura del Director.

El ancla del mosquito se mueve a (0.8,1.85,4.5) para situarlo en la franja central visible entre los paneles de UI de esa captura. La posición del humano y la cámara se conservan.
## Lote doméstico posterior a menu-1080.png

Captura revisada: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/menu-1080.png`. Tres paneles azules sin contenido, estantería vacía y banco de una sola pieza dominaban el fondo. Este delta separado del fix del paño transforma el fondo en una zona de descanso y guardado coherente con los conjuntos del estar, sin nueva función de juego.

- El builder retira únicamente las tres instancias `Lobby_Wall_Panel*`, decorativas y sin collider. Conserva el shell cerrado y su comprobación de seis caras.
- Dos sofás del kit sustituyen los dos bancos traseros, en la misma posición. Cada uno recibe cojín azul apoyado en y0.575 y paño claro sobre el asiento; la banda apoya sobre el paño. Los cuatro bancos laterales permanecen.
- Estantería central con siete libros orientados con el lomo hacia la sala (−Z) y dos cajas de guardado en la balda inferior. Base de cajas y libros coincide con la cara superior de su balda; las tapas apoyan en las cajas. No son pickups ni inventario.
- Panelado bajo de madera en la pared posterior, dividido por montantes y rematado con moldura, conecta visualmente ambos sofás y la estantería. Permanece detrás del mobiliario. Perchero de entrada de tres ganchos a x−2/y1.7/z5.92, sin cartel ni mecánica inventada.

Todo el conjunto se mantiene fuera de la reserva9.6×7.6m y del centro con16spawns. Cámaras, stages y anclas de luz conservados. Las comprobaciones existentes de circulación visual/física y espacio de humano/mosquito se ejecutan antes/después de guardar; se añaden comprobaciones de apoyo de libros/cajas/tapas y textiles sobre asiento, y ausencia de los paneles provisionales.

Coordinación W2 a partir de la misma captura: `Lobby_LanternGlow` tenía emisión(2,0.92,0.24) y es distinto de `House_Diffuser`. Ambos pasan a emisión(0.18,0.10,0.035), base(0.90,0.78,0.57), aplicada en cada build; conservan Smoothness0.25 lobby/0.10 casa. Worker2 ajusta luces, bloom y relleno; M2 no añade ni modifica componentes Light runtime. Afinación futura debe persistirse en fuente.

Fuente C# y compilación offline solamente: sin Unity, Blender, GPU ni audio. Generación y evaluación del nuevo menú con luces W2 quedan pendientes; la aprobación visual no se deriva de los controles de apoyo.