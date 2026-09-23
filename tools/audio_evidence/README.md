# Harness de escucha objetiva (audio y voz)

Graba **lo que oye el AudioListener** del juego (todas las AudioSource, grupos del
`LMS_AlfaMixer`, filtros y voz) mientras un escenario controlado ocurre, y mide la
grabación con números reproducibles. Sirve para comparar el estado actual contra
cualquier cambio de cualquier frente (mapas, personajes, animación, audio, voz).

No sustituye la escucha humana en parlantes y auriculares: la complementa con
evidencia objetiva (paneo, atenuación, oclusión, colas, loudness, picos, clics,
huecos, voces simultáneas).

## Piezas

| Pieza | Dónde | Qué hace |
|---|---|---|
| Escenarios PlayMode | `unity/Assets/LetMeSleep/Tests/AudioEvidence/` (asmdef `LetMeSleep.Tests.AudioEvidence`, `defineConstraints: UNITY_INCLUDE_TESTS`, ensamblado de tests: no entra al build) | 12 escenarios scriptados con los assets reales (`LMS_AlfaAudioRoot`, cues, mixer) y el volumen por defecto de Preferencias. Sin `--lms-audio-evidence` se marcan *Ignored*. |
| `AudioEvidenceRecorder` | idem | Captura la mezcla final: `OnAudioFilterRead` en el listener (tiempo real, usado en batch) o `AudioRenderer` (offline a 60 fps de captura; en `-batchmode` no entrega muestras y cae solo al tap). Escribe WAV float32 + JSON (segmentos, eventos, conteo de voces reales/virtuales por frame, trayectorias). Limitación medida: el tap no registra `AudioReverbZone`; sí filtros por fuente y efectos del mixer. |
| `lms_audio_evidence.py` | `tools/audio_evidence/` | Analizador (numpy + ffmpeg): `clips`, `captures`, `report`, `selftest`. Loudness BS.1770/EBU R128 propio (validado contra `ffmpeg ebur128`, diferencia ≤ 0,1 LU/dB), true peak 4×, clics, cortes duros, huecos, costuras de loop, L/R, HF, centroides, EDT/T20, f0, pitch/doppler, similitud espectral, espectrogramas. |
| `targets.json` | idem | Objetivos numéricos (PASS/FAIL del reporte). |
| `run_unity_audio_evidence.sh` | idem | Toma el semáforo global de Unity (máx. 3), corre los escenarios, analiza y reporta. |

## Uso rápido

```bash
# 1) Grabar todos los escenarios de un proyecto/worktree y analizarlos
bash tools/audio_evidence/run_unity_audio_evidence.sh \
     N:/LetMeSleep/Worktrees/<worktree>/unity  N:/LetMeSleep/Validation/V030/Audio/runs/<nombre>
#    (opcional 3er arg: -testFilter, p.ej. LetMeSleep.Tests.AudioEvidence.AudioEvidenceScenarios.S11_VoiceProximity)
#    (opcional 4to arg: filter (por defecto) | renderer; en -batchmode de Unity 6000.3.24f1
#     AudioRenderer no entrega muestras y el grabador cae solo a filter)

# 2) Sólo análisis de clips (no necesita Unity)
python tools/audio_evidence/lms_audio_evidence.py clips unity/Assets/LetMeSleep/Audio/Clips \
       --out <dir> --unity unity --spectrograms

# 3) Reporte combinado
python tools/audio_evidence/lms_audio_evidence.py report --clips <dir> --captures <run>/analysis --out <dir>

# 4) Validar el medidor contra ffmpeg
python tools/audio_evidence/lms_audio_evidence.py selftest <archivo.wav> [...]
```

Argumentos Unity (después de `-runTests ... -testCategory AudioEvidence`):
`--lms-audio-evidence <dir absoluto>` (obligatorio), `--lms-audio-capture renderer|filter`,
`--lms-audio-mixer defaults|unity` (defaults = Master .8, Música .5, Efectos .85, Voz .8
como un perfil nuevo; unity = todos los faders a 0 dB). No usar `-nographics` (los mapas
instancian agua GPU) ni `-quit`.

