# Chat de voz v0.3.0: proximidad, entorno y estabilidad

Frente: voz en tiempo real (rama `claude/v030-voice`). Alcance: `Online/Voice*`, `Audio/Runtime/Voice*`,
`Bootstrap/VoiceRuntimeCoordinator.cs`, `Bootstrap/VoicePeerPresenter.cs`, `AlfaApplication.Voice.cs`, dos textos
de la UI de voz y sus pruebas. **No se tocó el mixer ni el resto del audio** (dueño: frente de audio).

Pedido del usuario: "debe haber chat de voz de proximidad y que fluctúe con el entorno, el sonido debe ser
espacial … correcto, estable … pulido".

Evidencia: `N:/LetMeSleep/Validation/V030/Voice/` (ver §7). Pendiente y declarado como tal: una prueba real con
dos personas en redes distintas, micrófonos reales y escucha humana (§8).

---

## 1. Qué estaba mal (medido en `r1-before`, código de `claude/v0.3.0`)

- **Cortes al empezar cada PTT y huecos en estado estable.** El `AudioClip` de streaming de `VoicePlayoutStream`
  pedía 2 400–4 096 muestras (200–341 ms) por lectura contra un prebuffer de 60 ms y rellenaba con ceros: voz
  continua recién a los 0,86–1,28 s y huecos de 8–150 ms (S11 del harness de auditoría).
- **El entorno no existía para la voz**: interior = exterior (−0,1 dB, misma cola), oclusión binaria por un rayo
  pies↔pies, sin frente/atrás, voz ubicada a 25 cm del piso.
- **Distancia**: meseta de 0 dB hasta 4 m y caída tardía; el mosquito junto al oído sonaba igual que a 4 m.
- **Captura**: sin AGC, compuerta ni fundidos de PTT; remuestreo lineal sin anti-alias; clip de micrófono que se
  perdía en cada pulsación; sin voz hasta elegir micrófono a mano.
- **Doppler 1** en la fuente de voz (un mosquito que habla volando desafinaba) y paneo duro (oído opuesto en cero).

## 2. Arquitectura nueva

```
emisor:  micrófono (48 kHz) → VoiceSampleFramer (sinc anti-alias → 12 kHz, cuadros de 20 ms)
         → VoiceCaptureProcessor (HPF 90 Hz · compuerta −24 dB con histéresis · AGC → −18 dBFS · limitador
           ≤ −1 dBFS · fade-in 20 ms · cola de soltado 40 ms) → IMA ADPCM → wire v1 → EOS P2P canal 4
receptor: VoiceOnlineSession / VoiceJitterBuffer (prebuffer 40 ms, reordenamiento con gracia de 30 ms,
          después entrega en orden sin marcar el paso) → decodificación
         → VoiceConcealer (cuadro perdido: continuación del período de pitch, 5 ms de fundido al volver)
         → VoiceMosquitoTimbre (sólo mosquitos: WSOLA + remuestreo, +4 semitonos, nivel compensado)
         → VoicePlayoutStream / VoicePlayoutEngine (hilo de audio, OnAudioFilterRead):
             anillo con prebuffer adaptativo (≥ 40 ms, +20 ms por falta, devuelve ¼ del exceso cada 4 s estables)
             control PI de deriva (≤ ±0,6 % de velocidad) · ocultación de faltas en el hilo de audio
             ganancia suavizada · pasa-bajos TPT · estante detrás/arriba · limitador suave
             reverb de sala FDN difusa (L/R decorrelados) · sobremuestreo sinc a la salida
             × ganancias espaciales de Unity (clip constante 1,0) con crossfeed según distancia
presentación: VoicePeerPresenter (por par, cada frame): política de proximidad (VoiceSpatialPolicy),
          VoiceAcousticProbe (5 rayos boca→oído a 10 Hz; sala: 13 rayos, caché 0,5 m / 3 s),
          suavizado temporal (oclusión: 150 ms sube / 300 ms baja; sala: 0,5 s), ruteo con histéresis.
```

El protocolo de red **no cambió** (wire v1, 165 B por cuadro). El único cambio de emisor que afecta los bytes es
la elección del índice inicial de IMA ADPCM (búsqueda ±8 del heurístico, menor error): el decodificador lo lee
del encabezado, así que clientes viejos y nuevos se entienden (+1,0–1,1 dB de SNR con el mismo decodificador).

### 2.1 Proximidad (VoiceSpatialPolicy)

