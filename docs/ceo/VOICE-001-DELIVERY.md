# VOICE-001 — entrega del núcleo de voz Unity

Fecha: 2026-09-20. Alcance: núcleo independiente de voz para integración por
Bootstrap/UI, sin activar micrófono durante pruebas y sin afirmar evidencia EOS,
WAN, hardware real o escucha humana.

## Componentes entregados

- `Online/VoiceContracts.cs`: contexto inmutable de ronda, rutas de hasta 15
  pares autenticados, controles de volumen/mute y contrato de transporte.
- `Online/VoiceWireCodec.cs`: paquete estricto con epoch, ronda, actor, stream,
  secuencia, frecuencia, muestras y longitud. Rechaza versión, flags, tamaños,
  identidad vacía y bytes sobrantes.
- `Online/VoiceImaAdpcmCodec.cs`: cuadro independiente mono de 240 muestras a
  12 kHz, 20 ms y 126 bytes. No conserva estado implícito entre paquetes.
- `Online/VoiceRateLimiter.cs` y `VoiceJitterBuffer.cs`: límites por emisor,
  prebuffer de 60 ms, ventana futura de 24, cola de 12, hasta tres cuadros por
  tick, cierre a 300 ms y concealment acotado. Un `End` descarta cuadros iguales
  o posteriores a su secuencia y deja tombstone para que un stream cerrado no
  reviva por paquetes tardíos. Las purgas temporales de audibilidad, mute,
  foco o pausa conservan un watermark: permiten reanudar el mismo stream sólo
  con una secuencia nueva, sin reproducir audio ya descartado.
- `Online/VoiceOnlineSession.cs`: PTT, autenticación PUID→actor desde membresía
  real, ronda/epoch, mute local/por actor, volumen, foco/pausa, purga y cambio de
  audibilidad sin reiniciar PTT. Reiniciar PTT no repone los token buckets.
- `Online/VoiceEosChannelTransport.cs`: adaptador exclusivo del canal EOS P2P 3;
  no modifica ni posee el transporte compartido.
- `Audio/Runtime/VoiceMicrophoneCapture.cs`: `Microphone.Start` aparece sólo en
  `BeginPushToTalk`. Cierra y limpia al soltar, perder foco, pausar, deshabilitar
  o destruir. Enumerar dispositivos no inicia captura.
- `Audio/Runtime/VoiceSampleFramer.cs`: resampling streaming a 12 kHz y cuadros
  exactos de 240 muestras.
- `Audio/Runtime/VoicePlayoutStream.cs`: ring PCM por actor, `pitch=1`, mute,
  volumen, RMS de boca y hooks locales de ganancia/low-pass para proximidad y
  oclusión. El componente conserva y destruye únicamente el `AudioClip` runtime
  que creó, tanto al reinicializarse como al destruirse.
- `Audio/Runtime/VoiceMosquitoTimbre.cs`: pitch shifter streaming con dos
  cabezas de retardo y crossfade. Produce una salida por muestra de entrada; no
  acelera el `AudioSource` ni cambia la duración.

## API pública para integración

1. Crear `VoiceEosChannelTransport(EosPeerTransport, EosLobbySession)` y luego
   `VoiceOnlineSession(transport, relojMonotónico)`.
2. Al confirmar roster/ronda, construir `VoiceRoundContext` sólo con miembros
   presentes en la sesión EOS y llamar `UpdateRound`. Una ruta contiene el
   `MemberId` autenticado, `ActorId`, rol mosquito y permisos iniciales de oír.
3. Actualizar proximidad/oclusión mediante
   `SetPeerAudibility(actorId, peerCanHearLocal, localCanHearPeer)`. Este método
   no cambia identidad ni reinicia una transmisión local.
4. Conectar `VoiceMicrophoneCapture.FrameCaptured` a
   `SubmitCapturedFrame`; abrir captura únicamente después de que
   `BeginPushToTalk` acepte el inicio. Conectar cierre/pérdida de foco a
   `EndPushToTalk` y los setters de ciclo de vida.
5. Consumir `FrameDecoded` en un `VoicePlayoutStream` asociado al actor. Aplicar
   `IsMosquito` mediante `SetMosquitoTimbre`; resolver posición, ganancia y
   low-pass sólo desde presentación local.
6. Llamar `ClearRound` antes de abandonar/eliminar el contexto y `Dispose` al
   cerrar la sesión. No guardar ni mostrar el PUID usado para autenticación.

## Evidencia offline reproducible

- Compilación del conjunto Online completo, incluidos EOS adapter y fuentes
  existentes: 0 errores, 0 advertencias contra assemblies locales instalados.
- Compilación del conjunto Audio completo contra UnityEngine 6000.6.0f1: 0
  errores, 0 advertencias.
