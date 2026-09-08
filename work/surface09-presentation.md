# Presentación al recorrer una esquina convexa

## Defecto reproducido

El fixture usa SurfaceLocomotion y Arena reales sobre una caja finita, snapshots públicos a 20 Hz y ActorView a 60 Hz. En los seis recorridos originales, 34 de 576 cuadros colocaron la raíz presentada dentro del margen de viaje de 40 mm: la interpolación recta cortaba la polilínea que seguía la autoridad. La autoridad permaneció válida en todos sus ticks.

La primera evidencia `outputs/0.9-actor-corner/before` registra ese defecto de raíz. Su muestreo de huesos se hacía tras process_frame; los datos de penetración de patas de esa carpeta son preliminares. Se añadió espera post_draw para que las pruebas siguientes midan la malla del cuadro dibujado.

Con la ruta corregida, el defecto residual se reprodujo también después de post_draw: ocho cuadros, máximo 4,781 mm. En la primera cara eran 29 vértices de `leg2_b_r`, explícitamente sin apoyo. Al perder el borde finito, el fallback de retracción acercaba el tarso a la rodilla cruzando la cara vecina. Testigo completo: `outputs/0.9-actor-corner/foot-diagnostic/actor-corner09.json`, frame 60. No era un fallo del solver ni un contacto que se hubiera marcado como apoyado.

## Cambio acotado

`surface_presentation.gd` conserva una cola local de hasta 12 puntos. Entre planos públicos perpendiculares reconstruye la arista común y recorre ambos tramos con el mismo suavizado exponencial de posición. Sólo fusiona puntos del último tramo recto. La normal, dirección tangente y fase de marcha siguen el tramo presentado; el snapshot recibido permanece intacto. La fase pública modular se desenrolla hacia delante antes de interpolar, utilizando la distancia de la ruta para resolver vueltas completas entre muestras. El gate mantiene su zancada pública de 0,15 m alineada con SurfaceLocomotion. MosquitoPose sigue calculando la orientación, que conserva su interpolación de rotación.

Descarta la cola al despegar, al cambiar a una normal opuesta o no perpendicular, al recibir planos paralelos incompatibles o un salto de más de 0,35 m por muestra, y si se supera el límite de puntos. Esos casos se reinician en la posición recibida; no se reconstruyen rutas desconocidas. No añade campos de red, soporte privado, reglas, colisiones, posición de cámara ni cambios de Simulation.

CharacterSkin comprueba el repliegue sin apoyo con rayos desde la raíz de la pata hasta su extremo y hasta seis extremos reales de la malla deformada. El eje solo puede pasar libre mientras el espesor del tarso toca una cara. Si un rayo cruza geometría real, resuelve la suela importada contra esa cara. La pata continúa `supported=false` y `stance=false`: es una corrección de penetración, no un apoyo inventado. No cambia mallas ni medidas de locomoción/impacto. La suela usa hasta 16 iteraciones con salida al error menor de 10 µm; la opción con puños mostró que tres iteraciones no bastaban al inclinarse en la arista.

Los rayos parten del marco real del cuerpo interpolado. Cada pie usa la normal que devuelve su impacto, por lo que puede conservar la cara anterior mientras el cuerpo gira. La proyección oblicua no desplaza tangencialmente una punta ya plantada sobre esa misma cara.

El retorno del trípode ocupa ahora el 50% del ciclo, con el 50% restante plantado; su amplitud de apoyo sigue la distancia real, sin modificar la velocidad ni la fase autoritativa. El polo de flexión de rodilla se transporta desde el eje de reposo: la proyección anterior podía cambiar el lado de flexión al elevar la punta y obligar a la suela a levantar el tarso excesivamente. Se conservan los dos segmentos y sus longitudes.

## Reproducción

```text
Godot --path game --script res://tests/surface09_actor_corner_checks.gd -- --output=<directorio absoluto nuevo>
Godot --headless --path game --script res://tests/surface09_actor_corner_checks.gd -- --helper-only
```