| Distancia | Humano → humano / humano → mosquito (corte 12 m) | Mosquito → humano (corte 8 m) | Mosquito → mosquito (corte 16 m) |
|---|---|---|---|
| ≤ 0,5 m | +8 dB (tope: "zumba en el oído") | +8 dB | +8 dB |
| 0,8 m | +5,9 dB | +5,9 dB | +5,9 dB |
| 1 m | +4,5 dB | +4,5 dB | +4,5 dB |
| 2 m | 0 dB (referencia) | 0 dB | 0 dB |
| 3 m | −2,6 dB | −2,6 dB | −2,6 dB |
| 5 m | −5,9 dB | −6,2 dB | −5,9 dB |
| 7 m | −8,1 dB | −22 dB | −8,1 dB |
| 8 m | −9,9 dB | **0 exacto** (corte) | −9,0 dB |
| 10 m | −19,8 dB | — | −10,7 dB |
| 11,5 m | −43,7 dB | — | −13,8 dB |
| ≥ 12 m | **0 exacto** | — | −15,4 dB (corte a 16 m) |

−4,5 dB por duplicación alrededor de 2 m y un fundido coseno sobre el último 42 % del alcance (pendiente cero en
el corte: se desvanece, no salta). Más allá del corte la ganancia es exactamente 0 y la ruta de red se cierra
(histéresis de 1 m para no alternar). Cada dirección se evalúa por separado: un humano llega a 12 m a un mosquito
aunque el mosquito sólo llegue a 8 m al humano (antes se usaba la misma evaluación para ambas). Mosquito ↔
mosquito: 16 m. Vivos y eliminados siguen sin cruzarse. Distancia oscura suave: pasa-bajos de 5,5 kHz hasta 4 m
y 3,8 kHz en el corte. La voz sale de la **boca** (humano: 1,45 m, −0,64 m agachado; mosquito: su cuerpo) y se
escucha desde el **oído** del jugador local (humano 1,53 m; mosquito su cuerpo, no la cámara de seguimiento).

### 2.2 Entorno

- **Oclusión graduada**: 5 rayos boca→oído (centro 0,4; ±0,35 m lateral y ±0,3 m vertical, 0,15 cada uno). La
  fracción bloqueada escala −7 dB (una pared), −11 dB (dos), −15 dB (tres) y un pasa-bajos de 5,5 kHz a 1,5 kHz
  (0,9 kHz con dos paredes). Una puerta abierta deja pasar parte (≈ −2 dB, ≈ 3,7 kHz con un 30 % bloqueado); cerrada, como pared.
  Sólo cuenta geometría del mapa (`UnityGameplayWorld.IsWorldCollider`, sin actores ni triggers).
- **Interior / exterior**: el probe de sala mide techo, paredes alrededor y diagonales. Exterior: decaimiento
  0,25 s y envío −29 dB (apenas aire). Interior: decaimiento 0,35 s (cuarto) a 1,1 s (salón), envío −12 a −9 dB,
  amortiguación de agudos más oscura en cuartos chicos. Se mezcla la sala del hablante y la del oyente. La reverb
  es difusa (no se panea con la voz) y su envío baja más lento que la voz directa con la distancia (lejos suena más
  "a sala"); junto al oído es seca.
- **Sin clics al cambiar de estado**: la presentación suaviza (oclusión 150/300 ms, sala 0,5 s) y el motor vuelve a
  suavizar por muestra (ganancia 12 ms, filtro 40 ms, reverb por bloque de 16 muestras con retardos fijos).
- **Dirección**: paneo de Unity (fuente 3D) + crossfeed de 6 % (junto al oído) a 20 % (≥ 3 m) para que el oído
  opuesto nunca quede en silencio digital; detrás: −5 dB sobre 2,8 kHz y −1,5 dB; arriba: +2 dB de brillo.
  `dopplerLevel = 0`, `spread = 0`, `priority = 8`, `bypassReverbZones` (la voz ya dibuja su sala).

### 2.3 Estabilidad

- **Playout en el hilo de audio** (`OnAudioFilterRead` sobre una fuente que reproduce un clip constante 1,0: los
  datos que entrega Unity son las ganancias espaciales y se multiplican por la voz). Consume a la granularidad del
  DSP (21 ms a 1024/48 kHz). La fuente sólo suena mientras hay voz y se detiene tras 1,5 s de silencio: pares
  callados no ocupan voces reales.
- **Jitter**: el anillo del playout es el reloj. El jitter buffer sólo guarda los primeros 40 ms, reordena y
  decide pérdidas (espera 30 ms a un cuadro que falta si ya llegó uno posterior); después entrega en cuanto hay
  cuadros contiguos. Antes marcaba el paso a 50 fps del receptor, lo que escondía el excedente de un emisor más
  rápido y hacía crecer la latencia.
