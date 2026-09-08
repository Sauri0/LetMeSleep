# Checkpoint técnico WIP — NO candidato, NO entrega aprobada

Se preserva personalización independiente, protocolo8/DD4 y correcciones faciales en desarrollo antes de ampliar nuevas mecánicas autorizadas. EXE estándar todavía pertenece a19088e0, previo a cachetes/personalización; no se reemplaza ni se presenta como este código.

## Fallos conocidos al registrar
- Blink fuente conserva globo/pupila y rayos de autoría están verdes; smoke2 humano mostró blanco/iris porLOD generado en pose abierta. A/B nativo con detalle completo eliminó defecto. Política facial de cercanía y cejas sobre párpado ya escritas; smoke3 pendiente.
- Gate de cobertura importada aún abierto: bake coincide con autoría a0,12µm, BVH encuentra cobertura pero rutaGeometry3D/Physics del fixture difiere. No afirmarPASS ni deformar modelo para ocultar error del fixture.
- H0 corona mejoró con normales aplicadas a función correcta, raíces claras aún requieren diagnóstico.
- No catálogo completo ni285poses finales/EXE/perf/ZIP sobre este estado. Las capturas históricas mantienen hashes y revisiones separadas.

## Nuevo alcance explícito autorizado
Mosquito camina por piso/pared/techo; mapas con distribución/número de pisos/zonas variables, misma semilla+layout válido para todos; menú con mosquitoB real; emotes corporales; uso de personalización ampliada; voz espacial real para todoscercanos. Vozmosquito aguda sin acelerar, M→H más baja/corta, M→M mejor inteligibilidad, humano normal. PTT configurablepor defecto; no abrir micrófono en automatización ni sinacción explícita deljugador.

Root conservaGit/build/client/network/practice/generación/voz; Sim locomociónsuperficies simulation/arena/mosquito_pose; UI menú/módulos nuevos sin tocar AvatarPreview mientras reproduzcasmoke; Visual Skin/GLB/rig/emotes después cierre facial. Los contratos work/surface09-scope.md,ui09-scope.md,generation09-scope.md y Director0.7-cierre contienen límites/aceptación, no funcionalidad implementada.

Este commit permite desarrollo paralelo disjunto con fallos identificados; no habilita publicación ni certificaWAN/60FPS.
