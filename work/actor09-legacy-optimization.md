# Evitar regenerar geometría humana oculta

El perfil de presentación encontró 7,36 ms/frame en los cuatro humanos, con 4,80 ms/frame residuales dentro de pose después de excluir colliders. La lectura de ActorView mostró escrituras de `CapsuleMesh.height` en torso y ocho segmentos que `_hide_legacy_geometry` ya había ocultado. Esa cifra de perfil incluía también skin y overhead; no se adjudicó por completo a las cápsulas.

El cambio de producción se limita a `game/scripts/actor_view.gd`: omite escribir height cuando el MeshInstance3D tiene `visible=false`, y evita reescribir un height idéntico cuando sí está visible. Se siguen calculando las mismas transformaciones, manos, socket y colliders. La rama de caché refresca dimensiones de mallas de respaldo que se hayan revelado sin recibir un snapshot nuevo. Se usa visibilidad local, de modo que un respaldo visible bajo un padre oculto conserva sus dimensiones preparadas.

La referencia `game/tests/actor09_legacy_reference.gd` es una copia exacta del ActorView previo, salvo la primera línea `class_name ActorView`. El fixture reconstruye esa línea y verifica SHA-256 `90bf7163a767f6ccfc0223aae625e7541dfe61f49ede67e8fb0ca473f37f6a0c`. El código candidato es `7a519204a2acdb11a77623bfabf38475593876d3318a17211955a24ee60390a4`.

`actor09_legacy_geometry_test.gd` pasó **49.778/49.778 checks**, nativo Compatibility, exit 0, stderr vacío, proceso de 3,75 s. Son 72 snapshots sintéticos de presentación con las seis herramientas, agachado, marcha, yaw/pitch, gestos, golpes y lanzamientos, más casos de respaldo revelado con caché y bajo padre oculto. Ambos actores usan el mismo GLB de producción y el mismo CharacterSkin actual; los dos controladores faciales reales reciben semilla inicial .375, y luego avanzan sin resets. Se comparan pose compartida exacta, transformaciones de todo el árbol, huesos, morphs visibles, bounds de mallas visibles, clase/dimensiones de ray shapes, agarre y entrada pública intacta. La prueba no simula encuentros de autoridad ni modifica reglas.

Dos bloques ABBA, cada ejecución con 48 llamadas cambiantes a `_apply_human_pose` y 12 frames intermedios:

| Orden | Versión | CPU media por llamada |
|---|---|---:|
| A | Referencia | 0,9835 ms |
| B | Guardia | 0,4052 ms |
| B | Guardia | 0,5220 ms |
| A | Referencia | 1,0191 ms |
| A | Referencia | 0,8427 ms |
| B | Guardia | 0,4826 ms |
| B | Guardia | 0,4544 ms |
| A | Referencia | 0,9846 ms |

En total, 192 llamadas por versión: **0,9575 → 0,4660 ms**, reducción de CPU del método de **51,3%** en este replay. Los eventos `changed` de las nueve mallas procedurales ocultas pasaron de **1.728 a 0**. Los dos actores comparten también la guardia de primera persona añadida por Root en Skin, por lo que esa optimización no explica la diferencia A/B.

El tiempo hasta frame_post_draw se conserva en JSON, pero incluye scheduling/render y no se atribuye exclusivamente a GPU. La fixture no tiene la escena completa de 16 actores y no mide una reducción global de FPS ni explica todo el coste humano del perfil. Root mide posteriormente la partida completa. No se bajó población, frecuencia, calidad, radio ni precisión de contactos.

Evidencia final: `work/actor09-legacy-abba-fixed.json/.log/.err/.run.json`. El primer intento `actor09-legacy-abba.*` queda preservado como inválido: comparaba fases faciales derivadas de instance_id distintos e intentaba consultar morphs de CapsuleMesh. Se corrigieron sólo esos controles experimentales; no se omitió la comparación facial ni se relajaron límites.

Para el pack, los dos actores se precargan desde scripts incluidos bajo `game/tests`/`game/scripts`; no dependen de `work`. Tras esta corrida se añadieron guardias `FileAccess.file_exists` únicamente a la lectura de hashes, a pedido de Root: si el export omite el `.gd`, declara `reference_source_hash_verified=false` y `unavailable_in_pack`, sin contar esa comprobación como PASS. La geometría/colliders y el replay se siguen comprobando. La ejecución real dentro del EXE queda a cargo del gate final de Root; no se afirma que ya haya ocurrido.

Archivos para integrar: `game/scripts/actor_view.gd`, `game/tests/actor09_legacy_reference.gd`, `game/tests/actor09_legacy_geometry_test.gd` y este informe. No cambios a Pose, Simulation, colisiones autoritativas, GLB o materiales por este trabajo.
