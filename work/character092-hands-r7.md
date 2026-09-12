# Manos 0.9.2 — r7 validada

Corrección cerrada para integración conjunta con HumanPose/EmotePose de Worker 1.
El reposo dorsal original llevaba los dedos al lado incorrecto y el agarre
estiraba las falanges. Ahora doblan hacia la palma, mantienen sus longitudes
y abren durante la palmada; el pulgar usa un reposo relajado y oposición limitada.
La malla y sus pesos acompañan esas poses sin cruzar las herramientas comprobadas.

## Integración exacta

- Facial previo: 1e2f828 y 1cf4cad, independiente de esta entrega.
- Runtime Worker 1 usado: b138a99239ecf8e0571c5fbfb7e2704bf320ce9f
  (cherry-pick local 681ef5f), test e761261b25ea85ed0a6001ad933aaf5e9782e969
  (local 00f24bc). No se editó ninguno de sus archivos.
- human_pose.gd SHA256: 2af92676bf892b32512e21b3bad9f8649e4bf5ddcb4f34551a82028186b7d35b.
- emote_pose.gd SHA256: f77146428f5752ffcc3cf96423aed32e69ba61cfecfc88b3c26ad9fedc3c934f.
- Rig explícito LMS092.palm1. Director debe migrar la expectativa legacy
  LMS07.grip1 de v07_character_rig_checks, conservando sus comprobaciones de
  36 huesos, nombres y jerarquía.

Se conservan mano/muñeca, raíces proximales, longitudes, contacto/eje de
herramientas y centro de palma. La fuente modifica sólo human_core y pesos de
mano/antebrazo distal: reposo palmar, espesor local mínimo 0.70 y contorno de
hasta 2.5 mm, con 15+3 correcciones localizadas de pesos. IDs cosméticos,
rostros, prendas, UV y otras mallas permanecen exactamente iguales.

## Pruebas y evidencia

Ejecutado 2026-09-12, 01:18:30–01:20:05 UTC, Godot 4.5.2 / Blender 4.5.3.
Todos los procesos r7 terminaron exit 0, stderr 0, sin timeout.

| Comprobación | Resultado |
|---|---|
| Comparación con fuente facial sellada | 147/0; 2629 vértices desplazados, cero ediciones fuera de alcance |
| Error máximo old→new de longitud ósea | 1.268e-8 m |
| Manos importadas, ambos POV, 33 frames por caso | 36098/0 |
| Caras invertidas/colapsadas, 40 estados nativos | 0/0 sobre 11476 triángulos por estado |
| Cruces exactos mano/herramienta en agarre final | 0 en escoba, diario, raqueta, pantufla y matamoscas |
| Regresión independiente Worker 1 con piel importada | 64848/0 |
| Radio máximo de reserva del brazo | 0.5822365069 m |
| Error de contacto de palma Worker 1 | 3.726e-8 m |

GLB final SHA256: 0c26be1c356e4b03c259d371095e419dba9478b3228b2e889980abe5a68ff8d1.
El reporte nativo incluye SHA de CharacterSkin/pose y matrices de piel por estado.
Evidencia: character092-rest-r7.json, character092-worker-hand-r7.json,
character092-tool-contacts-r7.json y character092-rest-mesh-r7/character092-hands.json.
Los registros de procesos mantienen prefijo character091-hand-rest-*-r7.run.json.

Revisión visual funcional de seis capturas: diario completo en tercera persona
y ojo real, palmada derecha completa, palmada izquierda desde el ojo, pantufla
desde el ojo y detalle lateral del agarre. Mano/objeto permanecen unidos y la
palmada abre los dedos. Capturas seleccionadas junto al reporte nativo.

## Alcance y límites

La orientación de caras se compara con la cara fuente transformada por su
paleta real. El diagnóstico separado de normales suaves conserva hasta dos
caras discrepantes y el sombreado irregular de antebrazos/detalles macro sigue
visible; no se oculta cambiando luces, cámara, sombras ni materiales.
La revisión funcional se acepta para este cierre; no equivale a acabado de
primer plano ni elimina toda limitación cosmética de la malla anterior.

El contacto exacto cubre las cinco poses finales; no certifica colisiones
continuas, todos los posibles golpes/lanzamientos ni todas las combinaciones
cosméticas. La regresión de pose comprueba envolvente y autoridad de contacto.
No se midió aquí 1080p60/GTX1660Ti ni WAN; esta entrega no publica el juego.

## Reproducción

Exportador: Blender --background --python-exit-code 1 --python
art_source/export_presets/characters_selected_pipeline.py -- --species human.
Después importar game con Godot y ejecutar tests/character092_hands.gd con
--capture --full-mesh --audit-mesh --output=<carpeta absoluta>. Ejecutar
character092_tool_contacts.py en Blender sobre esa carpeta y
motion092_hand_test.gd con --report=<JSON absoluto>, renderer nativo.
La comparación de fuente usa character092_rest_check.py --baseline=<carpeta>
--output=<JSON>; baseline contiene human_lms06.glb y rig_contract.json del
commit facial 1e2f828, conservado localmente en work/character092-baseline.
