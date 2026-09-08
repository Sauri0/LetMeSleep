# Let me sleep — fuente de audio 0.7

Tema principal **Pasos de puntillas**, 104 BPM, 4/4, 32 compases (73,846 s). Variante tranquila **La casa bosteza**, 80 BPM, 24 compases (72 s). La escritura es original: bajo con notas de enlace, acordes con sextas/novenas, motivo de llamada y respuesta, silencios, articulación, swing moderado y variaciones de frase, dinámica y panorama.

La partida tiene tres capas estéreo con exactamente 3.256.615 muestras cada una: base de contrabajo/pizzicatos, percusión suave y motivo melódico. Menú y lobby usan otras tres capas del arreglo principal; el lobby atenúa percusión y melodía. Personalización/ajustes puede usar la variante tranquila. Seis acentos breves: inicio, aturdimiento, recuperación, tarea, victoria y derrota.

La biblioteca incluye 38 efectos y ambientes mono. Hay modelos diferenciados para vuelo/posado/picadura, movimiento de golpe/impacto, las cuatro herramientas, retirada, ropa, aterrizaje, pasos de madera/baldosa/tejido, tareas, interfaz, caída/aturdimiento/ayuda/recuperación y fuentes domésticas. El zumbido y el Foley son síntesis original; no se presentan como grabaciones de mosquitos o personas reales.

## Material editable y procedencia

- `compose.py`: composición por eventos, sampler acústico offline, modelos de Foley, arreglo, panorama, reverberación discreta y exportación.
- `masters/music/`: WAV completos y stems, partituras de eventos JSON y MIDI de los arreglos. La interpretación final usa el sampler, no un banco GM; el MIDI es una referencia editable de notas/tiempo/articulación.
- `masters/sfx/`: WAV mono de cada efecto, sin pérdida.
- `accent-events.json`: notas/tiempos de los seis acentos.
- `instruments/vsco2ce/`: 24 notas acústicas de contrabajo, viola pizzicato, clarinete, marimba y glockenspiel. Son grabaciones CC0 de VSCO 2 CE de Versilian Studios, Sam Gossner y Simon Dalzell; corte de muestras por Elan Hickler/Soundemote.
- `sample-provenance.json`: URL primaria fijada a commit, hash y licencia de cada muestra. `instruments/vsco2ce/LICENSE-CC0.txt` contiene la licencia completa.
- `game/assets/audio/manifest.json`, `CREDITS.txt` y `LICENSE-VSCO2CE-CC0.txt`: manifiesto de los 51 OGG y procedencia para distribución.

Fuentes primarias: [VSCO 2 Community Edition](https://versilian-studios.com/vsco-community/) y [licencia del repositorio](https://github.com/sgossner/VSCO-2-CE/blob/440300901dfe9275fd84e0b7763af1f8443ae62e/LICENSE). No se usaron servicios de generación externos, cuotas ni suscripciones.

## Reproducir

Desde la raíz del proyecto, con Python 3.14, NumPy y FFmpeg/ffprobe disponibles:

```powershell
python art_source/audio/fetch_samples.py
python art_source/audio/compose.py --sketches
python art_source/audio/compose.py --sfx
python art_source/audio/compose.py --music
python art_source/audio/doors07.py
python art_source/audio/validate_assets.py
```

Las muestras incluidas permiten trabajar sin volver a descargarlas. `fetch_samples.py` recupera únicamente faltantes desde el commit fijado. Semillas, notas y decisiones de arreglo son deterministas; diferentes versiones de FFmpeg pueden cambiar bytes del contenedor OGG. Los WAV son la referencia de mezcla sin pérdida. No se necesita Python, FFmpeg ni los WAV de instrumentos para ejecutar el juego.

## Integración y pruebas

`AudioCatalog.ensure_buses()` crea Music, Effects, Ambience y UI bajo Master y aplica preferencias. `MusicDirector.set_context(screen,snapshot,personal,local_id)` conserva el cursor musical en pantallas del mismo grupo, funde entre temas y actualiza capas al compás. Su tensión sólo depende de reloj, plazo propio, acciones propias y percepción local; nunca de asignaciones libres ni ubicaciones de otros actores. El volumen de enjambre tampoco depende de enemigos ocultos.

`AudioFx.setup/sync/clear` conserva su API; `set_context(map_id)` sitúa fuentes domésticas y `sync_private(personal)` maneja únicamente carga/ayuda y tareas propias. Los zumbidos activos se limitan a seis fuentes cercanas, los efectos a doce voces. Fuentes de insectos tras paredes/pisos quedan silenciadas. Las transiciones de estados confirmados no se repiten con snapshots duplicados; salida, ausencia de datos y muerte de Supervivencia detienen las voces.

Pruebas: `game/tests/audio06_test.gd` (entrada anterior `audio_checks.gd` conservada) y `game/tests/music06_test.gd`. La segunda registra salida real del bus Master de Godot en `work/audio06-godot-mix.wav`, comprueba PCM no nulo, transiciones, sincronización, separación de buses y margen de saturación. Esto es validación técnica de la mezcla; no sustituye la escucha subjetiva en distintos parlantes o auriculares ni certifica rendimiento de hardware externo.

## Puertas 0.7

`doors07.py` genera tres efectos originales mediante resonancias amortiguadas, fricción armónica y ruido filtrado: giro de bisagra, pestillo y obstrucción. WAV editables bajo masters/sfx; reproducción espacial en Effects desde cambios de estado confirmados. Primer snapshot, repetición de snapshot y entrada a otra sala no disparan sonidos. No se usaron nuevas muestras externas.
