# Personajes Unity 0.9.4 alfa — contrato de fuente

Propietario: Modelador 1. Integración Unity: Director / Presentation. Pareja base, sin variantes. La autorización del Director del 12 de septiembre de 2026 permite terminar alfa sin esperar aprobación artística de Branko mientras duerme. No autoriza ampliar este lote a beta. La segunda entrega agrega builder Unity, todos los estados fuente alfa y matamoscas; ver UNITY-INTEGRATION.md para API y estado de ejecución.

## Escala y ejes

Fuentes Blender en metros, Z arriba, frente -Y, derecha +X. Objetos y rig con escala 1, sin espejos negativos. FBX exporta con `axis_forward=-Z`, `axis_up=Y`, unidades métricas; Unity debe mostrar Y arriba y frente +Z. El integrador comprobará orientación, medida y materiales en el importador real; la inspección Blender no sustituye esa prueba.

| Contrato | Humano | Mosquito |
|---|---|---|
| Raíz de fuente | Suelo entre los pies | Centro del tórax |
| Escala visual Unity | 1 | 0,5 aprobada por Director |
| Dimensiones previstas | Cuerpo 1,72 m; gorro hasta ~1,93 m | Fuente cuerpo ~0,22 m, largo total ~0,38 m y alas ~0,48 m; Unity mitad |
| Cámara | Socket.Eye a 1,53 m | Cámara externa a cargo de Presentation |
| Colisión de juego | Cápsula radio 0,25 m, altura 1,72 m, agachado 1,0 m | Radio 0,055 m, independiente de alas |
| Sombras/colisión de accesorios | Gorro fuera de cápsula | Alas y antenas sin colisión |

Las medidas reales de exportación están en cada `audit.json`. La anchura del humano auditada corresponde a la pose T. No inferir la cápsula de los límites del renderizador. El mosquito tiene pies por debajo de su raíz: compensar el posado por la distancia raíz-suelo, escalada por 0,5; no desplazar su malla destructivamente.

## Rig y manos

`LMS_HumanRig` y `LMS_MosquitoRig`, con hueso `Root` sin deformación ni movimiento de raíz en clips. Importación inicial Generic; Humanoid es posible mediante mapeo explícito de Hips/Spine/Chest/Neck/Head, hombros, brazos, manos, piernas, pies y dedos, pendiente de validación Unity.

Sufijos `.L` y `.R` indican lados anatómicos. Cada mano tiene `Thumb`, `Index`, `Middle`, `Ring`, `Little`, con segmentos `01`, `02`, `03`. Las palmas apuntan hacia -Y en reposo. En ambos lados la flexión positiva del eje X local lleva los dedos hacia la palma. `FingerCurl` permite revisar apertura/cierre sin mover los brazos; la auditoría comprueba desplazamiento real del extremo del índice en ambos lados. Las manos se sueldan por voxel y se simplifican durante generación: no son cinco piezas superpuestas con la palma.

Sockets humano: `Socket.Eye`, `Socket.Head`, `Socket.Back`, `Socket.Grip.L`, `Socket.Grip.R`. Mosquito: `Socket.Mouth` en extremo de probóscide y `Socket.Back`. Se exportan como huesos no deformantes; no activar una opción de exportación que elimine huesos no deformantes. El eje longitudinal del socket es Y local. Presentation debe colocar herramientas según su eje de agarre y verificar ambas manos; la segunda entrega incluye el matamoscas con Socket.Grip y Socket.Impact.

## Materiales

Materiales originales, colores constantes, sin texturas externas. Humano: piel cálida, pijama azul, ribetes azul claro, suela oscura, ojos crema y expresión oscura. Mosquito: caparazón rojo oscuro, abdomen cálido, patas oscuras y alas frías. Materiales identificados por nombre estable permiten recolor base sin variantes geométricas.

URP Lit opaco para todas las superficies salvo `Mosquito_Wing`: fuente alpha 0,72, roughness 0,4 y doble cara por espesor geométrico; builder Unity usa alpha 0,42 Premultiply y Cull Back según W2, sin sombras en alas. FBX no garantiza conversión automática de nodos Blender a URP. Sin emisión, texturas normales, mapas externos ni efectos de picadura.

## Animación fuente

30 FPS, acciones independientes con prefijos `Human_` y `Mosquito_`. Todas en el sitio: locomoción y altura efectiva dependen de Gameplay. Listas y rangos reales en `audit.json`. La primera muestra incluyó 10 acciones humanas y 5 de mosquito. La segunda entrega completa 15 por personaje, incluidos Land/Hit/Faint/Recover/Swat y los estados de posado, superficie, picadura, desprendimiento y recuperación del mosquito. El catálogo e IDs exactos están en UNITY-INTEGRATION.md. Son clips iniciales para integrar y revisar, no evidencia de retargeting o contacto correcto en Unity. Mezclar Blink como capa facial; FingerCurl es herramienta de revisión. Crouch/Jump/Fall animan Hips, sin trasladar Root.

Loops declarados: Idle, Walk, Run, Fly, Hover, PerchIdle, SurfaceWalk y BiteLoop según especie. Las acciones restantes terminan una vez o se mantienen según estado del controlador. `Mosquito_Hit` sirve como inicio de caída; física y recuperación son responsabilidad del juego. El aleteo no es una simulación física.

## Reproducción y verificación

Ejecutar `art_source/unity/characters/build_characters.py` con Blender 5.2 LTS en background, previa coordinación CPU/GPU con Director. Genera `.blend`, FBX, auditorías y manifest en subdirectorios propios. No renderiza ni modifica `unity/Assets`. No requiere red ni proveedores pagos.

La auditoría mide triángulos, vértices, bounds, pesos normalizados, ausencia de vértices sin peso, triángulos degenerados, coordenadas no finitas, sockets y cierre de índices. Meta de presupuesto: humano < 20.000 triángulos y mosquito < 4.000, sujeto a los resultados medidos. Los archivos fuente conservan mallas, materiales, rig y acciones editables.

La inspección visual frente/perfil/espalda, mano cerrada y contacto de palmada fuente, así como la conversión FBX de ida y vuelta, están registradas en SAMPLE-REVIEW.md. Siguen pendientes la importación URP/rig en Unity, el apoyo de pies y contacto en juego, la primera persona con brazos/cuerpo y las pruebas de caída/posado. No convertir una auditoría geométrica en una aprobación visual de Unity.

El mapeo de anchors solicitado por Presentation está en `integration.json`. Renderers humanos: HumanBody, HumanHead, HumanNightcap, HandSkin.L y HandSkin.R. Mosquito: MosquitoSkin, MosquitoMembranes y MosquitoVeins. Sólo hay LOD0. La lista explícita de clips faltantes frente al contrato de Presentation acompaña el mapeo; no se inventa un estado a partir del nombre de una acción existente.
