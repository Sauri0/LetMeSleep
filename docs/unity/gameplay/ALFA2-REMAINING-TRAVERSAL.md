# Alfa.2: entrada frontal, pared y techo

**PASS de los casos acotados**, sobre candidata `0a82314`, root `bae7869d784587c612a3ec46893233d101cc0e6c` (sólo documentación adicional). Unity6000.3.24f1, Windows, editor D3D11 por lotes PID33400, cerrado al terminar. No se modificaron fuentes, escenas, Content ni Presentation, ni se republicó la candidata.

## Entrada frontal y exterior inmediato

Humano desde el spawn normal: Entry → frente exterior z≈−1 → lateral x≈8 y x≈4 → vuelta por Entry al interior. Se alcanzaron seis puntos de control, sin saltos ni teletransporte. Posición final (6.053313, .000159, 1.064174).

Esta prueba acredita salida y regreso por la entrada frontal y el espacio exterior inmediato. No acredita una conexión lateral alrededor de la casa hasta el patio trasero. La ruta PatioDoor ya validada no se repitió.

## Mosquito: aproximación, movimiento y despegue

Cada fixture comenzó como entrenamiento mosquito desde su spawn normal, con bots activos. Se voló mediante comandos autenticados hacia la superficie, se frenó, se pulsó F, se giró la mirada, se caminó sobre ella y se pulsó F nuevamente para despegar. No se asignaron posiciones ni LifeState. Los campos locales de yaw/pitch se actualizaron con los mismos valores de los comandos para representar la mirada que normalmente escribe el ratón, sin alterar transforms de cámara.

| Caso | Soporte | Posición al adquirir Surface | Movimiento tangencial | Separación tras despegar |
|---|---:|---|---:|---:|
| Pared interior de Living, normal −X | 10276 | (4.923, 1.902600, 1.994799) | .368335 m hacia +Z | .52 m hacia −X |
| Techo de Living, normal −Y | 10230 | (3.579814, 2.742008, 1.297169) | .368335 m hacia +X | .52 m hacia −Y |

En ambos casos se conservó Surface durante el recorrido y el giro de mirada. El primer tick observado tras F pasó a Flying, sin ancla, con ViewRevision2: tick119 en pared y tick107 en techo. Después se avanzó en vuelo libre alejándose del soporte. Para mirar hacia el techo se respetó el límite normal de pitch de89°.

## Cámara y límites de las muestras

Se tomaron seis muestras por caso: hacia la superficie, paralela, alejándose de ella, después de caminar, primer tick observado de despegue y vuelo posterior. Las **12 muestras** tuvieron posición y distancia finitas, **cero solapamientos** de la esfera de cámara de radio.08 con sólidos del World, y **cero bloqueadores** del rayo cámara→anchor. Se excluyeron las colisiones propias del actor, igual que en el filtro de cámara.

Mirando hacia fuera del soporte, ResolvedDistance se redujo a0, pero la cámara quedó separada del sólido mediante el anchor corregido: pared x4.885 y techo y2.705. A diferencia del fallo previo, no se encontró solapamiento. Son muestras de estado después de actualizaciones normales; no una certificación de cada frame renderizado ni revisión visual de clipping.

## Operación y evidencia

El Play inicial produjo audio audible durante el uso del PC. Tras el aviso se aplicó inmediatamente `AudioListener.volume=0`, se confirmó por API y el ensayo de techo se ejecutó silenciado. No se modificaron preferencias guardadas. En adelante, los editores para pruebas físicas deben arrancar también con `-noaudio` y conservar el mute tras entrar en Play. Al terminar se canceló el entrenamiento, se salió de Play y se cerró el editor.

El checkout principal estuvo limpio antes y durante las pruebas; al cerrar, Unity volvió a escribir datos dinámicos en los dos assets TMP AtkinsonHyperlegible-Regular y Bangers-Regular. Se informó al Director; W1 no restauró ni comprometió esos archivos ajenos.

Recibo resumido y hashes: `ALFA2-REMAINING-TRAVERSAL-20260912.json`. Scripts y recibos originales en `N:/LetMeSleep/Validation/Alfa2-RemainingTraversal-20260912/`: `front-human.json`, `wall-attempt1.json`, `ceiling-attempt1.json`, `FrontTraversal.cs`, `WallCeilingProbe.cs`, confirmaciones de mute y cierre. La única diferencia operativa añadida al script entre pared y techo fue el mute explícito al iniciar.

No se cierra U094-06/U094-09 globalmente: quedan otras normales de pared, todas las habitaciones/uniones y el recorrido completo interior/exterior del mosquito. Tampoco se acreditan partida manual, diversión, aceptación visual, validación del paquete instalado, WAN ni FPS del hardware objetivo. No se detectó un nuevo defecto que requiriera parche en este lote.
