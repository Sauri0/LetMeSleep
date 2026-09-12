# Procedencia de audio recuperado para Unity

Los archivos `MUS_Legacy_*`, `AMB_Legacy_*`, `SFX_Legacy_*` y
`UI_Legacy_*` en `Assets/LetMeSleep/Audio/Clips` son copias binarias de la
biblioteca anterior en `game/assets/audio`. Se recuperan para conservar la
identidad sonora y las señales de alfa durante la migración a Unity.

La fuente registra 67 archivos OGG generados para el proyecto a partir de
VSCO 2 Community Edition. Esa biblioteca se distribuye bajo CC0 1.0; el texto
completo está en `game/assets/audio/LICENSE-VSCO2CE-CC0.txt`. La procedencia,
duración, formato y SHA-256 individual están en
`game/assets/audio/manifest.json`; el commit de producción declarado allí es
`440300901dfe9275fd84e0b7763af1f8443ae62e`.

El builder de Presentation sólo referencia el subconjunto con uso real en
alfa: stems de menú/ronda, tema tranquilo, aire nocturno, zumbidos por estado,
contacto al posarse/desprenderse, defensa, picadura, pasos, aterrizaje,
recuperación y señales de interfaz. Los clips de tareas y herramientas de
fases posteriores permanecen fuera del runtime Unity.