El gate nativo verifica por defecto. `--diagnostic` desactiva sólo las aserciones de calidad para conservar una medición fallida; sus contadores de ejecución nunca significan que el defecto esté cerrado. `--first-face` limita una investigación a la primera cara. `--footwear=0|1|2` selecciona cada sección de patas. Sin render, sólo se permite el modo helper.

El helper ejercita las 24 parejas de normales adyacentes, movimiento oblicuo, tres cadencias (30/60/144 Hz), invariancia del snapshot y discontinuidades. El fixture nativo usa el solver continuo, comprueba la raíz, vértices y triángulos importados deformados después de post_draw y registra máximos de penetración, retraso y desplazamiento por cuadro.

## Alcance

Es una regresión preparada del recorrido por una caja finita, no una sesión humana, una prueba ENet ni un benchmark. No certifica todas las uniones de la casa ni rutas perdidas entre snapshots; las discontinuidades no reconstruibles se descartan. Las pruebas históricas de seis planos independientes no cubrían esta transición continua.

Una revisión independiente de Simulation detectó que la primera versión del helper interpolaba de forma lineal la fase pública al envolver TAU. La atribución inicial de un pico de 55 mm al gait anterior era incorrecta: esa versión nueva podía recorrer fases hacia atrás. Se corrigió el desenrollado y se añadieron regresiones de cruce de TAU con muestras a 10/20/60 Hz. El límite de 45 mm por cuadro del tarso se conserva; no se relaja para ocultar ese defecto.

La reproducción de la presentación lineal anterior sobre Skin actual está separada en `outputs/0.9-actor-corner/legacy-stride-current-skin`: no pretende ser el binario anterior. En la primera cara reproduce seis cuadros de corte de raíz y un máximo de tarso de 55,816 mm; sólo el modo del fixture sustituye el helper, nunca el juego.

## Resultado final ejecutado

- `surface09_actor_corner_checks.gd`: **18.776/18.776 por estilo**, tres procesos nativos, **56.328/56.328** en total. Dieciocho recorridos, 1.728 cuadros medidos, las seis normales y tres secciones reales de patas. Salida 0 y stderr vacío en los tres procesos.
- Ninguna penetración de raíz, núcleo ni tarsos. La malla se comprueba con vértices y recorte exacto de triángulos contra la caja, después de post_draw; umbral de interiores de triángulos: 50 µm.
- Máximos por cuadro: raíz **14,195 mm**, núcleo **29,111 mm**, tarso **36,240 mm**. El límite de 45 mm permanece. En la primera cara comparable, el tarso baja de **55,816 a 33,789 mm**.
- Retraso máximo respecto al snapshot actual: **40,572 mm**. Deslizamiento tangencial máximo de un pie que continúa plantado sobre la misma cara: **0,000239 mm**. El mínimo durante la arista es dos patas con soporte real; no se declara que las seis estén siempre apoyadas.
- `appendage09_visual_checks.gd`: **2.547/2.547** nativo, salida 0, stderr vacío. Conserva seis planos, tres secciones, apoyo, cuatro gestos, rig y Preview. El total anterior era 2.727 porque el número de asserts condicionales sobre el intervalo de apoyo depende de DUTY; no se eliminaron aserciones.
- `voice09_visual_checks.gd`: **552/552** nativo, salida 0 y stderr vacío. Sólo niveles sintéticos: no demuestra por sí mismo reproducción/Opus/voz online.

Evidencia final: `outputs/0.9-actor-corner/final-style0`, `final-style1` y `final-style2` (18 PNG y tres JSON); `outputs/0.9-appendage-p2-final`; `work/voice09-p2-final.json`. Los PNG muestran el cuadro crítico del giro con cámara exterior a la cara actual. Las capturas anteriores quedan como diagnóstico histórico con sus propios encuadres.

Hashes y métricas exactas: `work/surface09-presentation-manifest.json`. Runtime y fixtures congelados al terminar estas corridas; no hubo export/import ni modificación de GLB. No se repitió lógica de autoridad que esta tarea no cambió.
