# Revisión de las cinco capturas nativas de casa/patio

Evidencia del Director: `N:/LetMeSleep/Artifacts/review/alfa-map-0.png` a `alfa-map-4.png`, con las posiciones de ALFA-REVIEW-CAMERAS y rig de presentación real. Revisión de geometría visible y encuentros; no se abrió Unity ni se amplió la decoración.

| Vista | Observación | Acción |
|---|---|---|
| 0 — Frente | Hueco triangular entre la coronación de muros y la cara inferior de cubierta: se ven el ático y la cara superior del techo. El faldón de tejado tiene espesor, pero no cierra el hastial. Fachada, ventanas, esquina y entrada no muestran otros huecos evidentes. | Corregido en fuente con dos cierres triangulares sólidos, frontal y trasero; pendiente nueva captura. |
| 1 — Patio | Sendero continuo; banco, pinos y cerca quedan fuera del paso. Puerta abierta y unión suelo/fachada legibles. | Sin modificación de mobiliario ni circulación. El cierre trasero recibe la misma corrección de hastial. |
| 2 — Escalera | Dos tramos y rellano se leen continuos; hay hueco real de forjado. Guardas y postes visibles, sin grieta clara al exterior. Parte de los peldaños queda oculta por la baranda y la perspectiva. | Sin defecto concreto que justifique cambiar geometría. El recorrido y las dimensiones útiles no se certifican por esta imagen. |
| 3 — Puertas | Portal, marcos, hojas abiertas y suelo presentan encuentros continuos; no se observa un segundo shell ni abertura al exterior. Mancha intensa en techo corresponde a iluminación visible. | Sin cambio geométrico. |
| 4 — Dormitorio | Cama, mesita, escritorio/silla y matamoscas sobre superficie se leen apoyados y dentro del cuarto. Esquinas de pared/techo continuas. Mancha triangular clara detrás de cama no demuestra una grieta geométrica. | Sin cambio geométrico; no atribuir esa mancha a UV o topología sin evidencia adicional. |

Los materiales lisos no proporcionan un patrón para juzgar estiramiento UV; estas capturas tampoco muestran las islas UV2 ni una prueba completa de bake. La ausencia de agujeros visibles no demuestra por sí sola topología manifold. Esas propiedades se apoyan separadamente en validación de fuente e importación.

## Corrección acotada del hastial

Se añaden `Gable_Front` (z=0..0.18) y `Gable_Back` (z=11.22..11.40), con material existente Plaster_Warm, espesor real de 0.18 m y MeshCollider. Cada pieza tiene ocho triángulos. Su base está a y=6 y su vértice a y=8.04, exactamente bajo la cara inferior de cubierta. La intersección de la pendiente define x≈0.233636..12.566364 para la base; el alero y la cumbrera exterior y=8.20 se conservan.

`close_house_gables.py` modifica únicamente la fuente/FBX de casa y el manifest; `build_sources.py` reproduce ambos cierres en futuras generaciones. Lobby y kit no se reexportaron. No se cambiaron muros de habitaciones, puertas, spawns, escalera, huella ni límites globales de la casa.

Las piezas pasan manifold y volumen positivo; la validación completa de fuente y reimportación registra **819 checks / 0 fallos**. Fuente casa: 121 meshes / 4628 triángulos, dos meshes/colliders y 16 triángulos más que antes. El builder nativo comprueba presencia, límites exactos y collider de ambos hastiales. La nueva importación y la captura frontal final quedan pendientes del Director.
