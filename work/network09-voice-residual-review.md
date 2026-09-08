# Pérdida residual de VoiceSession: enlace identificado

Análisis de lectura de la corrida instrumentada de UI, posterior a `bandwidth_limit(0,0)`. No se ejecutó otra fixture ni se modificó VoiceSession, VoiceTransport o la fixture de UI.

| Medida | Humano | Mosquito |
|---|---:|---:|
| Frames codificados, 960 muestras | 50 | 50 |
| Paquetes válidos de 60 bytes | 50 | 50 |
| Entradas reales a `_request_frame` | 41 | 43 |
| Paquetes en `packet_received` | 41 | 43 |
| Throttle emisor hacia host | 26 | 26 |
| Límite throttle emisor | 32 | 32 |
| RTT emisor observado | 6 ms | 6 ms |
| Throttle y límite host hacia receptor | 32 / 32 | 32 / 32 |
| Mínimo de tokens de aplicación observado | 10,65 | 10,65 |

Las listas de secuencias del ingreso RPC y del observador receptor son idénticas, en orden. Los frames faltantes ya faltan antes de la autoridad. Por eso esta pérdida no viene del validador de Opus, permisos, rate limit de la aplicación, jitter ni DSP. El enlace emisor hacia host conserva un throttle reducido aunque su límite de ancho de banda ya es correcto.

ENet descarta comandos no fiables comparando un contador modular con `packetThrottle`; su control dinámico reduce ese valor cuando el RTT supera el historial reciente. Es coherente con las pérdidas periódicas observadas y las estadísticas del enlace. [Descarte en protocol.c](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/thirdparty/enet/protocol.c), [ajuste por RTT en peer.c](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/thirdparty/enet/peer.c).

La traza comienza cuando el throttle ya vale 26. **No demuestra qué ACK lo redujo ni permite atribuirlo específicamente a ActorView.** Durante las frases anteriores el período de observación fue estable, aproximadamente 6,9 ms; eso tampoco reconstruye el tiempo empleado por la carga previa de modelos. La siguiente medición mínima, si se necesita atribuir el disparador, debe guardar throttle, límite, RTT anterior y varianza después de autenticación, antes y después de `_context`/`_prepare_roles`, y después del primer polling posterior a cada carga.

Se mantuvieron las exigencias de 50 frames y continuidad sin PLC; no se cambió el protocolo a fiable ni se desactivó el control dinámico para hacer pasar el gate. La causa del límite inicial de 6 bytes/s sigue siendo un defecto separado, ya corregido y documentado en `network09-voice-review.md`.

Evidencia inmutable copiada desde la salida de UI, con SHA256 comprobado antes y después de la copia:

- `work/network09-voice-residual26-human.json`
- `work/network09-voice-residual26-mosquito.json`
- `work/network09-voice-residual26-summary.json`: conteos, secuencias faltantes, estadísticas y hashes de la corrida instrumentada.

Las trazas de esta corrida emplean la subclase de diagnóstico de ingreso RPC. El ensayo anterior 212/212 del transporte aislado usó VoiceTransport de producción sin esa subclase. Ninguno demuestra escucha de micrófono o conectividad WAN.

## Ping y recuperación: lectura del motor

El motor ya envía ping automático cada 500 ms cuando no tiene comandos fiables pendientes. El tráfico saliente no fiable deja `canPing` activo; no impide por sí solo ese ping. El ACK actualiza RTT y evalúa throttle. La referencia de RTT se renueva con una ventana de 5.000 ms; aceleración y desaceleración predeterminadas valen 2. Si la muestra queda entre la referencia y el umbral superior, el throttle no cambia. [Condición y ACK](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/thirdparty/enet/protocol.c), [constantes](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/thirdparty/enet/enet/enet.h), [regla adaptativa](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/thirdparty/enet/peer.c).

Godot expone `ENetPacketPeer.ping()`, que solicita un ping con ACK sin tocar el throttle. Por tanto, un ping explícito al terminar preparación es un experimento válido; un heartbeat adicional de 500 ms no tiene por ahora una causa demostrada que corregir. [API implementada](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/modules/enet/enet_packet_peer.cpp).

Recomendación acotada enviada a Root/UI: registrar también LAST_RTT, LAST_RTT_VARIANCE y THROTTLE_EPOCH; comparar preparación de modelos previa a conexión con el arranque frío actual, manteniendo las 50 entregas exigidas. Conservar el fallo frío como evidencia separada, sin convertir una espera hasta throttle32 en prueba de recuperación inmediata. Si el runtime real reproduce un estado persistente tras cargar mapa, una ventana adaptativa de 1.000 ms con aceleración/desaceleración 2 sería un candidato para medir, no un arreglo validado ni una garantía de entrega íntegra.