## Escenarios

| Test | Captura | Qué ejercita |
|---|---|---|
| S01_SpatialPan | `s01_spatial_pan` | Ruido rosa (ajustes de cue `HumanFootstep`) a 2 m en 10 azimuts + 7 cues reales a −90/0/90/180°. L/R, frente/atrás. |
| S02_DistanceAttenuation | `s02_distance` | Ruido rosa con los ajustes de 6 cues a 0,5–20 m + loop real de alas. Curvas nivel/distancia. |
| S03_MosquitoFlight | `s03_mosquito_flight` | Zumbido real: órbita r=1,5 m, fly-by a 5 m/s, aproximación 12→0,4 m, enjambre de 9 con la regla del presentador (6 más cercanos ≤ 8 m, 30 Hz). Seguimiento de paneo, doppler, clics al entrar/salir. |
| S04_SwatterLeftRight | `s04_swatter` | Swing+impacto+caída propios y de otro humano a izquierda/derecha a 3 y 8 m. |
| S05_Footsteps | `s05_footsteps` | Pasos propios y de otro humano en madera/baldosa/tela + salto/aterrizaje. Variación entre pasos, distinción de materiales. |
| S06_DoorBehindWall | `s06_door_occlusion` | Puerta a 4 m abierta, tras pared, dentro de cuarto cerrado; control positivo con LPF 1,2 kHz −6 dB. |
| S07_InteriorExterior | `s07_interior_exterior` | Impulso, impacto y puerta al aire libre, en cuarto cerrado y control con `AudioReverbZone`. EDT/T20, cola. |
| S08_MapAmbience | `s08_map_ambience` | Menú + 5 mapas Higgsfield: ambiente de cada uno, emisores locales, fogata/chimenea, materiales de pisada que resolvería el presentador y su costo, silencio tras `StopAll`. |
| S09_MusicFlow | `s09_music_flow` | Menú → sala → personalización → menú → ronda (como `AlfaApplication`) → urgencia → resultados → sala → costura del loop → `StopAll`. |
| S10_UiClicks | `s10_ui` | UI select/confirm/error/ready, ráfaga (throttle 65 ms), UI sobre música. |
| S11_VoiceProximity | `s11_voice` | Voz sintética de habla por `VoicePlayoutStream` + códec IMA ADPCM + `VoiceJitterBuffer` + `VoiceSpatialPolicy`: distancias, lados, pared, interior, control con reverb, mosquito (timbre), pérdida/jitter, emisor en movimiento. |
| S12_MixStress | `s12_mix_stress` | Combate denso: 6 loops de alas + ráfagas de impactos/picaduras/pasos + urgencia + resultado. Picos, clipping, voces reales/virtuales. |

Para agregar un escenario: un `[UnityTest]` en `AudioEvidenceScenarios` que llame
`AudioEvidenceStage.Begin("<nombre>")`, `StartRecording()`, marque intervalos con
`Recorder.Segment(label, kind, start, end, params)` y termine con `Finish()`. El
`kind` elige las métricas del analizador (`pan`, `distance`, `orbit`, `flyby`,
`approach`, `swarm`, `footsteps`, `occlusion`, `reverb`, `ambience`, `local_emitter`,
`music`, `sting`, `transition`, `loop_seam`, `ui`, `voice`, `stress`, `silence`).

## Salidas

`<run>/captures/*.wav|json` (grabaciones), `<run>/analysis/captures.json` (métricas por
segmento), `summary.md` y `checks.json` (PASS/FAIL/INFO contra `targets.json`) y
`spectrograms/`. Las grabaciones son float32 estéreo a la frecuencia de salida de Unity.

Nota: en modo `filter` la captura es en tiempo real y Unity también reproduce el audio por el
dispositivo de salida por defecto (se oye en parlantes/auriculares mientras corre la suite, ≈15 min).
