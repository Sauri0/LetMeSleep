# Lote de integración de personajes alfa

Builder compilado contra las bibliotecas instaladas de Unity 6000.3.24f1, sin abrir otro editor. El Director ejecutó la revisión inicial y detectó la inversión de frente descrita abajo. El delta de orientación y la idempotencia todavía deben acreditarse con el nuevo recibo real del editor residente. No confundir `unity_compile.json` con ese recibo.

## Método para el Director

```csharp
LetMeSleep.Content.Characters.Editor.CharacterContentBuilder.BuildAndVerifyIdempotence();
```

También está en `Let Me Sleep > Content > Build Characters and Check Idempotence`. `BuildAll()` ejecuta una sola pasada. No se dispara automáticamente al importar scripts ni modifica escenas abiertas; usa escenas de preview propias y las cierra en finally. No requiere configuración de paquetes, tags, capas ni ajustes de proyecto adicionales. El Director conserva el turno de editor.

Salidas bajo `Assets/LetMeSleep/Content/Characters/`: Models, Materials, Controllers y Prefabs. Lee los FBX y auditorías desde `art_source/unity/characters/` del mismo repositorio. No necesita Blender para importar en Unity. Conserva los GUID existentes y escribe sólo cuando corresponde; la segunda pasada comprueba que los bytes generados permanezcan iguales.

El recibo `BuildReceipt.json` sólo marca success cuando pasan los gates. Ante error escribe el diagnóstico, registra `LMS_CHARACTER_BUILD_FAILED` y lanza la excepción. No tapa fallos con prefabs sustitutos. Marcadores esperados: `LMS_CHARACTER_BUILD_PASSED` y después `LMS_CHARACTER_IDEMPOTENCE_PASSED`.

## Prefabs y responsabilidades

| Prefab | Contenido |
|---|---|
| LMS_Human | Humano Generic completo, escala visual 1, cuerpo/manos/cabeza/gorro separados |
| LMS_Human_FirstPerson | Mismo rig; cabeza/gorro en ShadowsOnly para el dueño, cuerpo y manos visibles |
| LMS_Mosquito | Actor root 1; VisualRoot 0,5; raíz de malla en tórax |
| LMS_Flyswatter | Matamoscas de marco abierto y rejilla geométrica real, mango y grip; ancla Impact |

`CharacterView` pertenece al assembly `LetMeSleep.Content.Characters`; su editor está en `LetMeSleep.Content.Characters.Editor`, autorizado por Director. Presentation puede referenciar runtime sin depender del editor. No hay dependencia inversa ni referencias a Gameplay.

Los colliders de referencia de los personajes están **deshabilitados**: almacenan cápsula 0,25/1,72 y esfera 0,055, pero GameplayActorProxy crea los colliders vivos. No activar ambos conjuntos. Gameplay ajusta agachado, colisión y autoridad; el prefab no aplica movimiento, daño ni resultados.

Punta mosquito de reposo: `(0,0,+0,095)` m Unity desde la raíz, conforme al contrato W1. Se acortó y enderezó la probóscide fuente para coincidir sin desplazar el tórax ni cambiar el radio. Los clips de picadura mueven Head/Proboscis; Presentation debe alinear continuamente el ancla ProboscisTip con el contacto autoritativo de W1.

## API de Presentation

La jerarquía visual incluye `VisualRoot/SourceOrientation/Model`. `SourceOrientation` corrige el frente real importado mediante yaw, por fuera del Animator; conserva curvas, bindposes y transformaciones locales del rig. Presentation puede mover VisualRoot y no debe reiniciar el giro del hijo SourceOrientation. Los paths de huesos relativos al Animator se conservan.

- `Animator`, `VisualRoot`, `Anchors`, `Motions`, `HitVolume` son referencias serializadas.
- `GetAnchor(name)` devuelve el Transform; los nombres exactos y huesos fuente están en `integration.json`.
- `RefreshAnchors()` sigue los huesos. LateUpdate lo ejecuta con orden 1000, después de las poses de Presentation con orden normal. Si Presentation escribe huesos más tarde debe llamarlo después de su pose.
- `SetFirstPersonVisibility(bool)` alterna sólo cabeza/gorro entre On y ShadowsOnly. No desactiva el cuerpo ni objetos del rig.
- `SetSkinColor`, `SetPajamaColor`, `SetMosquitoColor` usan MaterialPropertyBlock por slot; no clonan materiales ni borran otras propiedades del bloque.
- `PlayMotion(id, crossFadeSeconds)` actualiza el entero Motion y hace CrossFade explícito. También se puede hacer CrossFade directo en Animator. No hay transiciones AnyState que interrumpan el estado elegido. El entero por sí solo no dispara una transición.

Los estados se llaman `Base Layer.<nombre>`. IDs estables, empezando en cero:

