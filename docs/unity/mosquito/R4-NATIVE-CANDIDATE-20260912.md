# R4 mosquito — entrega para integración controlada

**Candidato nativo generado y auditado; Unity y aceptación artística pendientes.** Director concedió CPU2 BelowNormal, máximo12min y hasta dos reparaciones menores. Se ejecutaron dos snapshots secuenciales, sin Unity ni canónicos durante la validación. El segundo terminó PASS a23:30:38UTC, unos6min30s desde la concesión. CIM terminal confirmó0Unity/Blender/ffmpeg y se devolvió el turno. Después, Director pidió el commit con fuente y R4 generado: se copiaron sólo seis outputs mosquito y tres PNG al worktree del especialista; la integración central corresponde al Director.

## Archivos exactos

Origen del paquete: `N:/LetMeSleep/Worktrees/mosquito/`. Copiar sólo las siguientes rutas, manteniendo los helpers compartidos y wrappers humanos actuales del Director:

Lista mecanizable de19archivos y SHA256: `R4-COPY-LIST-20260912.json`.

- Fuente mosquito: `art_source/unity/characters/author_mosquito_geometry.py`, `author_mosquito_motion.py`, `author_mosquito_face.py`.
- Runner/auditoría/QA: en el mismo directorio, `build_mosquito_candidate.py`, `audit_mosquito_candidate.py`, `check_mosquito_source.py`, `check_mosquito_face.py`, `render_mosquito_witness.py`, `run_mosquito_candidate.py`. El helper matemático `check_mosquito_audit_math.py` ya pertenece a la entrega R3 y no cambió.
- Los seis outputs de la tabla, bajo `art_source/unity/characters/mosquito/`.
- Evidencia seleccionada: `art_source/unity/characters/review/mosquito-facial-flight-r4/face_open.png`, `face_closed.png`, `body_three_quarter.png`, `witness.json`. Sólo3PNG versionados; los59 originales quedan fuera del repo.
- Documentación propia: `docs/unity/mosquito/R4-NATIVE-CANDIDATE-20260912.md`, `R4-NATIVE-VALIDATION-20260912.json`, contrato FACE-FLIGHT y recibos de fuente actualizados. R3 queda como registro histórico.

| Output | SHA256 |
|---|---|
| LMS_Mosquito_alpha.blend | `5c26bfd6a7625e127272cdc33b0252bdbc3b3613cb8af7bfaa96941175a5562c` |
| LMS_Mosquito_alpha.fbx | `2f73f7f08c3fba4191cc08b46fcf4547d5b3acc1a7e6ecf36c98974649ed8d06` |
| audit.json | `4569500a1bd98df89168c9eb69a1e922d316a48e5ccf970832d98619fc925039` |
| candidate.json | `b59cdd9037de5dbc99f4183b8b79afa6a52ac8189ab2933bd632aeaf928baedf` |
| candidate_motion_audit.json | `7f880e5ba9c37b0bcc97c9869eb3ef12810d3cc42656a53ebdaa2b754483fed5` |
| candidate_surface_support_audit.json | `dc5bd20486cce9face31ea69b844a0953d40ad0b5e664645c6b7179a9b75ea98` |

No copiar `build_characters.py`, `author_motion.py`, manifest, `surface_support_audit.json` del directorio raíz ni Human/Flyswatter. El snapshot contiene copias aisladas de los helpers con hashes de procedencia; no constituyen cambios para integrar. Los wrappers deben seguir delegando sólo mosquito a los módulos propios. La fuente nueva requiere el módulo `author_mosquito_face.py`; no mezclar fuente39huesos con FBX R3.

## Resultado comprobado

