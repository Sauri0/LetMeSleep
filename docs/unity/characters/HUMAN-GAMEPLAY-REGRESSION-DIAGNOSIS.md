# Human joints3: diagnóstico de regresiones reportadas en juego

Referencia reproducible: central `e9d15e7`, fuente humana de `98f238c`. Lectura de código y datos FBX; ninguna nueva ejecución Blender/Unity de este trabajador. Las capturas estáticas anteriores y los 37 poses del menú **no aceptan** estos casos de gameplay.

## Resultado y responsables

| Caso real | Evidencia causal o hipótesis delimitada | Próxima corrección/comprobación |
|---|---|---|
| Al pegar la muñeca se estira hacia la visión | `ActorVisualBinding.cs:395` mueve sólo `hand.position` al extremo del collider de antebrazo después de Animator. UpperArm/LowerArm conservan la animación independiente. La piel interpolada LowerArm/Hand se estira con esa traslación. Mecanismo confirmado en código; aún sin medición del fallo en un frame real. | Gameplay trabaja IK de dos segmentos con longitudes del rig, objetivo limitado al alcance, codo estable y pose local de muñeca conservada. Medir longitudes, offset local Hand y articulación/malla durante todo el golpe. |
| Al pegar agachado el humano se levanta solo | `SelectMotion:325` prioriza Strike12 sobre Crouch3; `PlayTemporary` también impone Swat12. Controller tiene una Base Layer sin máscara. `author_motion.py:159` inicia Swat con `base()` de pie, mientras Crouch baja Hips `.42 m`. No se trata aquí de separación geométrica. | Gameplay conserva Crouch y aplica gesto de brazo por IK durante preparación, golpe y recuperación. Verificar continuidad al comenzar/terminar la defensa y al cambiar crouch durante ella. |
| Parpadeo hace salir cosas en mentón/cachete | FBX sólo mueve párpados; Unity tampoco desplaza mentón/cachete en30 muestras. Sí introduce deltas de normales ajenos:127v fuera de ojos,95 con giro>1°, máximo mentón116.565° y cachete51.252°. Error objetivo de normales importadas; explica una deformación aparente por iluminación. | Fix Editor d4a51ef filtra deltas de normal/tangente fuera de los triángulos deformados por cada forma; mantiene normales animadas de párpados/anclas. Reimport y repetición de datos/visión pendientes de Director. No cambiar BlinkWeightPolicy. |
| Marcha/carrera/escaleras extrañas | Gait fuente planta el pie durante duty `.62/.48` y recorre `.20/.28 m`, respectivamente. Su avance compatible es `.322581/.583333 m/ciclo`; runtime calibra ambos a `1.2 m/ciclo`. La escala visual humana es1. Esto implica desajuste de cadencia/contacto en suelo plano. No se encontró IK de pies humano en los archivos Presentation/Gameplay consultados. | Calibrar ciclo según clip y fase autoritativa; medir suela contra suelo real, pendiente/escalón y velocidad. La geometría autorada sólo recibe un plano de apoyo, así que corregir cadencia por sí sola no resuelve escalones. |

## Separación fuente / adaptación de brazo

Cadena exacta en ambos lados: `Shoulder → UpperArm → LowerArm → Hand`; sin huesos intermedios. Los dedos y `Socket.Grip` son descendientes de Hand. Longitudes de bind fuente: UpperArm `.270185 m`, LowerArm `.230217 m`, alcance total `.500403 m`; Unity debe medirlas en la instancia real.

`GameplayActorProxy.cs:58` construye otro brazo con segmentos `.28/.28 m`, shoulder `y1.39-.57*crouch` y objetivo limitado a `.55 m`. `SetSegment:86` orienta correctamente la cápsula desde codo a muñeca, con centro cero y ejeY. Por ello no hay evidencia para culpar al cálculo del extremo por ignorar `CapsuleCollider.direction/center`: esta creación sí cumple esas suposiciones. La discrepancia es el uso de ese objetivo para trasladar sólo Hand sobre otro brazo animado.

