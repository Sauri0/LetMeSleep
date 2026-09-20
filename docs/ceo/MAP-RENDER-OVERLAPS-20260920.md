# Solapamientos de render — primera búsqueda en cinco mapas

Corrección directa del usuario: revisar choques de texturas y parpadeos.
Se ejecutó una búsqueda geométrica sobre las mallas renderizadas de los cinco
prefabs de producción, en el checkout aislado4947f72. Ningún prefab cambió.

Evidencia: `N:/LetMeSleep/Validation/V020/RenderOverlap01/native-results-02/`.
Resumen y candidatos con posición/material: `RenderOverlap01/summary-native02.json`.
Unity terminó exit0; sin errores, mallas omitidas ni límite de comparaciones
alcanzado. Todos los detalles quedaron guardados en v2. Native01 se conserva,
pero sólo detallaba los primeros1000 pares por mapa y no sirve como ranking.

| Mapa | Triángulos examinados | Pares de triángulos sospechosos | Pares de objetos |
|---|---:|---:|---:|
| Isla |126601|1525|45|
| Casa |118120|5071|934|
| Campamento |76531|992|123|
| Yate |73813|2340|81|
| Puerto |175758|5054|171|

Son sospechas geométricas, **no cantidades de fallos visibles**. Se exige
orientación coincidente, distancia de plano≤0,1mm y área proyectada de
intersección>0,00001m². Recorte de triángulos comprobado con casos coincidente,
disjunto y adyacente. Revisión técnica independiente acepta la matemática
para triage, con límites explícitos.

El agrupamiento cuantizado de planos puede omitir parejas en fronteras.
Pueden estar incluidas caras ocultas interiores, intersecciones intencionales,
LODs no simultáneos y renderers ShadowsOnly/forceRenderingOff. No se resolvió
visibilidad, material/culling/transparencia ni profundidad desde una cámara.
Se omiten SkinnedMeshRenderer; agua animada necesita comparación temporal real.
No borrar caras automáticamente a partir de este inventario.

## Puntos para comprobación en movimiento

- Puerto: suelo/fundación de taller y casas; piso del faro/terreno. El ensayo
  dirigido PuertoCoplanar01 encuentra render y collider coplanares50/50 rayos
  de dos parches0,20×0,20m; no es aún observación temporal de parpadeo.
- Camp: unión Water_Creek/Water_Lake, malla de caminos y tablones de puentes.
- Isla: escalones de entrada/soportes de piedra y forros/paredes de la cabaña.
- Casa: encuentros techo/frontón y piso/cinturón de sellado, además de piezas
  pequeñas coincidentes. Distinguir caras interiores no visibles.
- Yate: cubiertas de teca/mamparos y barandas; distinguir uniones ocultas.

MapFlickerMotion01 prepara primero dos clips dirigidos: faro de Puerto y agua
de Camp, acercamiento/giro con iluminación y agua reales. Los otros tres mapas
siguen pendientes de revisión en movimiento. No equivalen a recorridos completos.

### Captura dirigida posterior

`MapFlickerMotion01/Run-20260920-174637-208` completó dos secuencias de48
frames720p y sus MP4, con fuentes intactas. CEO inspeccionó los frames0/16/32/47
de cada secuencia. No se afirma haber revisado visualmente todos los frames.

Puerto muestra bandas alternantes de terreno/piso y bordes dentados en16/32/47:
defecto visual de superposición confirmado en ese punto. Frame0 está tapado por
pared y la trayectoria diagnóstica atraviesa la pared; no es un recorrido jugable.
La corrección propuesta retira las caras de terreno duplicadas bajo la huella
del piso, preservando apoyo y borde, antes de repetir A/B físico y visual.

Camp muestra el entorno y agua, pero el primer plano de terreno tapa parte del
testigo: cobertura visual insuficiente para decidir si ese solapamiento produce
un defecto. No editar agua ni certificar ausencia a partir de esta toma.

## Otros hallazgos visuales separados

CasaLightingAudit01 confirma9 point lights con sombras→54 caras en atlas2048,
reducidas×4 por URP. No atribuir automáticamente el parpadeo a z-fighting ni el
tiempo de frame exclusivamente a luces. Experimento A/B propuesto aún no ejecutado.

CharacterMotionAudit01 confirma caídas humanas por clips y ragdoll mosquito
R4 fuera de producción; humano65 y mosquito39 huesos no acreditan deformación.
HumanArmPose01 prepara medición de extensión de brazos sobre prefab real.
Rediseño UI en implementación, todavía sin revisión visual de su resultado.
