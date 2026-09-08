# Segundo nivel de atribución, casa generada 1

El 8 de septiembre de 2026, la fixture de fuente ejecutó Main/Client/Practice reales con 16 actores y 15 bots. Conservó mapa y fingerprint idénticos en autoridad y World. La preparación queda separada: Main/Client 849,5 ms, preámbulo 625,9 ms, navegación ya preparada 0,12 ms, inicio final 3,19 ms y publicación final 6,43 ms. La medición cubre 12 s de reloj tras 2 s de calentamiento.

Proceso 35728: 18,46 s totales, exit 0, sin timeout, failures 0, pila de spans correcta y stderr vacío. Se corrigió sólo la limpieza diferida de la fixture, no el runtime. No hubo otro Godot durante la ventana; el estado del consumer facial de CPU no se verificó. Es diagnóstico instrumentado, no comparación limpia de FPS respecto del pase anterior.

## Desglose de los 701 ticks

| Trabajo | ms por tick | Porcentaje de Simulation.step |
|---|---:|---:|
| Simulation.step inclusivo | 6,423 | 100% |
| Movimiento libre de los 12 mosquitos | 2,448 | 38,1% |
| DoorState.step | 1,574 | 24,5% |
| Concentración consultada dentro del tick | 1,006 | 15,7% |
| Residuo de _tick | 1,142 | 17,8% |
| Ayuda, incluso consultas internas | 0,145 | 2,3% |
| Otros hijos y envoltura de step | 0,108 | 1,7% |

Los renglones internos son hijos directos, sin sumar dos veces sus descendientes. El residuo incluye funciones estáticas Arena de movimiento humano, bucles no envueltos y registro del perfilador; no es una medición exacta de locomoción humana.

DoorState tuvo p90 4,211 ms, p99 6,949 ms y máximo 7,691 ms por tick. En los seis peores ticks de Simulation.step, las puertas consumieron 6,704–7,102 ms. El peor tick, número 774, contiene 15,277 ms de Simulation.step, 6,915 ms de puertas y 4,116 ms de movimiento libre. El escenario emite 136 órdenes directas de puertas y observó alguna puerta en movimiento en 120 de 259 frames; no representa el uso habitual de una sola puerta.

El movimiento libre sumó 8.412 llamadas (12 por tick); sólo 40,9 ms de sus 1.716,3 ms corresponden a separación contra humanos. El resto incluye Arena.step_mosquito y su colisión real de mapa/puertas. Al leer el código se observa que DoorCatalog.body_blocked revisa todas las hojas en cada intento por eje; todavía no se midió ese método estático aisladamente y no se atribuye todo el resto a él.

Practice.advance promedió 10,832 ms. Fuera de Simulation.step, las decisiones de bots consumieron 1,156 ms por tick; su seguimiento de rutas incluido, 0,891 ms. Hubo 166 reconstrucciones de ruta en 2.057 llamadas, con 548,77 ms acumulados en llamadas que reconstruyeron. Los callbacks síncronos y residuo de publicación sumaron 1,061 ms por tick. Las consultas privadas humanas dedicaron 341,75 ms a oportunidad de ataque, 103,05 ms a puertas y 24,56 ms a recogida durante toda la ventana.

## Cobertura y límites

No hubo picaduras, golpes, ayuda ni proyectiles activos en esta ventana; las llamadas de mantenimiento ociosas no permiten estimar el coste de un combate. El modo Sangre no ejecutó tareas. Todos los cuerpos sí ejecutaron física a 60 Hz. Hubo 259 frames observados y acumulación de hasta ocho ticks por frame; el histograma completo acompaña los datos.

La lectura identificó repetición concreta en DoorState.step: reconstrucción de cajas de todos los actores y SAT para actores lejos de una hoja, repetidos por subpaso, además de cápsulas humanas repetidas cuando una extremidad es candidata. Root autorizó una optimización separada con descarte conservador y una referencia diferencial inmutable. Este informe precede a ese cambio y no afirma todavía ahorro o equivalencia.

Evidencia: `perf09-profile-detail-house-v1-1.json`, `.log`, `.err`, `.run.json` y `perf09-profile-detail-parse.log`. Hashes de módulos y fixture están incluidos en el JSON; las filas por frame y la jerarquía permiten revisar cada testigo. El archivo de producción no fue instrumentado.
