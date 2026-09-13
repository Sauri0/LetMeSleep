# Candidato humano: cierre completo con caras visibles

Entrega aislada `art_source/unity/characters/candidates/human-blink-winding1`; canónicos no modificados por este trabajador. Fuente de orientación `2f2f115`; baseline joints3 `98f238c`.

Las imágenes Unity de Director `FaceBlinkAB/capture-20260913-010507-950` y el static bake `010943-664` muestran cierre100 con la mitad inferior del ojo abierta. El FBX tenía60 caras inferiores por ojo orientadas hacia dentro en todas las formas; el auditor anterior con rayos de doble cara no detectaba su desaparición con culling. Normales espurias de mandíbula y este defecto de winding son causas separadas.

La reparación orienta la lámina por el centro del ojo después de recalcular normales generales. Invierte exactamente120 caras por archivo, conserva240 caras oculares hacia fuera en cinco formas y ahora exige que los25 rayos frontales por ojo encuentren piel frontal al cierre completo. No usa material de doble cara.

| Archivo candidato | SHA256 |
|---|---|
| human/LMS_Human_alpha.blend |`918ddcf802ef761cf89bf20bedcec4019de8d8adc4bb765c2e5c7361e3f862a1`|
| human/LMS_Human_alpha.fbx |`2473a8e6420dcb7c15d58c6dcb0e9fc64769bc8af6636844c71851a42bd84b66`|
| menu/LMS_HumanMenu.blend |`665ff9c53429ae49346a370c1b57884bdaab9a4871f838b016e2fb04a2773ab2`|
| menu/LMS_HumanMenu.fbx |`44738cda7b3cba4baa7b4e0b68e40acc1eda30a0bed701a153ebd0348aa3678c`|

Native PID31512 salió0 a01:19:26.318UTC,16.218s, CPU2 BelowNormal, dos exports y auditor facial. PID8092 previo salió1 antes de exportar: la comprobación esperaba reverse literal y Blender conserva otro primer vértice; se corrigió para admitir el mismo reverse por rotación cíclica. Ambos logs conservados, procesos cerrados y slot devuelto a Director. No render propio.

`winding_repair.json` compara antes/después dentro de Blender: vértices, grupos/pesos, shapes, materiales, rig y curvas de cada acción idénticos por hash; sólo cambian120 órdenes de cara. Los dos `fbx-preservation.json` lo contrastan en los FBX exportados: posiciones, índices/deltas de formas, skin/transforms de bind, propiedades de modelos/huesos, payloads de curvas y asignación de materiales idénticos. Única topología distinta:120 caras de HeadAuthoredPlanes, con los mismos índices y orden invertido. Normales se actualizan conforme a esa orientación.

`fbx-winding.json`:0 caras oculares hacia dentro en ambos exports,240caras×5formas cada uno. `menu/facial_audit.json`: nueve cierres, source-FBX morph fidelity, globo sin cambios y25/25 rayos de piel frontal por ojo en cierre completo. Estos resultados no son aprobación visual ni de animación del golpe.

Reproducir sólo con slot Director: ejecutar `repair_human_lid_winding.py --baseline-root <candidates/human-menu-joints3> --output-root <directorio nuevo>` mediante el runner CPU2. Después ejecutar el auditor standalone `audit_fbx_eyelid_winding.py --require-outward` y `compare_fbx_winding_repair.py` sobre ambas parejas. Snapshot de scripts y hashes completo en manifest. Los metadatos anteriores de menú conservan la procedencia de sus clips; la reparación no los reautoró.

Para Unity: Director integra el postprocessor d4a51ef, copia cada candidato correspondiente, reimporta y recertifica contratos por API. El script externo HumanFaceImportDifferential.cs incluido ya está fijado al nuevo SHA base. Repetir normal probe en base y menú, más A/B con la misma cámara/luz a cierre0/.25/1 y giro35°. Comprobar cierre completo y apariencia natural antes de cerrar el defecto; sigue pendiente evaluación gráfica tras el cambio de winding.

Actualización de revisión externa: `N:/LetMeSleep/Validation/TeamRecovery/visual/FaceBlinkAB/WINDING-FULL-CLOSURE-REVIEW.md` compara seis vistas012911 con011323. Acepta de forma acotada cobertura inferior: quiet100 y turned35-100 cierran ambos ojos sin esclerótica/pupila visible, sin reaparición de triángulos de mandíbula. Párpados demasiado abultados y pliegue poco definido siguen pendientes estéticos, junto con fluidez/runtime. Sólo base2473a8e revisada, no menú44738cda. Los hashes de buffers BakeMesh cambian con el nuevo import; esta revisión no prueba igualdad de orden/splits, y la preservación fuente/FBX arriba no debe confundirse con igualdad de buffers importados.
