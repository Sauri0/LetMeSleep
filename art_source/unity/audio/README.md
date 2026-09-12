# Fuentes de audio alfa

`generate_alfa_audio.py` es la fuente editable y determinista del primer set
audible. Define partitura, tempo, voces, envolventes y semillas; no descarga ni
incorpora samples externos.

Desde la raíz del repositorio:

```powershell
python art_source/unity/audio/generate_alfa_audio.py
python art_source/unity/audio/generate_alfa_audio.py --verify
```

El primer comando produce WAV PCM 48 kHz/24 bit en `generated/`, copia bytes
idénticos a `unity/Assets/LetMeSleep/Audio/Clips/` y actualiza
`audio_manifest.json` con duración, canales y SHA-256. Los WAV de runtime se
comprimen mediante import settings de Unity; los WAV fuente permanecen aquí.

`MUS_NightMischief_Menu` es un loop original de ocho compases a 96 BPM con
pizzicato, bajo, voz tipo clarinete y escobillas sintéticas. El resto cubre
ambiente nocturno, alas, defensa, contacto, picadura y ready de UI. Son
prototipos alfa editables: cualquier reemplazo conserva ID, licencia, puntos
de loop, 48 kHz y el gate de mezcla definido en `docs/unity/presentation/`.
