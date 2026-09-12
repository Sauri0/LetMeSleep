# U09414: ronda completa, resultado y reinicio

**PASS del recorrido nativo automatizado**, 12 de septiembre de 2026. Fuente de la candidata `35c2af4b168f5b95943f09fbb2c556924dd7b2cf`; editor sobre root `79f9031`, cuya diferencia comprometida respecto a la candidata era sólo documentación. Unity **6000.3.24f1**, Windows, D3D11, PlayMode por lotes, PID16368. No hubo cambios de código para esta entrega.

| Rol local | Final natural | Tiempo de simulación | Sangre | Ganador |
|---|---|---:|---:|---|
| Humano | BloodGoal, tick1216 | 40.533 s | 20 / 20 | Mosquitos |
| Mosquito | TimeExpired, tick5400 | 180 s | 7.999994 / 20 | Humanos |

Se inició cada rol mediante los listeners reales de los botones de selección e inicio. `AutomaticTick=true`, `Time.timeScale=1`, bots y física del runtime; `CaptureLocalInput=false` mantuvo al participante local sin controles. No se alteraron reloj, cuota o sangre, ni se forzó EndRound. En el rol mosquito, la sangre obtenida corresponde al compañero bot. El recorrido completo del observador duró 224.38 s de pared; no es una medición de rendimiento.

En ambos finales se comprobó una sola notificación RoundFinished, pantalla Results, ganador/tiempo/sangre coincidentes con el estado autoritativo y controles de gameplay bloqueados. Se activó el botón real **Repetir entrenamiento** y se verificó:

- Epoch, mapa y runtime nuevos; los anteriores desactivados inmediatamente.
- Tick0, 180 s restantes, sangre0, resultado None y ganador Unassigned.
- Rol local conservado, tres actores con estado inicial, velocidades cero, sin anclas de picadura/posado, recuperación pendiente ni herramienta equipada.
- Siete herramientas libres en revisión1 y nueve puertas en revisión1 con apertura inicial100°.
- Controles desbloqueados y estado privado anterior descartado.
- Tras un segundo: tick30, exactamente un runtime, tres actores, siete herramientas y una cámara de salida.

Después de cada reintento se llamó CancelTraining para comprobar limpieza: MainMenu, cámara de menú restaurada y cero runtimes, actores o herramientas de partida activos. El editor se cerró limpiamente y el slot quedó libre. No se observaron excepciones ni errores C# en el log del recorrido.

El checkout estaba limpio al comenzar y antes de salir de PlayMode. Al salir, Unity escribió datos dinámicos en `AtkinsonHyperlegible-Regular SDF.asset` y `Bangers-Regular SDF.asset`; se comunicó al Director y W1 no restauró ni comprometió archivos UI ajenos. Estos cambios de caché no forman parte de la candidata publicada ni de esta entrega documental.

Recibo resumido: `U09414-NATIVE-TRAINING-20260912.json`. Evidencia completa y script reproducible: `N:/LetMeSleep/Validation/U09414-20260912/`, con hashes SHA256 en el recibo. `full-rounds.json` contiene inicios, muestras, resultados, reintentos y limpieza; `FullRounds.cs` es el observador temporal ejecutado por Pipeline, fuera de Assets y sin importar scripts al proyecto.

Alcance aprobado: fin y reinicio automatizados de entrenamiento Sangre en ambos roles. No sustituye una partida manual, aceptación de controles/diversión, validación del ejecutable instalado, calidad visual, FPS o WAN/EOS entre dos identidades. No se inició otra etapa del plan ni se republicó la candidata.
