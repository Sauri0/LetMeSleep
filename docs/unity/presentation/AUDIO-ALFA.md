# Audio 0.9.4/alfa

## 1. Identidad sonora

La música comunica travesura nocturna: jazz ligero con pizzicato, contrabajo,
clarinete/marimba suave, escobillas y pequeños acentos de madera. El pulso base
es `96 BPM`, 4/4 con swing moderado. Evitar metales estridentes, subgrave
continuo y percusión densa, porque compiten con zumbido, palmada y confirmación
de extracción.

Contenido musical mínimo de alfa:

| ID | Entrega | Duración/formato |
|---|---|---|
| `MUS_NightMischief_Menu` | tema original de menú/lobby | loop estéreo de 64 compases, intro de 2 compases |
| `MUS_NightMischief_Round` | versión reducida para ronda | loop estéreo de 32 compases, menos melodía |
| `STG_RoundStart` | entrada de ronda | 0.8–1.4 s |
| `STG_HumansWin` | cierre humano | 1.5–2.5 s |
| `STG_MosquitoesWin` | cierre mosquito | 1.5–2.5 s |

Las piezas deben ser originales o incluir licencia y autor verificables. Entrega
fuente WAV `48 kHz / 24 bit`, puntos de loop en muestras y stems conservados
fuera del runtime. La versión alfa no incorpora voz espacial ni filtros de voz;
eso pertenece a omega.

## 2. AudioMixer

Un mixer `LMS_AlfaMixer` con esta jerarquía:

```text
Master (0 dB; material masterizado a true peak <= -1 dBFS)
├── Music (-10 dB)
├── Ambience (-8 dB)
│   ├── Exterior
│   └── Interior
├── SFX (0 dB)
│   ├── Critical (0 dB)
│   ├── Character (-1 dB)
│   │   ├── Human
│   │   └── Mosquito
│   ├── World (-2 dB)
│   └── Foley (-3 dB)
└── UI (-3 dB)
```

`Critical` contiene confirmaciones que cambian una decisión inmediata:
`StrikeImpact`, `MosquitoKnockedDown`, `BiteStarted`, extracción completa,
`HumanFainted`, inicio y fin de ronda. Un Send de `Critical` controla Duck
Volume en Music y Ambience:

- Music: threshold `-28 dB`, ratio `6:1`, attack `35 ms`, release `420 ms`,
  reducción máxima objetivo `-5 dB`.
- Ambience: threshold `-26 dB`, ratio `4:1`, attack `25 ms`, release `280 ms`,
  reducción máxima objetivo `-3 dB`.

Unity permite enrutar AudioSources a grupos, encadenar efectos, capturar
snapshots y usar ducking, según [Audio Mixer](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioMixer.html).

Snapshots:

| Snapshot | Music | Ambience | SFX | Transición |
|---|---:|---:|---:|---:|
| `Menu` | -8 dB | -14 dB | -2 dB | 0.50 s |
| `Lobby` | -10 dB | -10 dB | 0 dB | 0.75 s |
| `RoundCalm` | -14 dB | -7 dB | 0 dB | 1.20 s |
| `RoundThreat` | -18 dB | -9 dB | +1 dB | 0.25 s |
| `Paused` | -24 dB | -18 dB | -12 dB | 0.15 s |
| `Results` | -9 dB | -16 dB | -2 dB | 0.40 s |

Los valores son atenuaciones de grupo relativas al mismo material master. La
mezcla se acepta por medidor y prueba A/B, no porque la suma de dB coincida.
Pico master máximo `-1 dBFS`, loudness integrado de música `-20 LUFS ±2` y
ningún evento Critical por debajo de `-16 LUFS short-term` en su ventana útil.

## 3. Fuentes 2D/3D

Toda fuente sale a un mixer group explícito. Música, UI y stings de resultado
son 2D. Mundo, Foley y personajes usan `spatialBlend=1`, clips mono y rolloff
Custom. `dopplerLevel=0` salvo prueba posterior: el cambio de pitch del mosquito
se diseña en el clip, no depende de velocidad de red.

| Familia | Min | Max | Priority | Simultáneos |
|---|---:|---:|---:|---:|
| Critical | 0.7 m | 22 m | 24 | 8 |
| Zumbido/alas mosquito | 0.35 m | 12 m | 56 | 12 |
| Defensa/palmada | 0.8 m | 18 m | 40 | 8 |
| Pasos humano | 0.7 m | 15 m | 80 | 12 |
| Puertas/props | 0.8 m | 20 m | 96 | 10 |
| Ambiente puntual | 2.0 m | 28 m | 160 | 6 |
| Música/UI 2D | n/a | n/a | 32/48 | 4/8 |

En Unity, `spatialBlend=1` aplica atenuación y dirección 3D; Min Distance define
el radio de volumen pleno y Max Distance el fin de la curva, según
[AudioSource](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioSource.html).

Curva custom inicial para fuentes de gameplay, normalizada por Max Distance:

```text
distance: 0.00  0.08  0.25  0.50  0.75  1.00
volume:   1.00  0.92  0.68  0.36  0.14  0.00
```

