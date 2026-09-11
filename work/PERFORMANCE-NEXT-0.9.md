# Rendimiento después de la candidata 0.9.0-rc.1

El 11 de septiembre se verificó que la instancia abierta desde Desktop tenía
el mismo SHA256 que el EXE publicado: E5928D76F18F14B7573D135A88340B1C9F0237AE7EC468A1AAF29B41F6002D1E.
No se lanzó una medición simultánea ni se cambió esa instancia.

## Evidencia previa, no una medición del EXE publicado

`perf09-guards-natural-16-1080.json`: fuente 0.9, casa house-v1-1, 16 actores,
RTX 3060 Ti / Ryzen 5600X, 1080p, sombras 2, sin VSync/tope, 369 muestras.
Mediana 27.533 ms; p90 61.344 ms; p99 79.018 ms. El 75.34% de los cuadros
superó 16.67 ms. Física mediana 14.084 ms; render GPU p90 22.296 ms. No basta
con reducir sólo gráficos ni con aumentar el límite de FPS.

`perf09-view-attribution.json` identifica tareas candidatas: consultas de foco,
movimiento de mosquitos, rutas de bots y actualización de poses humanas. Es un
perfil instrumentado con sobrecoste: sus tiempos no sustituyen la medición
normal ni pueden sumarse sin descontar llamadas anidadas. Algunos cambios
posteriores de audio/HUD ya redujeron trabajo; volver a medir antes de editar.

## Próximo experimento

`measure-candidate09.ps1` compara en secuencia dos jugadores, dieciséis y
dieciséis sin sombras, sobre el mismo mapa y EXE. Rechaza arrancar si hay otro
juego/editor abierto y conserva el hash y la calidad real de cada muestra.
Quitar sombras es diagnóstico, no la propuesta visual para la versión final.

1. Medir el EXE publicado cuando el usuario no esté jugando.
2. Si domina física, perfilar consultas de foco/rutas con la misma semilla;
   conservar 60 Hz de autoridad, hitboxes, reglas de picadura y salida exacta.
3. Si domina dibujo, medir cantidad de draws y sombras por habitación;
   evitar apagar sombras a distancia mientras la lámpara sigue iluminando
   paredes adyacentes, porque produciría fugas de luz.
4. Validar una mejora por vez mediante equivalencia y repetición sin perfilador.

No se cambió el runtime ni el ZIP publicado. Siguen pendientes GTX 1660 Ti,
1440p/4K en hardware adecuado y partida real entre casas.