- **Prebuffer adaptativo**: 40–47 ms al empezar; +20 ms por cada falta (hasta 160 ms); tras 4 s sin faltas devuelve un
  cuarto del exceso (mínimo 5 ms) cada 4 s.
- **Deriva de reloj**: control PI del nivel del anillo (≤ ±0,6 % de velocidad, 10 cents, inaudible). Si la
  latencia supera el objetivo en 100 ms (tras un congelamiento) salta con crossfade de 10 ms.
- **Pérdida**: el cuadro perdido se sintetiza continuando el último período de pitch (10 ms a nivel pleno, luego
  fundido a cero en 50 ms) y el cuadro real que vuelve entra con crossfade de 5 ms. Si el anillo se vacía (red
  tarde o un congelamiento del juego) el hilo de audio hace lo mismo en vez de un hueco.
- **Fundidos**: 20 ms al pulsar y 40 ms de cola al soltar en el emisor (la captura sigue 40 ms tras soltar V y
  recién entonces envía `End`); 5 ms al empezar/terminar en el receptor y 10 ms antes de cada fin de stream.
  Silenciar, perder el foco o pausar cierran el micrófono **al instante** (privacidad) y el receptor funde.
- **Niveles**: AGC hacia −18 dBFS RMS (−8 … +20 dB, sólo aprende con habla), compuerta con piso −24 dB
  (piso de ruido = mínimo de 2 s; nunca cierra dentro del habla), limitador suave ≤ −1 dBFS en el emisor y en
  cada voz del receptor.
- **Eco**: nunca se reproduce la voz propia; mientras suenan voces remotas la compuerta sube su umbral a 12 dB
  bajo ese nivel (lo que el micrófono capte de los parlantes no se retransmite). No hay cancelación acústica de
  eco real (necesitaría un paquete externo).
- **Dispositivos**: selección vacía = micrófono predeterminado del sistema ("PREDETERMINADO DEL SISTEMA" en
  Ajustes); un micrófono elegido que falta no se reemplaza solo (aviso). Captura a 48 kHz (o lo que admita el
  dispositivo) con remuestreo sinc. Micrófono desconectado, sin permiso o que no entrega muestras en 0,75 s:
  se detiene y avisa, sin colgar la transmisión. Cambiar de micrófono durante una transmisión la cierra. Cambio
  de dispositivo de salida: el playout sigue la nueva frecuencia y descarta lo encolado. El clip de
  `Microphone.Start` se destruye al soltar (antes se perdía uno por pulsación).
- **Salir de la sala**: cada voz se funde en 5 ms en el hilo de audio y luego se destruye; el ducking de
  música/ambiente vuelve a su valor.

### 2.4 Mosquito

`VoiceMosquitoTimbre`: estiramiento WSOLA (granos de 20 ms, búsqueda ±5 ms) + remuestreo con anti-alias, +4
semitonos (f0 y formantes ×1,26: "bicho chico"), aleteo leve (38 Hz, 12 %) y +3 dB de presencia a 2,7 kHz. Nivel
compensado (±1 dB). Latencia fija de 26 ms que se vacía al final de cada stream. Frente al desplazador anterior
(dos cabezas de retardo): −1,5 dB → +0,1 dB de nivel, 6–16 Hz de modulación de envolvente 4–7 dB menor (el
"warble" de ~8 Hz del anterior), mismo tono.

## 3. Contrato con el frente de audio (mixer)

Todavía no existe `docs/v030/AUDIO-CONTRACT.md` en `claude/v030-audio`; la voz quedó parametrizada:

| Tema | Valor actual | Dónde |
|---|---|---|
| Grupo del mixer | se busca por nombre `"Voice"` (`VoicePlayoutStream.MixerGroupName`); si no existe, sale por Master | `VoiceRuntimeCoordinator` |
| Volumen del jugador | parámetro expuesto `VoiceVolume` del grupo (lo escribe Preferencias, sin cambios) | `AlfaApplication.Preferences` |
| Reverb / oclusión de voz | **las dibuja la voz** (por par). El grupo Voice no debe tener envío de reverb propio ni la fuente AudioReverbZones (`bypassReverbZones`) | `VoicePlayoutEngine` |
| Limitador | por voz (≤ −1 dBFS aprox.). Un limitador de Master es bienvenido; no hace falta en el grupo Voice | — |
| Ducking | `MusicVolume` y `AmbienceVolume` −4 dB, 120 ms bajada / 500 ms subida, respetando escrituras nuevas de Preferencias como base | `VoiceRuntimeCoordinator.DuckedParameters` |
| Prioridad de fuente | 8 (alta); la fuente sólo suena con voz | `VoicePlayoutStream.SourcePriority` |
| Criterio de sala | `VoiceRoomSample.FromGeometry` + `VoiceAcousticProbe`: disponible para que los efectos usen el mismo criterio interior/exterior | `Audio/Runtime/VoiceAcousticProbe.cs` |

