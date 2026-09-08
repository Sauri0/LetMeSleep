# Rendimiento de la continuación — medición en curso

`perf09-current16-1080.json` y sus logs se conservan como **intento inválido**.
No sirven para comparar FPS, escenas ni geometría de la fuente actual.

El fixture iniciaba práctica (que ahora genera una casa), reiniciaba la
simulación con el mapa histórico `house` y publicaba sin reiniciar el estado
`playing` del cliente. `Client._snapshot` carga la geometría al comenzar una
ronda. La simulación y la escena renderizada podían pertenecer a mapas distintos.
La salida 0 sólo acreditó la terminación del proceso; faltaba esa invariancia.

`performance07_live.gd` ahora fija la semilla del preámbulo, permite elegir
`--map-id`, publica el escenario como una ronda nueva y rechaza diferencias
entre `World.current_map`, `Simulation.config.map_id` y el mapa solicitado.
Aplica las opciones diagnósticas después de cargar esa escena. El mapa generado
conserva su spawn válido; el histórico conserva el inicio del recorrido de pasillo.

El fixture corregido se ejecutó en Compatibility, RTX 3060 Ti / Ryzen 5600X,
textura 1920×1080, 16 actores y 12 segundos medidos tras dos de preparación.
Ambos procesos terminaron con salida 0 y stderr vacío; las tres identidades
de mapa coinciden. Los informes y registros son:

| Escenario | Informe | Cuadro p50 / p90 / p99 | Muestras sobre 16,67 ms |
|---|---|---|---|
| Casa histórica | `perf09-corrected-house16-1080.json` | 19,311 / 32,628 / 45,768 ms | 66,55 % |
| Casa generada, semilla 1 | `perf09-corrected-generated16-1080.json` | 30,213 / 65,776 / 180,696 ms | 76,35 % |

Los registros `release07-perf09-corrected-*.run.json` identifican argumentos,
ejecutable del motor, fechas y salida. Se trata de fuente, no del EXE entregado.
No hubo otra instancia de Godot ni consumidor intensivo de CPU durante ambas
mediciones. La ventana física es 999×562; la textura renderizada es 1920×1080.
Los recorridos son distintos y no sirven como comparación A/B de optimización.
El escenario de estrés ordena abrir/cerrar todas las puertas periódicamente.

Continúan pendientes la optimización y su comparación. No se certifica un objetivo de FPS
con perfiles ejecutados junto con otros consumidores intensivos de CPU/GPU.
Los tiempos inclusivos de funciones anidadas tampoco se suman como costes
independientes.