- Pruebas CPU reflejadas en Unity: 10 casos de red y 2 de señal, todos pasaron
  en harness .NET externo. Cubren wire, ADPCM, jitter/replay/End, rate rollback,
  identidad, membresía, PTT, mute, foco, cambio de ronda, audibilidad y reinicio
  abusivo de PTT; Audio cubre resampling y DSP.
- ADPCM sobre senos de 120/220/440/1000/2400 Hz: cuadro 126 bytes, 20,0 ms y SNR
  45,01/39,90/32,71/26,57/19,55 dB. Son señales, no habla humana.
- Pitch mosquito sobre seno 220 Hz durante 1 s, procesado en cuadros de 240:
  12.000 muestras entran y 12.000 salen; 219,99→285,03 Hz, relación 1,296 y
  duración 1.000,0 ms.
- Carga .NET Release de 30 s: 1/4/8/15 rutas generan
  8.250/33.000/66.000/123.750 bytes/s de aplicación. Encode+wire+decode tomó
  27,44/62,87/84,30/140,13 ms totales, relación tiempo real
  0,00091/0,00210/0,00281/0,00467. No equivale a CPU Unity/IL2CPP ni incluye
  overhead EOS/UDP/IP.

## Gate Unity

Unity 6000.3.24f1 importó y compiló los assemblies y terminó el runner EditMode
con exit code 0: **12/12 passed, 0 failed, 0 skipped**, duración reportada
0,0761273 s. Evidencia:

- `N:/LetMeSleep/Validation/V020/voice-native-01.xml`
- `N:/LetMeSleep/Validation/V020/voice-native-01.log`

Los filtros específicos son:

- `LetMeSleep.Tests.EditMode.VoiceNetworkTests` — 10 casos pasados.
- `LetMeSleep.Tests.VoiceEditMode.VoiceAudioSignalTests` — 2 casos pasados.

Los 10 casos de red y 2 de Audio usan datagramas en memoria y señales generadas.
El runner no abrió `Microphone`, no reprodujo hardware y no estableció una
sesión EOS. Este gate acredita compilación Unity y comportamiento determinista
del núcleo; no acredita escucha, dispositivo, LAN o WAN.

## Corrección posterior de ciclo de vida

Se añadieron dos regresiones EditMode y una PlayMode después del gate nativo
anterior. Las regresiones de red ejercitan secuencia 1, purga temporal y
secuencia 3 sobre el mismo stream, además de repetir el ciclo para mute, foco y
pausa. También comprueban que un replay no entra y que ningún paquete igual o
posterior al `End` revive el stream. La regresión PlayMode inicializa,
reinicializa y destruye `VoicePlayoutStream`, espera la destrucción diferida de
cada clip propio y comprueba que un clip externo sigue vivo.

Validación offline posterior:

- `LetMeSleep.Online`, `LetMeSleep.Audio` y `LetMeSleep.Tests.EditMode`
  compilaron contra las referencias locales de Unity 6000.3.24f1 con 0 errores.
  MSBuild informó conflictos preexistentes de versiones `System.Memory` y
  `System.Buffers`; no son ejecución del runner Unity.
- Harness externo enlazado a las fuentes reales: 16 aserciones pasadas para
  audibilidad, mute, foco, pausa, replay y límite `End`.
- Unity 6000.3.24f1 ejecutó EditMode con 14/14 casos pasados, 0 fallidos y
  0 omitidos (`voice-native-02.xml`): 12 de red y 2 de señal sintética.
- La corrida de clearance pasó 19/19 casos, 0 fallidos y 0 omitidos, incluido
  `VoicePlayoutStreamLifecycleTests.InitializeReinitializeAndDestroyReleaseOnlyOwnedClips`
  (`clearance-voice-native-01.xml`). La prueba PlayMode no abre micrófono ni
  requiere hardware de audio.

Evidencia:

- `N:/LetMeSleep/Validation/V020/voice-native-02.xml`
- `N:/LetMeSleep/Validation/V020/clearance-voice-native-01.xml`

## Límites abiertos

- IMA ADPCM 12 kHz es baseline medible sin dependencia externa; no sustituye un
  codec de voz final ni acredita calidad bajo pérdida WAN.
- No se abrió micrófono ni se probó dispositivo, permiso, cambio USB/Bluetooth,
  salida espacial, eco acústico o audio residual de hardware.
- No hubo sesión EOS entre dos identidades, LAN/WAN, host real ni carga de 16
  participantes. Los 15 pares son una ruta sintética de cálculo, no soporte.
- El DSP conserva duración y sube frecuencia sintética. Falta escucha de habla
  humana, inteligibilidad por rol, artefactos, fatiga y presupuesto en Unity.
- Bootstrap/UI aún deben construir el contexto desde roster/vida/rol, asignar
  input PTT, gestionar objetos de playout y reflejar dispositivos/mute/volumen.
