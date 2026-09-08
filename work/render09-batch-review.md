# Diagnóstico estático de lotes — no integrar este candidato

Se probó `work/static07_batch_probe.gd` sin modificar producción, sobre la
casa generada de semilla 1 con tres cámaras fijas antes y después. El fixture
`game/tests/render09_batch_probe.gd` conserva 30 muestras por cámara, seis PNG
y `outputs/0.9-render-batch-probe/report.json`. Godot Compatibility, RTX 3060 Ti,
textura real 1920×1080; salida 0 y stderr vacío. No hay actores ni simulación:
estos tiempos no representan FPS de gameplay.

El candidato agrupa 392 instancias de moldura/barandilla en 128 MultiMesh.
Aunque disminuye el número de nodos renderizables, empeora los envíos y tiempos
medidos en los pasillos:

| Cámara | Draws antes / después | CPU render mediana antes / después |
|---|---|---|
| Habitación | 429 / 420 | 1,233 / 1,371 ms |
| Pasillo inferior | 1685 / 2680 | 3,233 / 4,287 ms |
| Pasillo superior | 1437 / 3503 | 2,817 / 5,263 ms |

Las imágenes tampoco son idénticas: 4,16 %, 1,30 % y 0,65 % de píxeles RGB
cambian respectivamente; esa métrica no identifica por sí sola una causa ni
aprueba la imagen. La visibilidad y selección de luces por AABB del conjunto
son una hipótesis para investigar si se retoma otro esquema de lotes.

**Se descarta integrar este candidato.** Las mallas, luces y construcción del
entorno de producción permanecen iguales. Reducir instancias de forma aislada
no demostró una mejora del render.
