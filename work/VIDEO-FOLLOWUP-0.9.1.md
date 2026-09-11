# Correcciones de la prueba del usuario — 11 de septiembre

Trabajo en `codex/0.9.1-video-polish`. La candidata descargable rc.1 se conserva
sin modificaciones; estos cambios todavía no forman parte de un nuevo ZIP.
Director integra runtime; Worker 1 revisó cámara/personajes y el diff de giro,
sin editar esos módulos ni ejecutar el motor simultáneamente.

## Evidencia de la grabación (44,95 segundos)

- 0 s: franjas blancas/marrones en techo; uniones verticales intermitentes.
- 2 s: inspección del propio cuerpo durante el recorrido.
- 8–12 s: marcos, rellanos y pasillos estrechos junto a las escaleras.
- 14–20 s: grandes habitaciones con pocos elementos y largos pasillos.
- 24–30 s: paso lateral junto al hueco y debajo de la escalera.
- 32–44 s: dormitorio vacío en gran parte, puerta y techo desde varios ángulos.

Los cuadros originales se guardaron localmente en `work/user-video-sep11`;
la grabación personal no se publica. El video no muestra el editor facial:
los hallazgos sobre ese editor provienen de código y pruebas, no del video.

## Cambios implementados y comprobados

1. Techo: el catálogo procedural ya contiene un sólido de techo; World
   añadía otro a la misma altura con material distinto. Se conserva el del
   catálogo con pintura de techo; la casa antigua mantiene su fallback.
   La regresión antes detectó área duplicada en ambas semillas y pasó en la
   casa antigua; después pasaron los tres mapas. Capturas nativas en tres
   ángulos confirmaron superficie uniforme. No se modificó la colisión.
2. Inspección al caminar: Arena transmite la magnitud saneada de movimiento;
   HumanPose permite al torso seguir la vista aunque se mire hacia abajo.
   La libertad de inspección en reposo pasa gradualmente según el pitch,
   evitando el salto al cruzar los umbrales. Worker 1 revisó el diff sin
   encontrar P1/P2. Defensa manual 6897/0; locomoción 7867/0; concentración
   y combate 145/0; caché de poses 4922/0. Falta comparación visual dinámica.
3. Editor facial: expresión neutral al comparar ojos, cejas y boca; vista
   general y gestos recuperan expresión animada. Tarjeta de ojos «Alertas»
   del mosquito diferenciada de «Redondos». UI nativa 137/137, incluidos
   canales faciales neutrales y restauración exacta de preferencias.

Una primera orden de captura pasó incorrectamente los argumentos de PowerShell
y abrió el menú. Se cerraron únicamente sus procesos identificados; la orden
corregida capturó las vistas, exit0 y stderr vacío. No se usa el intento fallido
como evidencia de validación.

## Pendientes de esta revisión solicitada

- Diagnóstico histórico de uniones (resuelto en la base del equipo; ver actualización al final): seguían visibles incluso
  tras corregir el techo. El diagnóstico detecta caras de base de marco
  coplanares con el piso, pero eso solo no prueba un conflicto visible:
  las normales opuestas pueden ocultar una cara. No recortar suelo a ciegas.
- Reestructurar la casa: recorridos, anchos útiles de escaleras y rellanos,
  habitaciones proporcionadas y mobiliario funcional; validar navegación,
  puertas, rutas, spawns y determinismo al cambiar el generador.
- C-01 de Worker: cámara con input actual frente a cuerpo/cabeza de snapshots;
  interpolar presentación sin separar hitboxes, marcas ni mosquitos adheridos.
- Más calidad y claridad de opciones faciales/modelados, revisión de perfil
  y movimientos. La corrección del editor no sustituye nuevas mallas.
- Revisar materiales/bordes y luz en movimiento; preservar estética caricatura.
- Medir de nuevo la versión terminada y exportar nueva candidata. La prueba
  WAN con amigo queda pendiente por elección del usuario hasta este pulido.

## Medición del EXE rc.1, anterior a estos cambios

1080p internos, RTX3060Ti/Ryzen5600X, 12 s por caso tras 2 s de calentamiento,
sin límite/VSync, un único proceso de motor. Evidencia `perf-rc09-sep11-final`.

| Caso | Frame p50/p90/p99 ms | >16,67 ms |
|---|---|---|
| 2 actores, sombras altas | 3,995 / 10,532 / 13,336 | 0,09% |
| 16 actores, sombras altas | 14,412 / 29,061 / 36,067 | 42,17% |
| 16 actores, sombras desactivadas | 7,912 / 13,596 / 18,447 | 2,22% |

Apunta a un coste importante de sombras, pero no es un replay idéntico: bots,
colisiones y posición final divergen entre corridas. No presentar la diferencia
como ahorro causal exacto ni quitar sombras como solución visual. Tampoco
certifica GTX1660Ti, multijugador WAN, combate intenso o todas las semillas.

## Base de superficies cerrada antes del equipo de siete puestos

Eliminadas caras duplicadas/interiores de paredes mediante unión visual, sin cambiar colisión. Las losas poseen las caras que ocultaban dinteles: se corrigieron las franjas de umbrales. Revestimientos cubren retornos menores a 12 cm y apoyan al muro sin separación. Los suelos húmedos son regiones de la losa, sin segunda capa superpuesta. Las molduras siguen cada planta y el intradós real: una moldura fija a 6.4 m afloraba en el tercer piso.

Capturas nativas locales de tres ángulos en work/user-video-sep11/storey-trim muestran resueltos los defectos investigados. No equivalen a auditar toda combinación de mapas o a medir rendimiento. Baseline: superficies 707/0, techo 3 mapas/0, marcos/uniones 992/0, juntas de casa 603/0, procesos exit 0. Prueba nueva de superficies incluida en build.ps1. Equipo activo y propiedad de archivos: TEAM-0.9.1.md. El paquete publicado rc.1 sigue intacto.
