# Cinco cámaras para revisión nativa de casa/patio

Receta derivada de `house_layout_plan.json`, `alfa_maps/build_sources.py`, `source_manifest.json`, `room_contract.json` y `AlfaMapBuilder.FurnishHouse()` en el commit 88d8ba2. No se abrió Unity ni se generaron imágenes. Las posiciones y criterios están en `ALFA-REVIEW-CAMERAS.json`, preparados para la captura automática del Director.

Todas las coordenadas son locales a la raíz HousePatio, en metros. Usar perspectiva 1920×1080, near=0.05 m, far=100 m y up=+Y; FOV **vertical** por cámara. Si la raíz tiene transformación, convertir tanto Position como LookAt con `mapRoot.TransformPoint` y up con `mapRoot.TransformDirection(Vector3.up)` antes de `Quaternion.LookRotation(target-position, up)`. No desplazar el mapa para encuadrarlo.

| Cámara | Position | LookAt | FOV | Qué debe resolver |
|---|---|---|---|---|
| Frente exterior | (18.5, 8, -14) | (6.4, 3.5, 4.5) | 55° | Encuentros fachada/lateral, techo continuo, ventanas y entrada |
| Patio | (6.4, 8.5, 22) | (6.4, 1.2, 14.3) | 65° | Sendero libre, apoyos de banco/pinos/cerca y unión salida-suelo |
| Escalera | (4.45, 4.75, 6.7) | (3.23, 1.75, 9.45) | 75° | Dos tramos, rellano, hueco de forjado, barandas y salida superior |
| Unión de puertas | (6.06, 1.53, 5.66) | (8.6, 1.1, 5.66) | 75° | Portal al descanso y puertas enfrentadas comedor/cocina |
| Cuarto interior A | (4.2, 4.53, 3.75) | (2.2, 3.7, 1.6) | 75° | Cama, mesita, escritorio/silla, circulación y pickup 1006 |

Las dos cámaras exteriores son cámaras de inspección fuera de PlayBounds; no son spawns ni posiciones que deba alcanzar el jugador. La escalera se observa desde el descanso superior elevado, mirando por el hueco real: desde abajo, el forjado oculta parte de la salida. La cámara de puertas pasa por HallEast0 y mira entre DiningDoor y KitchenDoor. La del cuarto se sitúa dentro del dormitorio, sin depender de que su puerta esté abierta.

Capturar primero con las nueve puertas cerradas. Para revisar hojas abiertas, reutilizar **la misma cámara 04** en una segunda captura con DiningDoor/KitchenDoor a 100° mediante GameplayDoor.ApplyAngle(100 * Mathf.Deg2Rad), que ya aplica OpenSign=-1; restaurar el estado al terminar y no guardar ese cambio en los prefabs. Esto revisa visualmente extremos del giro; la trayectoria intermedia necesita prueba en movimiento.

Mantener iluminación y materiales de la escena bajo revisión, sin ocultar paredes, quitar techo ni cambiar exposición entre capturas para disimular defectos. Desactivar gizmos/UI de depuración para la imagen final y registrar aparte commit, ContentHash nativo, estado de puertas, resolución y configuración de luz/exposición. El visual del pickup depende de W2: su ausencia en la escena de revisión sin presentación debe anotarse como pendiente de integración.

Los criterios de cada cámara figuran en el JSON. Un recorte, oclusión o zona ilegible se marca **no evaluable** y se ajusta la cámara de inspección registrando la posición final; no equivale a aprobado. Estas cinco vistas cubren encuentros y lectura espacial de puntos seleccionados. No prueban dimensiones exactas por píxeles, UV2 completo, toda la casa, colisiones, recorrido humano/mosquito, giros intermedios de puertas, sincronización ni rendimiento.
