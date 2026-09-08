# Adaptación al transporte de voz del juego

Estos son límites de integración de la API entregada; no implican que el juego
ya los implemente. El núcleo usa microsegundos locales, devuelve diccionarios y
una lista de eventos por poll. Si el adaptador usa milisegundos, multiplicar por
1000 al entrar. Comprobar `result.accepted`/`result.ok`, no la verdad del diccionario.

## Identidad y reingreso

Mantener tres identidades separadas: peer emisor autenticado, epoch de PTT en la
red y permiso actual de escucha para ese receptor. Un permiso puede tener ID
monotónico y `first_sequence` autorizada por el servidor. La generación local
entregada a `begin_stream` aumenta cuando se reconstruye la reproducción, aunque
el emisor continúe el mismo PTT. Los paquetes se remapean a esa generación sólo
después de validar peer, epoch y permiso en el adaptador.

No recrear una cola a partir de cualquier paquete tardío. Al reingresar por
distancia o mute, el punto de comienzo debe cubrir el cursor ya consumido o
saltado, no sólo el paquete más alto aceptado. Un cursor local antiguo tampoco
sustituye a una barrera fresca del servidor tras tiempo fuera de alcance.

## Recepción y reproducción

1. Validar tipo/tamaño del paquete y límites del emisor antes de llamar al codec.
   Para el presupuesto comunicado de 160 bytes, configurar `max_payload_bytes=160`
   y aplicar el mismo límite en el servidor. `LMSOpusCodec.validate_frame` verifica
   estructura mono de 20 ms sin decodificar ni alterar el historial; no autentica
   al emisor ni garantiza que todo bitstream pueda decodificarse.
2. Insertar el paquete validado con la hora local de recepción. No usar timestamps
   remotos para los plazos. Los duplicados/rechazos no mantienen vivo un stream.
3. En cada poll, procesar los eventos en orden. Si `reset_decoder` es verdadero,
   reiniciar el decoder antes de data o PLC. Data llama a `decode_frame`; PLC llama
   a `conceal_frame`. Cada resultado debe tener 960 muestras finitas.
4. Mantener separadas las colas del jitter y del reproductor. El descarte por pausa
   del jitter no vacía automáticamente el audio que ya se envió al Generator/DSP.
   Una discontinuidad requiere también resolver esa cola de salida para que no
   sobreviva audio atrasado. Medir la demora total, no sólo el prebuffer de 60 ms.
5. El RMS de boca corresponde al audio reproducido y su reloj. El filtro agudo
   probado añade aproximadamente 40 ms en el fixture; el RMS previo al efecto sin
   compensación adelantaría la animación. Usar buses/estado por emisor.

## Fin de frase frente a revocación

- Fin normal de PTT: `finish_stream(local_generation,last_sequence,now_us)` permite
  reproducir hasta el último frame anunciado y después termina. Paquetes reordenados
  anteriores a ese límite todavía pueden entrar; un frame final perdido usa PLC
  una vez. No purgar el prebuffer al soltar el botón: recortaría el final de la frase.
- Mute, distancia revocada, salida de sala, eliminación del emisor o reinicio:
  `stop(reason)`/`clear()` purgan inmediatamente. También vaciar Generator y DSP,
  detener el reproductor y llamar a `ActorView.clear_voice_level()`. Un callback
  tardío de la generación anterior no debe volver a abrir la boca.

Los RPC, permisos del servidor, el micrófono, el mezclador, la animación y su
limpieza son responsabilidad del adaptador del juego. Los fixtures del núcleo
no sustituyen las pruebas de esa cadena ni de dos redes diferentes.
