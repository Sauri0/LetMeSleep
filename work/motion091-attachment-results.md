# Corrección del salto al comenzar una picadura

Entrega incremental sobre `f1796c2`. Consume el contrato del Director
`4987609` (cherry-pick local `036c91e`), que publica `attached_to` únicamente
durante la picadura activa. El cambio del contrato pertenece al Director.

## Comportamiento final

El humano conserva el mismo filtro posicional antes, durante y después de
una picadura. Después de actualizar todos los actores, Client llama a
`ActorView.align_attachments`: cada mosquito activo recibe el desplazamiento
render de su humano, identificado por `attached_to`. No hay inferencias por
distancia, zonas privadas, historial de reserva ni dependencia del orden
de los actores recibidos.

La pose de contacto y su normal siguen siendo las de autoridad. La
traslación visual común se aplica al humano, el insecto y sus colliders.
La cámara y el punto usado para visibilidad de la marca del mosquito también
usan esa posición visual. La copia para el barrido de cámara es local:
el snapshot y los comandos/LOS de autoridad no se modifican.

Si el host falta, no es válido, no está vivo o el insecto deja de picar,
no se reutiliza un desplazamiento anterior. Los humanos superpuestos se
resuelven por ID explícito, sin aproximación geométrica.

## Evidencia nativa

Godot 4.5.2 / OpenGL Compatibility / RTX 3060 Ti. Import incremental limpio.
Cuatro ejecuciones finales con código 0, stderr vacío y sin timeout:

| Prueba | Checks | Fallos | Evidencia |
|---|---:|---:|---|
| motion091_attachment_test | 781 | 0 | motion091-attachment-r1.json |
| motion091_presentation_test | 4013 | 0 | motion091-presentation-r3attachment.json |
| camera_turn_checks | 6 | 0 | motion091-camera-r3attachment.run.json |
| manual_defense_test | 6897 | 0 | motion091-manual-r2attachment.run.json |

Total: 11697 checks, sin repetir las pruebas de contrato/privacidad que QA
ya ejecutó para el cambio del Director.

- Caso anterior de entrada `bitten`: corrección extra de raíz **.1053907 m
  -> 0 m**. El frame vuelve a avanzar .0368719 m, igual al filtro basal.
- Escenario de 72 frames con dos humanos y dos insectos: error máximo de
  cámara añadido **.000000504 m**; contacto relativo **.000000238 m**;
  colliders compartidos **.000000481 m**; marca real **.000000030 m**.
- Se probaron actores en orden inverso, humanos superpuestos con historial
  render diferente, cambio de host, entrada/salida, host eliminado, ID 0 e
  ID de tipo incorrecto. No se modifica el diccionario público.
- `World.show_assignment` se ejecutó realmente durante movimiento/picadura:
  marca visible y alineada. El test de cámara real conserva giro de 360°,
  marcha mirando abajo, agacharse, recentrado y palmada efectiva al insecto.

La invariancia que se verifica es relativa: si `d` es el desplazamiento
visual del humano, `insecto_render = insecto_autoridad + d` y
`ojo_render = ojo_autoridad + d`; por ello el vector de puntería entre ambos
permanece idéntico. No se afirma que los colliders render estén en `data.p`
absoluto mientras el desplazamiento basal está interpolado.

## Aviso intermitente de cierre, conservado

`motion091-camera-r2attachment` pasó las seis aserciones y salió con código
0, pero registró 241 bytes de stderr: ObjectDB instances leaked y 12
resources still in use. **No cuenta como ejecución limpia.** La repetición
única `r3attachment`, con el mismo código, tuvo stderr vacío. Eso no demuestra
que se haya resuelto la causa del aviso anterior.

Se inspeccionó el teardown de `camera_turn_checks.gd`: llama a `_leave()`,
espera seis process frames, encola la liberación del app y termina tras otro
process frame. Hay temporizadores/tween de UI, pero el stderr resumido no
identifica los recursos retenidos. No se modificó el fixture por conjetura,
no se cambiaron aserciones ni se ocultó stderr. Una investigación adicional
necesita salida verbose con los objetos concretos; queda para validación
integral coordinada por el Director.

## Límites

Esta entrega elimina el salto traslacional añadido por la entrada a la
picadura. Conserva las limitaciones previas de la interpolación posicional
general y del cambio de articulación al entrar en estados críticos; no
reclama haber resuelto toda animación/arte ni rendimiento/WAN. Las seis
capturas del tramo anterior siguen siendo evidencia histórica del recorrido
local; no se generaron nuevas capturas en este turno de corrección.

Motor liberado al terminar, sin procesos Godot/Blender pendientes.
