# Integración 0.7 — estado de trabajo

Actualizado el 8 de septiembre de 2026. Esta nota no certifica una versión publicada.

Decisión posterior del usuario: **LISO** para paredes y suelo, conservando paletas por habitación y materiales propios de los objetos. Humano A y mosquito B siguen elegidos. Se autoriza continuar desde `50dec0d` con propagación acotada, verificación visual, rendimiento del arte integrado, matriz final de red desde EXE y distribución al pasar. Los apartados de elección pendiente de abajo describen el cierre histórico de la tanda anterior. Evidencia nueva en `outputs/0.7-liso/`; cierre final en `outputs/0.7-validacion/`. Conexión WAN por relay/EOS y certificación GTX 1660 Ti permanecen sin implementar/verificar respectivamente.

## Decisiones y entrega de muestras

El usuario eligió **humano A compacto y mosquito B alargado**. Se mantienen pijama, pantuflas, gorro y personalización. La elección de proporciones no aprueba sobreexposición, sombras defectuosas ni uniones pendientes. El acabado liso/sutil del módulo sigue sin elegir; esto no bloquea personajes e iluminación.

Muestras editables entregadas en `C:/Users/brank/Documents/Codex/2026-09-06/realtime-voice-chat/outputs/muestras-3d-07/`. ZIP `Let-me-sleep-muestras-3D-editables.zip`: 40.222.081 bytes, 132 archivos, SHA-256 `756402db540e3b8176a17a7405ed37c27cd647cdda61fe9137d9406c744f6ed7`. Incluye cuatro personajes Blender/GLB, seis caras con diez morphs cada una, generadores, shader original, visor Godot independiente, dos módulos Blender/GLB/texturas, fichas, vistas y clips. CRC y hashes internos comprobados. La carpeta extraída se importó desde cero y abrió con Godot Compatibility sin errores. El visor permite A/B o la combinación elegida, caras, expresiones, parpadeo y despiece.

La tanda de muestras está cerrada. No reabrirla por el contraste de cornisa o la trama leve del panel de puerta: ambos límites quedaron documentados. La iluminación actual de World se conserva en imágenes diagnósticas, no como acabado aprobado.

## Integración de esta tanda

- Humano A y mosquito B integrados en producción, con seis caras y diez controles por cara. Malla y superficies físicas comparten postura. Malla 378/378, cliente 86/86, actores 411/411 y envolvente facial 67056/67056 en la validación nativa.
- Postura del antebrazo corregida: máximo 72,8711° dentro del límite conservado de 75°, cero giro de torso siguiendo la marca durante 2 s. Movimiento 11396/11396. Ver `work/forearm-a-yaw-comparison.md`.
- MosquitoPose comparte orientación y cápsulas de tórax/cabeza/abdomen con impacto. Se conservan navegación de 0,04 m, hueco de puerta de 0,14 m, puntería manual, ventanas de golpe, aturdimiento de 35 s y ayuda. Ver `work/selected-impact07-report.md`.
- Piel corregida en origen sRGB→lineal. Iluminación continua con 23 focos con sombra, sin los 16 rellenos de ventana sin sombra. Control cualitativo de oclusión por puerta y capturas con materiales de producción. Persisten bordes duros y facetas; no se aplicó el acabado liso/sutil pendiente.
- El build rechaza stderr de Godot aunque el proceso devuelva cero. Se corrigió el cierre del fixture de combate por red para esperar evidencia pública/privada fiable del último tick: 34/34, cero pendientes, sin modificar el runtime de red ni relajar la auditoría.
- Evidencia actual en `outputs/0.7-integracion/`: clip de expresiones de 8 s, caras, contacto manual, comparaciones y ficha. `work/package_integration07.py` sólo consolida el informe y copia el EXE tras un build completo exitoso. Consultar `INFORME-FINAL.md` y `CIERRE.json` cuando existan para el cierre verificable.

## Rendimiento y candidato

FPS ilimitados y VSync desactivado por defecto para perfiles nuevos; las elecciones guardadas se respetan. Verificación de preferencias: 13/13; comprobación gráfica nativa: 23/23. Commit `155d721`.

Decisiones de bots escalonadas a 20 Hz, controles y simulación a 60 Hz; navegación precalculada y rutas válidas conservadas. Scheduler 242/242 y práctica 79/79. Commit `c5e80ff`. En una comparación limpia con arte idéntico, el scheduler redujo p50/p90/p99 de 13,649/19,852/30,703 ms a 9,849/16,076/24,691 ms. El pase combinado posterior fue 11,546/17,142/26,034 ms: no se atribuye una mejora de FPS adicional a la caché de rutas. Ver `work/cpu07-report.md` para límites y causas. No se ha demostrado 60 FPS sostenidos ni rendimiento en GTX 1660 Ti.

El EXE candidato anterior se conserva en `outputs/0.7-candidate-before-live-cpu/`, SHA-256 `EBE089424F0E06FE97973019EEA59778493C2CB8955E90E7ECA1FF51D9CB430C`. No contiene esta integración ni se publica como final. La entrega pública 0.6 permanece intacta.

## Cierre pendiente

La orden vigente es cerrar esta tanda de integración, luz y regresiones, dejando un informe y evidencia actuales; no ampliar arte ni optimización. El acabado liso/sutil sigue pendiente de elección. Para el cierre posterior de 0.7 quedan rendimiento con población real y arte integrado, matriz completa de red desde el EXE, demostración del EXE definitivo y cierre de distribución/publicación. No marcar 0.7 completa por terminar esta tanda. Los borradores de README, distribución y publicación aún no certifican el estado final.
