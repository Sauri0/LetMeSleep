# Gate QA 0.9.2: accesos funcionales físicos

Estado: preparado estáticamente. No ejecutado hasta que la casa v3 y sus correcciones estén integradas en el árbol QA.

## Contrato

`game/tests/review092_functional_approach_test.gd` exige `Generator.VERSION == 3` y la identidad exacta `house-v3-<seed>`. Para cada habitación compara todos los `structures.kind == furniture` contra las aproximaciones: cada mueble debe aparecer exactamente una vez. Después resuelve `structure_id`, `zone_id`, el ancla finita de zona y el punto de acceso, y conduce un humano mediante `Arena.step_human` a 30 Hz por:

1. `room.center -> functional_zone.anchor -> furniture.approach`
2. `furniture.approach -> functional_zone.anchor -> room.center`

Todas las puertas están completamente abiertas. El gate exige consumir todos los puntos, terminar a no más de 0,12 m, conservar el piso, mantener un cuerpo válido y no producir saltos, teletransportes ni 90 ticks sin progreso. Un impacto del rayo conservador de puertas se considera diagnóstico; sólo el movimiento físico puede rechazar la ruta.

El corpus es `[1, 2, 7, 31, 97, 257, 997, 2026, 65537, 1234567, 2147483646]`. `--seed=<n>` permite un proceso acotado por semilla y evita contaminación entre caches.

## Negativos del arnés

- Un destino dentro del AABB de un mueble debe fallar como `end_blocked`.
- Un punto intermedio dentro del mueble debe fallar como `waypoint_blocked`.
- Un `zone_id` inexistente y un `origin` separado del ancla deben fallar antes de caminar.
- Quitar o duplicar la aproximación de un mueble debe fallar la cobertura uno a uno.
- Un ancla de zona no finita y un `structure_id` que no sea `furniture` deben rechazarse.
- Un presupuesto agotado debe terminar como `incomplete`; reducir la distancia no constituye éxito.
- La ejecución normal comprueba ambos sentidos y exige consumir los tres puntos.

Los negativos usan copias de metadata y puntos de muebles existentes. No alteran la casa generada, el catálogo, los fixtures ni el runtime.

## Ejecución pendiente

Cada proceso debe ejecutarse desde `game/` con el binario Godot 4.5.2 aprobado por Director. Ejemplo por semilla:

```text
Godot_v4.5.2-stable_win64_console.exe --headless --path . --script res://tests/review092_functional_approach_test.gd -- --seed=1 --report=res://../work/review092-functional-approach-seed-1.json
```

El resultado agregado debe conservar los JSON por semilla, códigos de salida, stderr y tiempos. Ningún resultado está reclamado en este documento.

## Límites

Este gate cubre autoridad local de movimiento y colisión en el acceso interior al mueble. No cubre renderer, aprobación visual, transición o bloqueo de puertas, entrada desde el pasillo, escaleras, red, predicción, controlador ni WAN. Esas superficies conservan sus gates independientes.