Si el frente de audio define un snapshot "VoiceActive" o un envío de reverb por zona para Voice, basta con vaciar
`DuckedParameters` o fijar `ReverbSend = 0` en `VoicePeerPresenter`; ninguna otra pieza depende del mixer.

## 4. Pruebas

- EditMode `LetMeSleep.Tests.VoiceEditMode` (nuevas): `VoicePlayoutEngineTests` (arranque ≤ 200 ms sin cortes,
  pérdida 5 % y 10 % sin huecos ni clics, congelamiento de 70 ms puenteado, deriva ±0,4 % sin latencia creciente,
  fin de stream con fundido, borrado con fundido ≤ 6 ms, oclusión con transición suave y ≥ 6 dB + ≥ 6 dB de agudos,
  reverb interior ≥ 10 dB sobre exterior, crossfeed), `VoiceCaptureProcessorTests` (fades de PTT 20/40 ms, AGC
  −40 y −10 dBFS a ±3 dB, compuerta, limitador, guardia de eco), `VoiceSignalQualityTests` (ocultación 8 % sin
  huecos ni clics, pérdida larga → silencio en 60 ms, anti-alias ≤ −40 dB, nivel del mosquito ±1 dB y vaciado de
  cola, SNR del codificador), `VoiceProximityPolicyTests` (curva monótona y suave por rol, cero en el corte,
  1→10 m ≥ 14 dB, oclusión proporcional, histéresis de ruteo), `VoiceAudioSignalTests` (actualizado).
- EditMode `VoiceNetworkTests` (actualizado + nuevos): reordenamiento dentro de la gracia, `StreamClosed`, política.
- PlayMode `LetMeSleep.Tests.VoicePlayMode`: playout real por `OnAudioFilterRead` (paneo, se detiene al callar),
  salida de sala sin audio residual, probe (pared / puerta / sala / exterior), presentador (suavizado de
  oclusión, ruteo asimétrico por rol, cero fuera de alcance), ciclo de vida del clip.
- Evidencia: `LetMeSleep.Tests.VoiceEvidence.V01_VoiceProximityAndEnvironment` (+ el S11 heredado) grabados con el
  harness de auditoría y `tools/voice_evidence/voice_report.py`.

## 5. Cómo reproducir

```bash
# grabar (toma el semáforo global de Unity) y analizar
bash tools/audio_evidence/run_unity_audio_evidence.sh N:/LetMeSleep/Worktrees/v030-voice/unity \
     N:/LetMeSleep/Validation/V030/Voice/rN \
     "LetMeSleep.Tests.VoiceEvidence.VoiceEvidenceScenarios.V01_VoiceProximityAndEnvironment;LetMeSleep.Tests.AudioEvidence.AudioEvidenceScenarios.S11_VoiceProximity"
python tools/voice_evidence/voice_report.py N:/LetMeSleep/Validation/V030/Voice/rN
```

## 6. Parámetros que conviene revisar escuchando

Curva (−4,5 dB/duplicación, referencia 2 m, +8 dB de tope), paredes (−7/−11 dB), envíos de sala (−29 / −12 … −9 dB),
aleteo del mosquito (38 Hz, 12 %), ducking (−4 dB). Son constantes con nombre en `VoiceSpatialPolicy`,
`VoiceRoomSample.FromGeometry`, `VoiceMosquitoTimbre` y `VoiceRuntimeCoordinator`.

## 7. Evidencia (antes / después)

Todo en `N:/LetMeSleep/Validation/V030/Voice/`:

| Carpeta | Qué es |
|---|---|
| `r1-before/` | S11 del harness de auditoría sobre el código previo de esta rama (WAV, JSON, `analysis/summary.md`, `voice_summary.md`) |
| `r2-offline/` | Simulación fuera de Unity con las clases reales (enlace completo con pérdida, jitter, deriva, congelamientos; A/B del timbre con espectrogramas; SNR del codificador; costo de CPU) |
| `r3/` | Primera corrida después (V01 + S11). Expuso la latencia que dejaban las faltas del arranque y artefactos del escenario (caché de salas, física sin sincronizar) |
| `r4/` | **Corrida final** (V01 + S11): `captures/*.wav`, `analysis/voice_summary.md` (50 PASS, 1 FAIL heredado de S11), `analysis/summary.md`, espectrogramas |
| `tests/` | Resultados Unity: EditMode y PlayMode (XML + log) |

