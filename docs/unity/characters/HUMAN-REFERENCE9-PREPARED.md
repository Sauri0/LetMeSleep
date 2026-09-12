# Human reference9 — SOURCE_ONLY

Revisión de la muestra real `b110965` y de `Validation/TeamRecovery/visual/HUMAN-REFERENCE8-REVIEW-20260912.md`. **No generada ni renderizada.** Los .blend/FBX actuales y los seis PNG reference8 siguen siendo el candidato anterior; sus hashes no acreditan esta fuente modificada.

Cambios por hallazgo:

- HR8-1: anillos de soporte facial interpolados sobre planos comunes de mandíbula/pómulo/sien; puente/punta nasal menos afilados, con bisel; abertura labial curva, cavidad que coincide con el borde y pesos mezclados.
- HR8-2: esfera ocular más profunda y enterrada, contorno orbital completo entre globo y cara; se reemplaza la tira superior aislada. Se conservan huesos Eye/Brow.
- HR8-3: cuello con estrechamiento y base ensanchada, más cubierto por collar elevado; mangas/rodillas con transición de sección gradual y pliegue localizado; asiento de pantalón continuo detrás del bajo y cobertura del encuentro de muslos. Mismos sockets/rig axial.
- HR8-4: dedos separados y afinados por falange, base del pulgar más ancha e integrada a palma; menor densidad de triángulos y sombreado por facetas. El agarre con Socket.Grip conservado sigue necesitando medición con la herramienta real.
- HR8-5: contorno de pantufla de 12 puntos con talón/antepié distintos y punta curva, empeine convexo, abertura interior más profunda y suela biselada. Se amplía el encuadre y añade vista superior de abertura.

Preparación comprobada sin Blender: todos los scripts parsean; `git diff --check`; ejecución matemática del módulo de geometría con callbacks sin bpy: 567 polígonos de cabeza/cavidad/prendas/calzado con índices válidos, coordenadas finitas y área Newell no nula. Esto no verifica teselado FBX, skinning, contacto ni aceptación visual.

## Próximo turno, sólo tras concesión del Director

Directorio de trabajo `N:/LetMeSleep/Worktrees/characters`. Blender `N:/Blender/blender.exe`, `--background --factory-startup --threads 2 --python-exit-code 1`; un proceso a la vez, prioridad BelowNormal, stdout/stderr y PID registrados.

1. `--python art_source/unity/characters/build_characters.py -- --species Human`. Verificar hashes intactos de mosquito/matamoscas y comparar nuevos binds contra snapshot anterior; no ejecutar main sin selección.
2. `--python art_source/unity/characters/render_human_witness.py -- --output-name human-reference9 --views front profile_left back profile_right three_quarter face_front face_profile face_three_quarter hand_open hand_back hand_profile hand_curl slipper slipper_opening collar`. Abrir las capturas; registrar defectos y decidir qué geometría queda elegida antes de certificar auditorías.
3. Sobre la fuente elegida: `--python art_source/unity/characters/verify_fbx.py`, luego `--python art_source/unity/characters/audit_motion.py -- --all-frames`. Esos auditores leen las otras especies existentes, no las generan ni guardan.
4. Python normal: `art_source/unity/characters/check_motion_audit.py`. Fallos permanecen abiertos; no usar PASS anterior. El sellado general requiere otras evidencias y no se ejecuta sólo por estos checks.

La auditoría preparada añade cada frame entero y subframe Clap 23,5; localización de máximo estiramiento por malla/arista/fase/pesos; Jaw relativo a Head en Idle/Hit/Fall/Faint/Recover incluyendo todo vértice con peso Jaw >0,001; posiciones de falanges en espacio Hand y Swat con brazo girado. No incluye la herramienta ni prueba visualmente costuras/contacto; no declara esos aspectos aprobados.

Después: video completo de FingerCurl/Swat/Clap/Jaw y resto de acciones, agarre con matamoscas real, importación Unity/rutas Animator y revisión independiente del mismo hash. Fuente/FBX, imágenes fijas, clips y Unity son evidencias distintas. Aceptación de Branko pendiente.

## Coordinación

Director volvió a reservar Unity para UI/living/luz/exterior después del primer turno humano. No hay nuevo turno concedido para reference9 ni procesos propios activos. Mosquitos entregó `11a2cc9` SOURCE_ONLY con módulos de geometry/motion; su nueva geometría debe prevalecer sobre la extracción inicial de `b110965`. Su dueño valida el candidato antes de conectar `author_mosquito_motion.mosquito(c)` en el wrapper común. Humanos no modifica ese módulo ni sus binarios.
