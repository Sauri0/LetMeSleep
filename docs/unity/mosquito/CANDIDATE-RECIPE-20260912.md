# Mosquito: fuente modular y receta de candidato

Estado: SOURCE_ONLY. Rama `codex/unity-mosquito-specialist`, base `c888d96`. No se ejecutaron Blender, Unity, render ni auditoría de assets durante esta entrega. Los `.blend`/FBX/audit existentes siguen siendo el candidato anterior. No hay aprobación artística ni de movimiento nuevo.

Se leyeron TEAM-RECOVERY-20260912, CHARACTER-QUALITY-BAR y UI-ENVIRONMENT-QUALITY-BAR. Se abrieron con view_image las ocho referencias originales de `N:/LetMeSleep/References/CharacterQuality-20260912`, más silhouette7 Mosquito_Idle_35/90. Se incorporaron M1–M5 del informe independiente `N:/LetMeSleep/Validation/TeamRecovery/visual/FIRST-FOUR-SAMPLES-20260912.md`. Referencias 03/04/05/07 fijan silueta, planos y alas; 01/06/08 fijan coherencia de acabado del elenco. Sus etiquetas no amplían contenido.

## Diseño implementado en la fuente

| Brecha | Cambio y evidencia todavía necesaria |
|---|---|
| Tórax bajo y línea horizontal | Centro del hueso pasa de Z .055 a .110 m fuente. Techo de carapacho con planos amplios; frente más baja que dorso. Ver perfiles 90/270 y tres cuartos con encuadre comparable. |
| Cabeza/collar esféricos | Cráneo por secciones longitudinales de sien/mejilla/frente, cejas de carapacho con espesor. Ojos facetados encastrados; no se usa elipsoide para cráneo ni collar. Revisar unión ojo/borde, nariz y perfil bajo luz neutra. |
| Probóscide poco descendente | Base [0,-.096,.092] hasta la misma punta [0,-.190,0] fuente: 44.384° de descenso frente a unos 25° anteriores. Se preserva el alcance de gameplay. La punta sigue 57 mm sobre el plano de apoyo en bind; no se alarga escondiendo un cambio de socket. |
| Abdomen como cola | Transición proximal, segmentos con cambios de volumen/material y punta más inclinada. Extremo Y .204 frente a .235 anterior. No se alargó. Revisar de espalda y ambos perfiles; segmentación no aprobada por existir más secciones. |
| Patas superpuestas | Seis cadenas de tres huesos con femur/tibia más largos y rodillas/tarsos escalonados lateralmente. Tarsos finales X absolutos .119/.106/.093 m fuente, todos Z -.1126. Se conserva el plano Z -.114 y el socket GroundContact. El ancho de apoyos cambió, sin cambiar colisión ni escala. Ver frente/espalda y caminar completo. |
| Alas planas/celestes | Membrana lanceolada de 0.45 mm fuente, tres vértices de cresta y nervadura fina. Conserva nombre Mosquito_Wing, alpha .42 y separación Membranes/Veins. Blender y URP deben demostrar transmisión sobre fondo contrastado. |

## Interfaz acordada con Humanos

Humanos transfirió la extracción inicial y conserva los compartidos. Este especialista posee:

- `art_source/unity/characters/author_mosquito_geometry.py`: `create_mosquito(*, Character, material, tube, ellipsoid, strip, mesh) -> Character` ligado, con `c.contact`, sin exportar ni animar.
- `art_source/unity/characters/author_mosquito_motion.py`: `mosquito(c) -> None`. Importa dentro de la función `Pose`, `sampled`, `smooth`, `TAU` desde `author_motion`; así el wrapper puede delegar sin importación circular durante carga.
- `build_mosquito_candidate.py`: entry point exclusivo de especie. Importa helpers de `build_characters` sin ejecutar su main, llama geometría/movimiento y exporta sólo `mosquito/`. No modifica manifiesto común ni exports humanos/herramienta.
- `check_mosquito_source.py`: checks livianos sin bpy. `audit_mosquito_candidate.py`: auditoría Blender pendiente de ejecución.

Conexión a cargo de Humanos/Director: wrapper compartido invoca `create_mosquito(...)`, `author_mosquito_motion.mosquito(c)`, `c.export()`. No copiar la función vieja de author_motion encima de este módulo. No regenerar ambas especies accidentalmente. Esta entrega no toca build_characters.py, author_motion.py, auditores compartidos, manifest, runtime ni Unity assets.

Se conservan 33 nombres de hueso previstos, 15 nombres/duraciones de clips y los siete sockets en sus coordenadas de bind anteriores. Root es [0,0,0], escala Unity .5, colisión .055 m, Root apoyado .057 m, Mouth Unity [0,0,.095] y GroundContact [0,-.057,0]. El chequeo liviano compara los siete sockets contra el audit del asset anterior, no contra una copia de sí mismo.