El zumbido usa dos capas en un solo prefab: loop de alas estrecho y cuerpo
tonal más bajo. El loop cambia volumen/pitch suavemente con velocidad visual,
con pitch limitado a `0.92–1.12`; nunca reinicia al cambiar de estado. La capa
de amenaza se activa sólo por el estado autoritativo y distancia válida.

Oclusión alfa para SFX 3D mediante linecast local contra `WorldStatic` y puertas
cerradas, actualizado a `10 Hz` por fuente audible:

| Cruces | Volumen | Low-pass |
|---|---:|---:|
| 0 | 0 dB | 22000 Hz |
| 1 pared/puerta | -5 dB | 4800 Hz |
| 2 o distinto piso | -10 dB | 2200 Hz |

Interpolar volumen/cutoff en `120 ms`. Ambience usa zonas, no un raycast por
emisor. El sistema limita consultas a 24 fuentes por tick y prioriza Critical.

## 4. Catálogo de eventos alfa

| Evento sonoro | Capa | Variantes mínimas | Fuente de disparo |
|---|---|---:|---|
| Footstep Human | Foley | 4 por superficie | cruce cosmético de pie durante `MotionPhase`, dedupe por `StateRevision` |
| Jump/Land | Foley | 2/3 | transición autoritativa de `Grounded`/`MotionPhase`; Land escala con velocidad |
| Wing loop | Mosquito | 2 capas | estado continuo del actor; loop sin clicks |
| Perch/Detach | Mosquito | 3/3 | cambio de `SurfaceAttachment` |
| Bite start/loop/stop | Critical/Mosquito | 3/1/3 | `BiteStarted`, `BiteAttachment`, `BiteEnded`; salida <=40 ms |
| Defend swing | Human | 4 | `StrikeStarted`; anticipa sin confirmar impacto |
| Defend hit | Critical | 4 | sólo `StrikeImpact` autoritativo |
| Mosquito hit/fall | Critical/Mosquito | 4/3 | `MosquitoKnockedDown` y cambio de `LifeState` |
| Human faint/recover | Critical/Human | 3/3 | `HumanFainted`, `RecoveryStarted` y `Recovered` |
| Door open/close | World | 4/4 | `DoorChanged`, en la posición real de puerta |
| Round start/end | Critical/2D | 1/2 | transición de fase Core / `RoundEnded`, una vez por secuencia |

Los sonidos derivados de snapshot son cosméticos y no emiten eventos de
gameplay. Al recibir un snapshot más nuevo, reconstruyen loops y estados; no
reproducen one-shots históricos. El inicio de ronda proviene de la transición
de fase de Core porque no existe un `GameplayEvent` `RoundStarted` en el
contrato alfa.

No hay sonido ni VFX de “marca de picadura”. BiteStart comunica contacto por
posición real, alas y reacción del humano; no revela una zona dibujada.

## 5. Presets de importación

| Tipo | Load Type | Formato | Canales | Sample rate | Preload |
|---|---|---|---|---|---|
| One-shot crítico <=1.5 s | Decompress On Load | PCM | mono | Preserve 48 kHz | sí |
| Foley/impacto frecuente <=5 s | Decompress On Load | ADPCM | mono | Optimize | sí |
| Ambiente medio 5–30 s | Compressed In Memory | Vorbis q=0.65 | mono/estéreo según campo | Optimize | sí |
| Música/ambiente largo | Streaming | Vorbis q=0.75 | estéreo | Preserve 48 kHz | no; Load In Background |

Unity distingue `DecompressOnLoad`, `CompressedInMemory` y `Streaming`; este
último minimiza memoria usando un buffer y thread de streaming, según
[AudioClipLoadType](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioClipLoadType.html).

Fuentes WAV conservan cola limpia y cruce de loop. Cada clip incluye ID, grupo,
licencia, duración, loudness integrado, true peak, punto de loop y hash. Clips
espaciales se importan mono; `Force To Mono` sólo se usa si una escucha A/B
confirma que no introduce cancelación.

## 6. Pooling y fallos

- Pools preasignados: 12 Character, 12 Foley, 10 World, 8 Critical y 6
  Ambience. Una voz de menor prioridad termina antes de crear una fuente nueva.
- `PlayOneShot` no puede saltar el mixer group ni el límite de voces.
- Evento repetido por `(SessionEpoch,RoundId,EventId)` se descarta.
- Si falta variante, usar una variante válida del mismo evento; si falta todo
  el evento Critical, registrar ID una vez y mantener la ronda funcional.
- Pausa transiciona a snapshot `Paused`; no cambia pitch global ni tiempo de
  clips Critical ya confirmados.
- Cambio de escena conserva el mixer y música, pero libera pools 3D del mapa.

Gate alfa: `<=48` Audio Voices simultáneas, `<=64` Playing Sources, memoria de
audio `<=96 MB`, Audio DSP CPU mediana `<=8%` y P95 `<=15%` en el hardware de
referencia. El Audio Profiler expone voces, fuentes, memoria y CPU para esta
medición.