La fuente joints3 mezcla los pesos de la unión muñeca/antebrazo y corrige el puño. No puede impedir que una escritura externa desplace arbitrariamente la mano. Gameplay recibió la cadena, medidas y recomendación de conservar `Hand.localPosition/localRotation`, resolver UpperArm/LowerArm con el codo autorado proyectado como pole y fallback anatómico si queda colineal. No escalar huesos ni copiar directamente los centros del brazo collider.

## Datos faciales nuevos

`inspect_fbx_morph_regions.py` carga únicamente el parser Python standalone de Blender; no inicia Blender, `bpy`, editor, render ni GPU. Sigue las conexiones Shape→BlendShapeChannel→BlendShape→Mesh y Skin→Cluster→Bone, y contrasta cada vértice móvil con `author_human_facial.eyelids`.

En ambos FBX:

- Ocho formas,156 entradas sparse por forma;130 vértices cambian posición más de `1e-7 m`,26 quedan anclados.
- Los130 vértices móviles por forma coinciden uno a uno con el párpado autorado y están influidos sólo por Head. No hay vértices de Jaw o mentón movidos por las formas exportadas.
- Su altura fuente de bind está entre `1.499822` y `1.616178 m`; a cierre completo entre `1.502407` y `1.612646 m`.
- Desplazamiento máximo en cierre completo: `70.810 mm`. La amplitud/formato del párpado todavía puede resultar visualmente abultado o cortar otras superficies. El confinamiento de deltas no aprueba su aspecto.
- Fuente canónica y FBX dentro de Assets tienen bytes idénticos en el commit reportado por el usuario; no se observó export viejo mezclado.

| Archivo en e9d15e7 | SHA256 |
|---|---|
| Human alpha, fuente e import | `9e85748b5ec38f57791bb7377dbe43bc6328792986837c7fdb260371f6a8d20b` |
| HumanMenu, fuente e import | `3412a9caaec030c3fe89d646fd063c8431ac3db38a9b76a8c49d8bc0d98470ff` |

`FacialContentBuilder` verifica nombres/ejes/bind, pero su comprobación de formas sólo requiere que existan por nombre. No mide las regiones afectadas en la malla de Unity. Importer tiene weld/optimización y cálculo de normales de blendshape; son variables a observar, no fallos demostrados. Comparar coordenadas y pesos, porque los índices pueden cambiar al importar.

## Prueba diferencial preparada para Director

Script externo: `art_source/unity/characters/diagnostics/gameplay-e9d15e7/HumanFaceImportDifferential.cs`. Es cuerpo de evaluación CLI; no se instala en Assets ni se invoca Unity desde este trabajador. Compilado con DLLs actuales y Unity6000.3.24f1:0errores/0advertencias.

1. Comprueba SHA256 y contrato facial; instancia prefab temporal oculto y desactiva Animator automático/renderers.
2. Lee `GetBlendShapeFrameVertices` de las ocho formas, conserva deltas de posición/normales, coordenadas, skin, frameWeight y región anatómica aproximada usando el bind de Head y ejes certificados.
3. Muestrea Idle0, Crouch.8 y Swat.48; en cada uno, cabeza quieta y `VisualAttentionRig` real con evaluación manual de mirada. Congela esa pose y aplica cierres0/.25/.5/.75/1.
4. `BakeMesh` mide30 casos: desplazamiento atribuible al cierre en mentón, cachete bajo y fuera del soporte de morph, con bounds en actor. Cada caso se compara con su propio cero de cierre, sin contar la rotación esperable de la cabeza como deformación de parpadeo.
5. Guarda JSON único en `diagnostics/gameplay-e9d15e7/unity-receipts`, destruye instancia y mesh en finally. Guard de4.2s entre operaciones marca `PARTIAL_QUEUE_REMAINDER`; sin aprobación visual implícita. Carga de asset/serialización no tienen timeout duro.

Si posiciones salen de la región autorada al importar, localizar remapeo/delta antes de cambiar source. Si sólo aparecen al activar cabeza/cuello, examinar bind/skin y escritores de transformación. Si posiciones y soporte permanecen correctos pero el defecto se ve, revisar normales, párpado/orbita y clipping gráfico. Repetir en primer/tercera persona con el mismo hash/build. No reemplazar estos pasos por métricas antiguas de Blender.

## Caída/desmayo físico solicitado