| Humano | Mosquito |
|---|---|
| 0 Idle | 0 Idle |
| 1 Walk | 1 Hover |
| 2 Run | 2 Fly |
| 3 Crouch | 3 Brake |
| 4 Jump | 4 PerchEnter |
| 5 Land | 5 PerchIdle |
| 6 Turn | 6 SurfaceWalk |
| 7 Clap | 7 BiteStart |
| 8 Hit | 8 BiteLoop |
| 9 Fall | 9 Detach |
| 10 Faint | 10 Hit |
| 11 Recover | 11 Fall |
| 12 Swat | 12 Recover |
| 13 Blink | 13 Land |
| 14 FingerCurl | 14 Bite |

Blink y FingerCurl son acciones diagnósticas completas; para parpadeo superpuesto Presentation necesita su propia capa/máscara facial. El catálogo real tiene 15 acciones por personaje. Los rangos y loops vienen de las auditorías fuente. No hay eventos de animación que confirmen impactos, extracción o desmayos.

`ToolView` expone ToolId `flyswatter`, Grip, Impact, HeadRadius y GripToImpact. El usuario de la herramienta coloca el prefab bajo ToolSocket_R y alinea Grip; W1 mantiene alcance y daño autoritativos. No se agrega MeshCollider a la rejilla.

## Gates incluidos en el builder

Avatar Generic válido, fuente de animación conservada, root motion desactivado, bindposes/bones completos, todas las curvas dirigidas a paths existentes, clips con duración esperada, mallas muestreadas sin coordenadas inválidas, Root sin traslación, discontinuidad de pies de loops <= 2 cm, materiales URP, cabeza ocultable, escala, altura de ojos, punta de mosquito y dimensiones de colliders. El recibo incluye paths reales de huesos/anclas, estados, GUIDs y digest de fuentes.

Los materiales de alas siguen la indicación de W2: alpha 0,42, Premultiply, Cull Back, sin recibir/proyectar sombras. Todavía corresponde comprobarlos sobre fondos claros/oscuros y durante el vuelo en Unity.

## Evidencia y límites de este lote

Fuentes Blender y FBX de humano, mosquito y matamoscas pasan auditoría y reimportación independiente. La fuente refuerza pesos de manga a Chest y de pantalón a Hips para cerrar las uniones durante poses. Compilación de ambos assemblies: cero errores y advertencias usando `compile_unity.py`.

Las capturas de `review/` pertenecen a la muestra anterior `48767e7`; no acreditan visualmente esta revisión de clips, pesos y probóscide. El manifest se sella con `--source-only` y mantiene esa distinción. No hay LOD1/LOD2: Director priorizó rig/manos funcionales y sólo permite decidir LOD tras medir.

Pendientes de cierre con Director/W2/QA: ejecución del builder y su repetición real, recibo validado, contacto de animaciones en motor, BodySurfaces/pose autoritativa, agarre y alcance de matamoscas, FP sin clipping y alas. No hay afirmación de FPS ni de prueba de partida.

## Corrección de orientación tras importación nativa

En Unity 6000.3.24f1 el Director midió antes del delta: mosquito ProboscisTip `(0,0,-0,095)`; humano Socket.Eye `(0,1,53,-0,17)`, UpperArm.L `(+0,25,1,17,0)`, UpperArm.R `(-0,25,1,17,0)`, Foot.L `(+0,125,0,12,0)` y Foot.R `(-0,125,0,12,0)`. Por eso el recibo inicial falló el gate de punta +Z. La ida y vuelta Blender/FBX no detectaba esta conversión específica del importador Unity.

El builder `alpha-characters-3-orientation` mide el frente a partir de puntos del rig importado y aplica sólo yaw en SourceOrientation: 180° para los valores observados. No mueve el socket respecto a la probóscide ni refleja la escala. Aplica el mismo tratamiento al humano completo, FP, mosquito y herramienta. Los nuevos gates exigen frente +Z, izquierda anatómica -X, derecha +X, pies humanos consistentes y distancia de la punta a un vértice real de la probóscide < 2 mm. El recibo incorpora los puntos medidos, el giro aplicado y esa distancia; la reejecución con Director debe verificar estos valores junto con las curvas y mallas de todos los clips.

La segunda ejecución nativa pasó los signos de orientación y llegó al gate de proximidad de malla, donde dio 0,04750128 m. El medidor usaba `BakeMesh(mesh)` seguido de TransformPoint: en la documentación instalada de Unity 6000.3.24f1, el argumento por defecto false incluye la escala del renderer en los vértices, de modo que el contenedor 0,5 se contaba dos veces. `alpha-characters-4-bake-scale` usa explícitamente `BakeMesh(mesh, true)` para compensar esa escala antes de TransformPoint. No cambia FBX, geometría, sockets ni transformaciones del prefab. El recibo marca `bakeMeshScaleCompensated`; el gate conserva la tolerancia de 2 mm y necesita nueva ejecución nativa.