S11 (escenario de la auditoría, mismo código de prueba) antes → después:

| Medida | r1-before | r4 |
|---|---|---|
| Voz continua desde el inicio de cada PTT | 859–1 280 ms | 86–214 ms (≤ 197 ms salvo 15 % de pérdida + 80 ms de jitter, fuera de lo pedido: el primer paquete ya llega hasta 110 ms tarde) |
| Huecos en estado estable (18 condiciones) | 20 huecos, hasta 150 ms | 0 |
| Pérdida 5 % + 40 ms | 2 huecos (150 ms) | 0 huecos, 8 cuadros ocultados |
| L/R a ±90°, 3 m | ±21,4 dB (oído opuesto en silencio) | ±15,7 dB (fuente 1,35 m bajo el oído en S11) |
| Interior vs exterior | −0,1 dB | −0,1 dB: S11 usa la API heredada `ApplyAcoustics`, sin capa de entorno (lo mide V01) |

V01 (camino de producción: captura → códec → red simulada → sesión → presentador → playout), r4:

| Medida | Resultado |
|---|---|
| Continuidad | 28 hablas medidas: voz continua a los 44–193 ms, 0 huecos, 0 clics (incluye 5 % + 40 ms, 10 % + 60 ms, 5 % reordenado y congelamiento de 80 ms) |
| Distancia | 1 m −21,8 · 2 m −26,5 · 3 m −29,5 · 5 m −32,6 · 8 m −36,1 · 10 m −46,2 dBFS; 1→10 m 24,4 dB; 12, 15 y 20 m: silencio digital |
| Mosquito | junto al oído −18,9 dBFS y L−R 26,8 dB (vs −22,0 a 1 m); 9 m silencio (corte 8 m); f0 ×1,25; nivel igual al humano (+0,0 dB) |
| Dirección | L/R ±19,0 dB a ±90°; detrás −1,3 dB y −2,9 dB de agudos; mosquito girando alrededor de la cabeza: paneo r = 0,97 |
| Oclusión (4 m) | pared −7,9 dB y −24,7 dB extra >3,5 kHz; puerta abierta −1,9 dB; puerta cerrada ≈ pared (+0,8 dB); dos paredes a 6 m −15,1 dB vs abierto a 4 m |
| Interior / exterior | ráfaga de 60 ms: cola tardía/temprana −33,0 dB fuera, −13,0 dB en cuarto 5×3×6 m (+19,9 dB), −9,4 dB en salón (EDT 78 → 314 ms, T20 419 → 799 ms) |
| PTT (5 pulsaciones) y salida de la sala | 0 clics, 0 cortes duros; residual tras salir: silencio digital |
| Mezcla | pico verdadero −3,6 dBTP, 0 muestras recortadas |

Pruebas Unity 6000.3.24f1 (`tests/`): EditMode 519/519 (`editmode-01`, 30 de `LetMeSleep.Tests.VoiceEditMode`)
y PlayMode 34/34 (`playmode-01`: voz + UI que presenta la voz; el playout real corrió en el hilo de audio, no se
ignoró). Las corridas finales `editmode-02` / `playmode-02` repiten todo con el código definitivo.

Límites de la evidencia: captura en tiempo real del listener en `-batchmode` (sin HRTF, estéreo); voz sintética,
no humana; red simulada; sin EOS ni micrófono. La latencia medida se toma desde el reloj de audio capturado y puede
quedar 20–40 ms por debajo del reloj de pared (igual antes y después).

## 8. Pendiente (no declarado como hecho)

- Prueba real con dos personas en redes distintas (EOS P2P/relay, NAT, pérdida y jitter reales). **Pendiente.**
- Micrófonos reales (niveles, ruido, permisos de Windows, USB/Bluetooth conectados y desconectados en caliente).
- Escucha humana en auriculares y parlantes (inteligibilidad del mosquito, cantidad de reverb, curva).
- Cancelación acústica de eco real (hoy: compuerta que sube con la voz remota) y un códec mejor que IMA ADPCM
  (Opus requeriría una dependencia externa; decisión de Director).
- La oclusión es geométrica (rayos), sin difracción: una puerta abierta fuera de la línea directa cuenta como pared.