El rig65 contiene Hips/Spine/Chest/Neck/Head, brazos y piernas segmentados y pies adecuados para una articulación física de torso y extremidades sin renombrar el skeleton. Root y sockets son controles; dedos, pupilas, cejas y Jaw no necesitan convertirse en cuerpos de la cadena principal. La fuente actual sólo trae Fall/Faint/Recover animados: no contiene un ragdoll físico.

Gameplay/Director deben definir cuerpos/colliders sobre el rig instanciado, masas y límites articulares coherentes, exclusiones de colisión entre segmentos vecinos y transferencia Animator↔física↔Recover. El brazo `.28/.28` de las superficies de gameplay no debe copiarse como medida del rig real. Mantener un único escritor final de huesos durante cada estado. Este trabajador no implementó un sistema físico paralelo ni añadió colliders al FBX.

Contrato fuente ampliado: `HUMAN-RAGDOLL-SOURCE-CONTRACT.md`, propuesta17 cuerpos, anchors/jerarquía, exclusión de escritores y transición hacia/desde física.

## Resultado nuevo ejecutado por Director

`face-import-20260913-005206-783.json`:8shapes/30samples,0.219866s, Unity6000.3.24f1. Mesh importado3485 vértices,426 móviles por forma debido a separación de vértices durante importación,852 únicos y0 fuera de regiónocular. Todas las muestras tienen0 desplazamiento en mentón/cachete bajo/fuera del soporte. Esto descarta desplazamiento de esos vértices en estas poses concretas, no certifica ausencia de clipping gráfico.

`face-normals-20260913-005418-042.json`:4 niveles pareados L/R,0.003708s. En todos,127 registros fuera de ojos,95 giran>1°, máximos mentón116.565° y cachete51.252°. Ejemplo vertex1, altura1.356m: normal base(0,0,1), deltaL=deltaR=(0,1,-1), posición0; normal final sin normalizar(0,2,-1). El cambio aparece desde closure.25. La política de mezcla es correcta, pero cada forma introduce el mismo cambio ajeno que se suma entre ambos ojos.

Se verificó además que cada forma del FBX lleva156 entradas de normal y todos sus índices están en párpados, incluidos los26 anclados. La corrección ahora autorizada en Editor usa soporte topológico, sin tocar fuente FBX. Ver `HUMAN-BLINK-NORMALS-FIX.md`; commits56145f6 (checks/doc) y d4a51ef (postprocessor/helper/meta). Compilación offline0/0 y6checks; posterior reimportación y revisión gráfica pendientes.

## Evidencia y alcance pendiente

- `diagnostics/gameplay-e9d15e7/runtime-context.json`: fuente completa y SHA de nueve archivos centrales en el commit exacto, hashes de cuatro rutas FBX y hashes de módulos fuente humanos.
- `diagnostics/gameplay-e9d15e7/fbx-morph-regions.json`: vértices/deltas/skin de los dos FBX actuales y coincidencia con targets autorados.
- Script Python nuevo y script C# externo anterior; estos últimos son herramientas de diagnóstico, sin modificación de FBX canónico, prefab, escena, controller ni runtime.

Actualización posterior: Director reimportó normalfilter y Revisor Visual comparó seis pares. Aceptación acotada: desaparecen líneas/triángulos oscuros de mejilla/mandíbula, cierre0 idéntico. Sus capturas también expusieron las caras inferiores invertidas; corregidas en fuente2f2f115/candidato20054df. Revisión `FaceBlinkAB/WINDING-FULL-CLOSURE-REVIEW.md`: quiet100/turned35-100 cubren ambos ojos sin blanco/pupila y no reaparece el defecto de mandíbula. Sólo base2473a8e revisada; menú no extrapolado. Párpados abultados/pliegue poco definido y fluidez aún pendientes.

Pendientes: pulido estético y animación facial completa; validación del fix IK/crouch en movimiento con objeto; reautoría gait/escalones; implementación ragdoll. Propuestas `HUMAN-GAIT-SOURCE-PROPOSAL.md` y `HUMAN-RAGDOLL-SOURCE-CONTRACT.md` no cambian assets/runtime. Los hallazgos y cierres acotados no aprueban todos los defectos reportados por el usuario.