- 39 huesos (33 retenidos + 6 faciales), 15 clips, 3 renderers, 3588 triángulos y 1910 vértices. Cero errores de pesos/triángulos/no finitos. Párpados e interiores usan Mosquito_Shell, conservando el binding de personalización. Los 33 huesos previos conservan cabezas, colas y padres contra el audit de `2f6ef3e`; diferencia máxima cero.
- 894 frames corporales enteros, 48 marcadores de malla, 20 endpoints y 12 poses faciales por formato. PASS. Máxima diferencia de marcadores source/FBX: cuerpo 0.0000004936 m, cara 0.00000008025 m; endpoints 0.0000001184 m fuente. Los controles faciales mueven skin real sin mover Root/Mouth/cuerpo.
- 636 muestras de soporte transformado en piso/pared/techo, peor clearance −0.00086477 m Unity, dentro del umbral previo 1.5 mm. No es ejecución real de orientación/contacto de Gameplay.
- Fly/Hover: 13 frames/.4 s, 7.5 Hz, Root fijo. Excursión real máxima de marcadores de cada ala ≈ .303 m fuente Fly y .286 m Hover; pies Fly ≈ .047–.056 m y Hover ≈ .0024–.0027 m. Son desplazamientos de vértices respecto al inicio, no longitudes de huesos ni una medida de legibilidad a escala final.
- Vistas inspeccionadas: abierto, medio y cerrado frontal; abierto/cerrado perfil; dos extremos de mirada y guiño por lado; cuerpo35; Fly frames0/2/6 y Hover frame2. Las alas cambian de arriba a extendidas, el cuerpo/patas cambian sin trasladar Root; los ojos se desplazan y los caparazones ocultan el blanco al cerrar.

## Fallos preservados y reparados

`work/mosquito-candidate/r4-cpu2-01/`: auditorías PASS, pero la vista cerrada de perfil reveló blanco posterior. Se añadió la cubierta fija de Head, retraída.8mm respecto a los párpados, y el chequeo de cobertura por rayos de ambos perfiles. El primer inicio de Fly salió2 porque `--cycles` era ambiguo para Blender Cycles; el argumento propio se renombró `--loop-count`. No hubo procesos superpuestos; ambos cambios quedaron dentro de las dos reparaciones concedidas. No se relajaron gates.

`work/mosquito-candidate/r4-cpu2-02/`: lote completo PASS con59PNG. `runner-receipt.json` conserva PIDs, tiempos, comandos, hashes y comparación de canónicos sin cambios al terminar el runner. El recibo documental registra aparte la promoción posterior solicitada por Director.

## Secuencias y alcance pendiente

Secuencias nativas completas: `N:/LetMeSleep/Worktrees/mosquito/work/mosquito-candidate/r4-cpu2-02/art_source/unity/characters/review/mosquito-r4-fly/` y `mosquito-r4-hover/`,24PNG por clip, dos ciclos en.8s a30FPS.

Videos reproducibles: `N:/LetMeSleep/Worktrees/mosquito/work/mosquito-candidate/r4-cpu2-02/Fly-normal-speed-5-repetitions.mp4` y `Hover-normal-speed-5-repetitions.mp4`. Cada uno repite cinco veces sus24frames originales:4s,120frames,30FPS, velocidad1, sin interpolación. FFmpeg/libx264,384×384,yuv420p,CRF20, un hilo. No son cinco tomas independientes ni una captura del juego. El recibo guarda hashes y ffprobe.

Presentación tiene el contrato para su único VisualAttentionRig post-Animator. La auditoría certifica bases de Blender y su reimportación FBX, **no** los ejes Unity. Para el builder: forward se puede derivar en bind de Mouth−Root y up de Root−GroundContact; convertir direcciones al marco local importado. La conversión posicional fuente→actor contiene reflexión, por lo que un eje axial no se copia con el mismo signo sin verificar. Propuesta para comprobar en Unity: eje de cierre `Cross(upWorld,forwardWorld)` pasado a lid-local, Upper+90°/Lower−90°. Certificar cierre real con skin importado antes de darlo por válido.

Quedan abiertos: curvas Wing respecto al Animator del menú, percepción de aleteo a escala/cámara final y velocidad normal; driver facial continuo en todos los contextos; mirada/defensa humana sincronizada; apariencia guardada de ambas especies; blends, gameplay y red. No se modifica ni cierra el P1 anterior de patas cruzadas. No más variantes ni renders en este turno.
