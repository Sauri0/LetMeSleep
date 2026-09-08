# Let me sleep 0.7 — candidato sin publicar

Se está verificando un candidato para Windows, con el proyecto Godot y las fuentes Blender y de audio. No se declara cerrada la versión ni validado el objetivo de 60 FPS. La guía de uso está en [distribution/LEEME.md](distribution/LEEME.md) y las comprobaciones con sus límites en [distribution/PRUEBAS.md](distribution/PRUEBAS.md).

## Descargar y jugar

Al preparar el paquete local se incluyen `Let-me-sleep-0.7.0-Windows.zip`, `Let-me-sleep-0.7.0-fuentes.zip` y el vídeo `Let-me-sleep-0.7-objetos.mp4`. Las versiones efectivamente publicadas aparecen en [Releases](https://github.com/Sauri0/LetMeSleep/releases); crear estos paquetes no los publica.

Descomprimí toda la carpeta y abrí **Let-me-sleep.exe**. Práctica permite elegir ambos personajes y los tres modos sin servidor externo. Todos los jugadores de una sala deben usar la misma versión y protocolo 8.

## Cambios principales

- Mejillas humanas continuas y piezas faciales combinables; bigotes, barbas y color de pelo compartido con cejas.
- Humano A compacto y mosquito B alargado, ojos, boca y cejas combinables con expresiones animadas y contacto coherente con la pose física.
- Acabado liso en paredes y suelo, conservando paletas por habitación, juntas y materiales de objetos.
- Dieciséis ambientes domésticos renovados, puertas interactivas, barandas, ventanas nocturnas e iluminación local.
- HUD compacto, guía con F1 e indicaciones de puerta y golpe junto a la mira.
- Aturdimiento de 35 segundos en Sangre y Tareas, con ayuda entre mosquitos a velocidad 4×; eliminación en Supervivencia.
- Cinco herramientas con agarres y gestos propios; diario y pantufla arrojables, recuperables y con sonidos confirmados.
- Portaescobas y bancos bajos coherentes con el entorno doméstico; unión de marcos corregida.
- Oclusión estática de paredes/pisos para reducir trabajo de renderizado, conservando los huecos.
- Ajustes de resolución real, pantalla completa, VSync, FPS, sombras y reflejos.

## Fuentes y reproducción

Abrí `game/project.godot` con Godot 4.5.2. `work/build.ps1` importa, verifica y exporta Windows; `work/verify-release07.ps1` ejecuta comprobaciones del EXE, red y rendimiento. `work/package.ps1` crea el ZIP sin sobrescribir uno existente.

`art_source/characters/` contiene los personajes, sus clips de pose y las herramientas. Los nombres internos `human_lms06` y `mosquito_lms06` se conservan por compatibilidad de referencias; sus contenidos corresponden a esta entrega. `art_source/environments/house/v07/` incluye los 32 modelos nuevos de la casa y su manifiesto. `art_source/environments/README-0.7.md` explica unidades, bisagras, materiales y regeneración. Las fuentes musicales y sonoras y sus licencias están en `art_source/audio/`.

Las mediciones proceden del equipo disponible, RTX 3060 Ti y Ryzen 5 5600X. La GTX 1660 Ti sigue siendo un objetivo sin certificación. La conexión directa necesita una ruta de red alcanzable; no se ha integrado relay ni probado conexión entre casas. El reflejo del baño es estático y no reproduce al jugador como un espejo plano. La versión 0.6 publicada se conserva.
