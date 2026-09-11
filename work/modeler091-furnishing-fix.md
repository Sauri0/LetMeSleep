# Corrección de mobiliario — semilla 221853394

El corpus de Director detectó `room-13` con dos muebles, en una casa de dos
plantas y 17 habitaciones. El dormitorio azul mide 5.0 × 3.85 m. La búsqueda
probaba un único perímetro a 30 cm del límite del cuarto: no encontraba
escritorio ni cama y terminaba colocando solamente ropero y cómoda.

El candidato trasero del escritorio empezaba en Z=9.600, mientras que el
paso reservado terminaba en Z=9.605. Se conserva esa reserva y se amplía la
búsqueda con otro perímetro a 20 cm, después de agotar los candidatos originales.
Quedan 10 cm respecto de la cara de paredes internas. Los mismos filtros de
colisiones, barrido, ventanas y acceso se aplican a ambos perímetros. Se mantiene
el orden del blueprint: escritorio, cama, ropero y cómoda. No se redujo el mínimo
de tres muebles ni se modificaron dimensiones o versiones de mapas.

## Evidencia local

- Diagnóstico anterior al parche: Godot 4.5.2, exit 1, sin timeout, stderr vacío.
  Log conservado: `work/modeler091-furnish-probe01.stdout.log` y `.stderr.log`.
- Regresión posterior: **3028 comprobaciones, 0 fallos**, exit 0, sin timeout,
  stderr vacío. Duración del comando aproximadamente 4.7 segundos; cota de proceso
  55 segundos. Logs `work/modeler091-furnish-regression01.stdout.log` y `.stderr.log`.
- Semillas: 221853394, 1, 2, 7, 31, 97, 257, 997, 2026, 65537, 1234567, 2147483646.
- Comprueba validación completa, cero aristas rechazadas, determinismo, conteo
  real de muebles, límites de cuarto, colisión contra sólidos y paso/barrido.
  Exige escritorio y cama reales en el dormitorio de la semilla problemática.
- Resultado de ese cuarto: `desk`, `bed`, `dresser`. Fingerprint corregido:
  `13b72ce1ef70d39ca07cc5b9d6d625abd77562b13377d8e4eda3431a6c5fa121`.
- Resultados estructurados: `work/modeler091-furnishing-regression-results.json`.

Prueba: `--headless --path game --script res://tests/modeler091_furnishing_regression.gd --single-threaded-scene`.
Motor liberado explícitamente al terminar, sin procesos Godot/Blender restantes.
Director repetirá el corpus de 1000 semillas tras integrar: este resultado local
no sustituye ese corpus ni la validación del build integrado. 0.9.2 permanece aparte.