## Movimiento y coordinación con Presentación

Fly/Hover tienen flexión y pose diferentes. Las alas se reflejan en coordenadas del rig, sin asumir que sus ejes locales son iguales. Perch/Detach extienden/recogen patas y cambian batido; SurfaceWalk mantiene dos trípodes. BiteStart/BiteLoop/Bite retienen Head/Thorax/Proboscis en bind y concentran alimentación en abdomen para no desplazar Mouth. Fall/Recover proponen contacto desde mínimo de malla evaluada y trasladan Thorax, nunca Root. Todo esto describe código: faltan evaluación Blender y clips nativos completos.

`c.contact.surface_walk` publica unidad, escala aplicada una vez, clip, frames/FPS, duración, stride y duty. Stride .026 m fuente, duty .65, ciclo 31 frames a 30 FPS (1 s): desplazamiento compatible por ciclo `.026 / .65 * .5 = .020 m Unity`. La división por duty es necesaria porque stride mide sólo la fase de apoyo. Presentation recibió la discrepancia con `MosquitoStrideMeters=.3` de runtime como hipótesis de patinaje. Su dueño debe separar el ciclo de superficie del vuelo y validar MotionPhase/velocidad nativos; no se modificó runtime desde este worktree.

## Comprobación realizada

`python -B art_source/unity/characters/check_mosquito_source.py`: PASS. Recibo `SOURCE-CHECK-20260912.json`, hashes de las cinco fuentes. Comprueba sintaxis, siete mallas custom cerradas/con orientación coherente/sin triángulos de área cero, constantes de sockets, 601 fases analíticas por pata, al menos tres objetivos apoyados y velocidad compatible con .020 m/ciclo. Margen mínimo de alcance: .005588 m fuente. Durante preparación detectó y corrigió alcance insuficiente en patas 1/3 y orientación de caras laterales en las cejas.

No se simularon armature modifiers, pesos reales exportados, transparencia, deformaciones, poses intermedias ni importación. Este PASS no es PASS artístico ni de animación.

## Receta para el turno del Director

Solicitar primero turno CPU Blender de dos hilos, sin render. No iniciarlo por transcurso del tiempo. Ejecutar cada comando secuencialmente desde `N:/LetMeSleep/Worktrees/mosquito`; logs en `N:/LetMeSleep/Worktrees/mosquito/work/mosquito-candidate/`.

```powershell
& 'N:/Blender/blender.exe' --background --factory-startup --threads 2 --python-exit-code 1 --python 'N:/LetMeSleep/Worktrees/mosquito/art_source/unity/characters/build_mosquito_candidate.py'
& 'N:/Blender/blender.exe' --background --factory-startup --threads 2 --python-exit-code 1 --python 'N:/LetMeSleep/Worktrees/mosquito/art_source/unity/characters/audit_mosquito_candidate.py'
& 'N:/Blender/blender.exe' --background --factory-startup --threads 2 --python-exit-code 1 --python 'N:/LetMeSleep/Worktrees/mosquito/art_source/unity/characters/audit_surface_support.py'
```

Primera salida: `.blend`, `.fbx`, `audit.json`, `candidate.json`, exclusivamente bajo `art_source/unity/characters/mosquito/`. La segunda evalúa todos los frames de los 15 clips en ambos formatos, Root/sockets/punta/gait/apoyos/loops/empalmes, y escribe `mosquito/candidate_motion_audit.json`. La tercera aplica el auditor compartido de soporte ya existente a esta especie para piso/pared/techo, escribe `surface_support_audit.json`. Usar su resultado como evidencia específica, no regenerar el motion_audit común de ambas especies en paralelo.

Si falla un gate, conservar log/identidad, corregir sólo fuentes propias y repetir lo afectado dentro del turno autorizado. Antes de devolverlo, registrar PID/salida y ausencia de proceso propio. Entregar hashes de fuente y de outputs; el Director sella manifiesto/importa Unity y los revisores cambian la identidad de su evidencia.

Render es otro turno explícito. Solicitar seis vistas 0/35/90/180/270/325, rostro, patas y alas sobre bandas claras/oscuras, neutral reproducible, y repetición del encuadre silhouette7. Para aceptar movimiento: clips completos Fly/Hover, PerchEnter/SurfaceWalk/Detach, Bite sobre piel, Hit/Fall/Recover; mostrar contactos y transiciones a velocidad real. Poses estáticas ni comparaciones numéricas sustituyen estos videos.

## Pendientes reales

Blender/FBX, transparencia, colisión visual tras elevar silueta, apoyo del modelo real y ausencia de saltos en IK/cabeza/alas, trayectoria de caída en runtime, fase de locomoción y picadura sobre piel no verificados. La altura visual cambió sin tocar radio/escalado; Gameplay/Presentation deben medir contacto con la malla nueva. Aceptación de arte/animación por revisores y Branko pendiente. No publicar ni avanzar a beta.
