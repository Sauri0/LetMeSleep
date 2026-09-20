# VOICE-001 — contrato y decisión técnica

Fecha: 2026-09-19. Este documento fija el alcance implementable del primer
pipeline de voz Unity y las mediciones que deben acompañarlo. No acredita una
prueba WAN, una escucha humana ni capacidad para dieciséis voces simultáneas.

## Evidencia local

- `unity/Packages/manifest.json` fija `com.playeveryware.eos` 6.1.2 y el paquete
  local contiene EOS SDK 1.19.1.2. El código generado incluye RTC/RTCAudio, pero
  habilitar voz de lobby requiere configuración externa de producto, política
  de cliente y conferencia. Eso queda fuera del ticket sin cuentas ni cambios
  de servicio.
- `EosPeerTransport.MaximumPacketBytes` es 1170 y sus validadores ya aceptan los
  canales 0 a 3. Los canales 0, 1 y 2 tienen usos de sala, gameplay y sondeo; el
  canal 3 queda reservado para voz. No hace falta modificar el transporte base.
- Godot usa Opus a 48 kHz, cuadros de 20 ms, jitter inicial de 60 ms, cola de 12
  cuadros y cierre por inactividad de 300 ms. Su binario
  `game/addons/lms_opus/bin/lms_opus.windows.x86_64.dll` tiene SHA-256
  `e5c2e4161e551451c4186a2627adf969a2bbfb12b7b5e7348361a1629dfeae25`, pero es
  una extensión ABI de Godot. No expone un runtime Unity ni una C ABI genérica.
  El proyecto Unity tampoco contiene Concentus, WebRTC u otro paquete Opus.
- La licencia del paquete PlayEveryWare es MIT y Opus usa su licencia BSD. No
  se incorpora ninguno como dependencia nueva en esta entrega.

## Baseline elegido

La primera versión usa EOS P2P por canal 3 y un codec IMA ADPCM propio, mono a
12 kHz, con cuadros independientes de 20 ms. Cada cuadro contiene 240 muestras,
predictor e índice propios y 239 diferencias de 4 bits. La independencia limita
la propagación de corrupción o pérdida y permite descartar/reordenar paquetes
sin conservar estado de codec implícito.

El payload del codec ocupa 126 bytes. El encabezado de voz ocupa 39 bytes:
magic y versión (3), tipo y flags (2), epoch y round (16), actor (4), stream y
secuencia (8), muestra/cantidad (4) y longitud (2). Un cuadro de audio completo
ocupa 165 bytes. A 50 cuadros por segundo son 8.250 bytes/s, 66 kbit/s por ruta,
antes del overhead de EOS/UDP/IP. Un emisor enviado a quince pares suma
123.750 bytes/s, 990 kbit/s de payload de aplicación. Esta cifra es un peor caso
de distribución directa y no una afirmación de que dieciséis participantes
simultáneos sean sostenibles.

El emisor se limita a 55 paquetes por segundo con ráfaga máxima de 12. El
receptor aplica el mismo límite por miembro autenticado, mantiene hasta 12
cuadros por stream, sólo acepta una ventana futura de 24 secuencias, prepara 60
ms y entrega como máximo tres cuadros por actualización. Un stream sin datos se
cierra a los 300 ms.

IMA ADPCM es una base sin dependencia y con coste predecible. Su calidad de voz
y su bitrate son inferiores a Opus. La entrega debe medir error, duración,
payload, pérdida y simultaneidad sintética, y dejar escucha humana, WAN y el
codec de producción como validaciones abiertas.

## Identidad, privacidad y wire

`VoiceRoundContext` se construye desde la membresía real de la sesión y contiene
epoch, round, actor local elegible y rutas de pares elegibles. Valida hasta 15
pares, PUID y actor únicos, IDs no nulos y ausencia del actor local en pares. El
PUID sólo se usa como clave de autenticación/ruta y no aparece en logs ni en el
paquete.

Cada paquete contiene epoch, round, actor, stream y secuencia. Al recibir, la
sesión resuelve primero el PUID autenticado del transporte contra el contexto y
exige que el actor del paquete coincida con el actor asignado a ese PUID. Nunca
confía en el actor entrante para seleccionar identidad o mute. Se rechazan
versiones, tipos, tamaños, enums, trailing bytes, ronda/epoch antiguos, actor no
miembro, duplicados, secuencias fuera de ventana y exceso de tasa.

Los tipos de paquete son `Audio` y `End`. `Audio` siempre transporta exactamente
un cuadro válido; `End` no transporta payload y termina únicamente el stream
actual. Un stream nuevo invalida cola y estado del anterior. Un cierre de ronda,
pérdida de foco, pausa, mute propio, eliminación, salida de lobby o `Dispose`
detiene captura y envío, emite `End` cuando todavía existe ruta y purga buffers
de recepción/playout.

## API de integración

La capa Online ofrece:

- `UpdateRound(VoiceRoundContext)` para reemplazar de forma atómica membresía,
  elegibilidad y reglas de audición derivadas por la sesión.
- `BeginPushToTalk(now)`, `SubmitCapturedFrame(samples, now)` y
  `EndPushToTalk(now)`; fuera de PTT no hay apertura ni envío.
- `Tick(now)` y un evento de cuadro decodificado con actor, PCM y nivel. El
  consumidor presenta el audio; la voz nunca modifica el estado autoritativo.
- mute propio, mute por actor, volumen maestro y volumen por actor como estado
  local. Mutear purga el playout correspondiente.

`VoiceEosChannelTransport` adapta `EosPeerTransport` sin cambiar su framing y
filtra exclusivamente el canal 3. Las pruebas usan un transporte en memoria.

La capa Audio ofrece `VoiceMicrophoneCapture`, cuyo único método que puede
llamar `Microphone.Start` es `BeginPushToTalk(device)`, y playout por actor con
buffer PCM, volumen, mute y envolvente RMS para boca. `OnDisable`, destrucción,
pausa y pérdida de foco paran el micrófono y borran buffers. Las pruebas de esta
entrega usan señales sintéticas; no abren hardware.

## Timbre mosquito

`AudioSource.pitch` queda fijado en 1 porque cambia timbre y duración a la vez.
La referencia Godot usa un pitch shifter con conservación temporal. Unity usa
un shifter streaming de dos cabezas de retardo con crossfade: emite exactamente
una muestra por cada muestra de entrada y conserva la duración. La señal
sintética confirma aumento tonal y duración; escucha de habla, artefactos y
presupuesto dentro de Unity siguen siendo gates separados.

## Criterios de aceptación medibles

- Señal sintética: 240 muestras por cuadro, duración decodificada idéntica,
  payload de 126 bytes y error/RMS registrados.
- Wire: round/epoch/actor autenticado, límites exactos, rechazo de corrupción y
  consumo completo.
- Jitter: reordenamiento, duplicado, pérdida, concealment acotado, cierre, idle
  y cambio de stream sin audio residual.
- Sesión: PTT exclusivo, mute propio/por par, foco/pausa/eliminación/salida y
  cambio de ronda purgan estado; no se registra ningún PUID.
- Carga sintética: medir CPU/tiempo y bytes para 1, 4, 8 y 15 rutas. El resultado
  se documenta; no sustituye una prueba EOS real ni WAN.
