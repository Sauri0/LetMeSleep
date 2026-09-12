# Presupuesto de presentación alfa

## 1. Objetivo y límite de evidencia

Objetivo de referencia: Windows x64, 1920×1080, GTX 1660 Ti, Unity
6000.3.24f1, build Development, pantalla completa exclusiva o borderless
documentado, VSync desactivado y `Application.targetFrameRate=-1`.

Este documento fija gates provisionales. Sólo un run en ese equipo, sobre un
commit exacto y con la ronda alfa real, puede acreditarlos. Editor, unit tests,
capturas de otro GPU o pruebas antiguas de Godot no acreditan FPS Unity.

## 2. Presupuesto por frame

Para sostener 60 FPS, el frame completo dispone de `16.67 ms`. Presentation no
consume todo ese margen:

| Medida | Mediana | P95 | Bloqueo alfa |
|---|---:|---:|---:|
| Frame Time completo | <=13.5 ms | <=16.0 ms | P95 >16.67 ms |
| GPU Frame | <=10.5 ms | <=13.5 ms | P95 >15.0 ms |
| Main Thread | <=7.0 ms | <=10.0 ms | P95 >12.0 ms |
| Render Thread | <=3.0 ms | <=5.0 ms | P95 >7.0 ms |
| Animator total | <=1.2 ms | <=2.0 ms | P95 >3.0 ms |
| Presentation scripts | <=0.8 ms | <=1.5 ms | P95 >2.5 ms |
| Audio DSP CPU | <=8% | <=15% | P95 >20% |

Mediana deja margen para red, simulación y picos; el P95 evita aceptar una
escena que sólo alcanza 60 FPS en cuadros favorables. No se usa promedio como
único resultado.

## 3. Presupuesto de contenido visible

Gates iniciales de la escena casa/patio con 16 actores visibles en el peor
encuadre reproducible:

| Métrica | Objetivo | Límite de investigación |
|---|---:|---:|
| Batches | <=700 | >900 |
| SetPass calls | <=180 | >240 |
| Triángulos | <=1.2 M | >1.8 M |
| Vértices | <=1.8 M | >2.6 M |
| Shadow casters visibles | <=300 | >450 |
| Luces realtime con sombra | 1 | >2 |
| Luces adicionales por objeto | <=4 | >4 |
| Materiales visibles | <=44 | >64 |
| Transparencia en pantalla | <=12% | >20% sostenido |
| Audio Voices | <=48 | >64 |
| Audio Memory | <=96 MB | >128 MB |
| Texturas residentes | <=1.25 GB | >1.75 GB |
| Memoria GPU estimada | <=3.5 GB | >4.5 GB |

Un límite de investigación exige captura de Frame Debugger/Profiler y dueño del
costo. No habilita recortar hitboxes, alcance, superficies defendibles ni
eventos gameplay.

## 4. Presets medibles

### AlfaReference1080p, predeterminado

- Render scale 1.0, SMAA High, HDR, post FX de `URP-ALFA.md`.
- Main shadow 2048, 28 m, 2 cascadas, Soft Medium.
- SSAO downsampled activo.
- Reflection Probes horneados 128.
- LOD Bias 1.0; Maximum LOD Level 0.
- Texture Streaming activo; anisotropic `Per Texture`.

### AlfaPerformance1080p, respaldo manual

- Render scale 0.85, SMAA Medium, HDR.
- Main shadow 1024, 22 m, 1 cascade, Soft Low.
- SSAO desactivado.
- Reflection Probes horneados 64; sin mezcla en props pequeños.
- LOD Bias 0.75; Maximum LOD Level 0.
- Mismos colores, cámara, siluetas, animaciones gameplay y distancia de objetos
  interactuables.

El juego empieza sin cap de FPS en ambos presets. El menú puede ofrecer límites
y VSync, pero el test siempre registra sus valores. No hay cambio automático de
calidad durante una ronda alfa.

## 5. Matriz de escenas

Cada run mide 10 segundos de warm-up y 60 segundos útiles por caso:

| Caso | Cámara/acción | Riesgo principal |
|---|---|---|
| `house_room_human` | primera persona, giro 360°, cuerpo visible | piel, muebles, luces, clipping |
| `house_hall_doors` | dos puertas animadas y cruce de pasillo | overdraw, sombras, Animator |
| `house_stairs` | subir/bajar mirando barandas | z-fighting, probes, cámara |
| `patio_human_16` | 5 humanos + 11 mosquitos, cercas/árboles visibles | skins, alas, sombras |
| `patio_mosquito_16` | vuelo, frenado, pared/techo y cámara cercana | transparencias, camera collision |
| `blood_contact` | extracción, defensa, hit, caída y recuperación | VFX, audio voices, spikes |
| `lobby_16` | 16 avatares, giro y ready | Animator, materiales, UI integrada |

Semilla/roster, ruta de cámara, duración y acciones se congelan para comparar
commits. Cada caso se repite tres veces `A-B-A` cuando se evalúa una
optimización; se descarta el warm-up, no el run lento.

## 6. Evidencia por run

Registrar:

- Commit, branch, Unity/URP/package versions, scene GUID y content manifest.
- CPU, RAM, GPU, VRAM, driver, Windows, resolución, modo de pantalla, API,
  calidad, render scale, VSync y targetFrameRate.
- Profiler `.data`, capturas de CPU/GPU/Rendering/Memory/Audio y Player log.
- Mediana, P95, P99 y máximo de frame CPU/GPU; GC alloc/frame y picos GC.
- Batches, SetPass, tris, vertices, shadow casters, lights, Audio Voices,
  Audio Memory y DSP CPU.
- Tres capturas por caso: inicio, peor P95 y cierre.
- Hash de build y timestamps de inicio/fin.

Los módulos GPU no están disponibles en todas las APIs/hardware. Si Unity no
entrega `GPU Frame`, marcar `unavailable` y no inferirlo desde FPS. Repetir con
una API compatible antes de acreditar el gate GPU.

## 7. Orden de corrección

1. Corregir spikes, errores, allocations por frame y material instancing.
2. Limitar sombras adicionales, distancia/cascadas y casters diminutos.
3. Reducir transparencia/overdraw de alas, vidrio y follaje manteniendo
   siluetas.
4. Compartir materiales, habilitar SRP Batcher/instancing válido y corregir
   prefabs repetidos.
5. Ajustar LOD de ambiente/personajes sin ocultar mosquitos dentro del rango de
   juego.
6. Bajar SSAO y sombras con `AlfaPerformance1080p`.
7. Reducir render scale sólo en el preset de respaldo y volver a capturar
   legibilidad de alas, manos y objetos.

Cada cambio conserva un caso antes/después y explica el mecanismo. Una mejora
de FPS que altera colisión, alcance, velocidad, reglas de defensa o actor count
se rechaza.

Unity recomienda reducir cascadas, distancia, resolución, casters y calidad de
soft shadows cuando el costo de sombras domina; también recomienda evitar
shaders complejos y usar Depth Priming en PC según
[configuración URP para rendimiento](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/configure-for-better-performance.html)
y [optimización de sombras](https://docs.unity3d.com/6000.3/Documentation/Manual/shadows-optimization.html).
