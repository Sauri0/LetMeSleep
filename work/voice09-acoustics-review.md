# Acústica de voz: revisión acotada de autoridad y geometría

Resultado: **1186/1186 comprobaciones; exit 0; sin errores de Godot**. No se identificó un bloqueo funcional en las consultas examinadas. No se cambió código de producción.

- Fixture: `game/tests/voice09_acoustics_checks.gd`.
- Comando: `work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/voice09_acoustics_checks.gd`.
- Salida nativa: `work/voice09-acoustics.log`.
- Datos, huellas de mapas y testigos XYZ: `work/voice09-acoustics-results.json`.
- SHA256 de `voice_acoustics.gd` durante la prueba: `58800c497f7651fca8b1681a8b6f81c2d3365a07eb8aef007e67a541407c88c2`.

## Cobertura medida

| Mapa real | Nodos | Enlaces comprobados a +1,4 m | Puertas examinadas | Tramos que atraviesan estructuras |
|---|---:|---:|---:|---:|
| house | 94 | 126 | 10 | 0 |
| house-v1-1 | 188 | 189 | 23 | 0 |
| house-v1-2 | 120 | 120 | 17 | 0 |

Las cuatro direcciones humanas/mosquito —HH, HM, MM y MH— se ejercitan por separado. Los límites respectivos son 10, 10, 8 y 3 m; se comprueban posiciones al 10%, 50%, 90%, 100% y 101% del alcance, ganancia decreciente, fórmula por rol, altura de boca al agacharse, tono según el hablante y denegación de hablante u oyente eliminado. El propio límite tiene ganancia cero; el umbral mínimo de ganancia también puede excluir una franja inmediatamente interior, comportamiento explícito del módulo.

En las 50 puertas se consultan las cuatro direcciones de roles con hoja abierta y cerrada. Cerrar produce ganancia relativa 0,25 y corte de 1200 Hz, frente a 12000 Hz abierta. Se verifica reciprocidad geométrica manteniendo los roles del hablante y oyente. Una puerta cerrada permite voz amortiguada; no se interpretó como una prohibición completa de sonido.

Cada mapa incluye un testigo a ambos lados de un forjado real, con cuerpos de mosquito físicamente libres. La cota inferior para rodear la losa, calculada de sus dimensiones sin consultar el algoritmo de voz, supera 10 m; el módulo deniega ambos sentidos. Cada mapa también incluye una ruta corta por abertura con línea directa bloqueada: 5,6675 m, 7,4754 m y 7,4519 m, respectivamente. Todos sus segmentos tienen LOS estructural; reducir el presupuesto a un valor entre la distancia recta y el rodeo hace la fuente inaudible.

## Hipótesis descartadas y límites

`voice_acoustics.gd:34` utiliza el grafo elevado sin volver a comprobar LOS de cada enlace durante Dijkstra. Se inspeccionaron **todos los 435 enlaces** de los tres mapas: ninguno cruza paredes, suelo o techo. Por tanto, no se confirmó la hipótesis de un atajo a través de un piso. Este resultado cubre estos mapas concretos, no todas las semillas futuras ni demuestra que el grafo encuentre la ruta acústica continua más corta en cualquier posición.

La lectura vigente de `voice_transport.gd:65` confirma que `_request_control` llama a `_ensure_map` antes de abrir un stream. La hipótesis anterior de un primer stream con geometría todavía vacía quedó descartada en el flujo leído. La clase acústica requiere configuración previa; esta fixture utiliza mapas válidos, como la simulación admitida por el transporte.

Los 268,9 ms indicados por la salida abarcan configuración/generación, consultas y aserciones de toda la fixture; **no son una medida aislada de consultas, FPS ni coste del chat con 16 clientes**. No se ejecutaron micrófono, codificación, ENet, reproducción ni escucha, y no se infiere calidad audible de estos resultados. La presencia de una voz aturdida sigue la condición pública `alive`; aquí sólo se comprueba la eliminación, no se inventa una regla de silencio por aturdimiento.

Estado de coordinación: ventana headless liberada; ningún proceso permanece activo.
