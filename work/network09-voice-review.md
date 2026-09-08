# Voz ENet: diagnóstico y ensayo posterior al arreglo

El ensayo completo de fuente terminó con **212/212 comprobaciones, exit 0 y ningún ERROR/WARNING en el log combinado**. Son 79 aserciones de configuración, autorización y ciclo de transporte, más 133 comprobaciones de identidad de bytes recibidos; no son 212 conversaciones distintas. Duración del proceso observada por el runner: 7,15 s.

La fixture usa Network y VoiceTransport reales, una autoridad, cuatro clientes autenticados y un quinto peer sin autenticar, todos con SceneMultiplayer separado sobre ENet loopback. Genera PCM mono sintético de 20 ms y lo codifica con libopus 1.6.1, obteniendo paquetes de 60 bytes. El resultado final usa el transporte de producción, sin la subclase opcional de diagnóstico.

## Causa comprobada

Los ensayos anteriores perdían paquetes antes de entrar a `_request_frame`: no aparecían en la traza de ingreso, el validador nativo no registraba error y el límite de throttle ENet bajaba de 32 a 1. Repetir con polling manual y con polling automático de SceneTree produjo el mismo fallo. Por tanto, cambiar el orden de polling o el jitter no resolvía esta pérdida.

Godot 4.5.2 pasa los argumentos de creación del host en un orden incorrecto: el valor de canales solicitado, más dos canales internos, termina en el parámetro de ancho de banda entrante. Con cuatro canales de aplicación resulta un límite de **6 bytes/s**, en vez de ancho de banda ilimitado. Se comprueba comparando la llamada en [enet_multiplayer_peer.cpp](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/modules/enet/enet_multiplayer_peer.cpp) con la firma de [enet_connection.h](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/modules/enet/enet_connection.h) y la constante de canales internos en [enet_multiplayer_peer.h](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/modules/enet/enet_multiplayer_peer.h).

Root corrigió la configuración después de crear el servidor y antes de asignarlo a Multiplayer: `peer.get_host().bandwidth_limit(0, 0)`. El protocolo conserva los cuatro canales de aplicación. En las 38 muestras del ensayo posterior, **throttle y throttle_limit permanecieron en 32**. La ráfaga intencional de 100 paquetes recibió sólo 12 durante 82 ms: el límite de la aplicación sigue funcionando; quitar el límite erróneo de ENet no desactiva esa defensa.

## Cobertura del ensayo final

- Tráfico real HH, HM, MM y MH; permisos, pitch y atenuación decididos por la autoridad, sin eco propio ni emisión del peer sin autenticar.
- Baseline positivo antes de comprobar rechazos; epoch, duplicados, ventana de reordenamiento, secuencias inválidas, TOC estéreo/duración incorrecta y paquetes vacíos, malformados o largos.
- Mute por receptor; nuevo permiso al reactivar durante el mismo PTT; descarte de paquetes tardíos del permiso anterior.
- `request_resume` válido únicamente para el permiso propio y epoch vigente; first_sequence renovado, otros oyentes conservados y nueva evaluación de distancia/mute.
- Revocación por distancia y eliminación; puerta real abierta/cerrada; límite de paquetes y control de spam que no se reinician al cambiar PTT.
- Fin con cola acotada, rechazo posterior, cambio a resultados, mapa generado nuevo y roles invertidos, permisos antiguos rechazados y limpieza al desconectar.

Las posiciones, roles y fases se preparan explícitamente en la simulación del servidor; el tick de juego se congela para aislar transporte. Los temporizadores de voz y los RPC son reales. No demuestra partida espontánea, captura de micrófono, decodificación, DSP, escucha, inteligibilidad, WAN ni EXE exportado. La prueba de frases y VoiceSession corresponde al siguiente gate de UI.

Al cerrar este informe, UI comunicó que su integración de un segundo todavía recibía 27/50 paquetes humanos y 16/50 de mosquito, con huecos periódicos. Ese resultado pertenece a otra fixture y permanece en investigación; el PASS del observador de transporte aquí documentado no declara cerrada la recepción continua de VoiceSession.

## Evidencia preservada

- `game/tests/network09_voice_checks.gd`: fixture reproducible; los modos opcionales `--poll-native` y `--trace-ingress` son diagnósticos declarados en su JSON.
- `work/network09-voice-fixed.log`, `work/network09-voice-fixed-results.json` y `work/network09-voice-fixed-manifest.json`: ensayo verde, comandos, salida, hashes y estadísticas.
- `work/network09-voice-poll-manual.json` y `work/network09-voice-poll-native.json`: comparación anterior al arreglo, ambos con throttle_limit 1 y fallos reales.
- Las corridas anteriores y el intento de instrumentación fallido por asignar el retorno void de `rpc_id` permanecen separados. Ese error de la fixture no se atribuye al runtime del juego.

No se modificaron preferencias ni se usaron dispositivos de audio. La reserva de procesos se liberó y se cedió directamente a UI.
