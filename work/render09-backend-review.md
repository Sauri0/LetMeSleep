# Diagnóstico de renderizadores — fuente fit3

8 de septiembre de 2026. No se modificó el renderizador predeterminado del
proyecto. Compatibility sigue vigente; Forward+/Vulkan se probó por CLI.

`render09_backend_probe.gd` captura la misma casa generada (semilla 1), tres
cámaras y textura 1920×1080, sin actores ni simulación. Ambos procesos nativos
terminaron con salida 0 y stderr vacío. Godot 4.5.2 / RTX 3060 Ti.

| Vista | Draw calls Compatibility / Forward+ | CPU render mediana, ms |
|---|---:|---:|
| Habitación | 429 / 327 | 1,376 / 0,895 |
| Pasillo | 1762 / 516 | 3,770 / 1,021 |
| Pasillo superior | 1437 / 204 | 3,174 / 0,594 |

Los seis PNG e informes están en `outputs/0.9-render-backend-compat-fit3` y
`outputs/0.9-render-backend-forward-fit3`. El hash de mapa y las cámaras son
idénticos. La apariencia cambia: techo, zócalos, brillo de pared y muebles son
distintos. Esta prueba acredita un coste menor del renderizado estático en este
equipo; no acredita paridad visual ni FPS de gameplay. No justifica migrar el
proyecto sin revisar iluminación, personajes y compatibilidad de hardware.

El ensayo usa la hipótesis de la arquitectura oficial de Godot 4.5: Forward+
dispone de agrupación de luces e instanciación automática; Compatibility puede
añadir pasadas para luces con sombras. No se atribuye cada diferencia medida a
una única función interna sin perfil de GPU.

Fuente: https://docs.godotengine.org/en/4.5/engine_details/architecture/internal_rendering_architecture.html

## Partida real preparada: 16 actores, casa generada, 1080p

El fixture real Main/Client/Practice conserva calidad, bots, física a 60 Hz y
órdenes periódicas de puertas. Cada proceso mide 12 s tras 2 s de preparación.
Las tres identidades de mapa coinciden. Se registran método y driver reales.

| Renderizador | Cuadro p50 / p90 / p99, ms | Sobre 16,67 ms |
|---|---:|---:|
| Compatibility | 48,241 / 94,347 / 123,682 | 80,82 % |
| Forward+ | 17,936 / 36,755 / 60,133 | 54,20 % |

Informes válidos: `perf09-catalog-compat-fit3-clean.json` y
`perf09-catalog-forward-fit3.json`, con registros `release07-*.run.json`.
Ambos terminaron con salida 0 y stderr vacío. No hubo otro motor ni consumidor
intensivo del equipo durante los ensayos. Las rutas adaptativas y el número
de órdenes efectivas (136/135) difieren; no es replay determinista ni permite
atribuir una mejora porcentual causal. Ninguno cumple 60 FPS sostenidos.

El primer ensayo Compatibility, `perf09-catalog-compat-fit3.json`, dejó recursos
al cerrar y se conserva como intento rechazado. Se cambió sólo el cierre del
fixture a `quit.call_deferred()` para liberar las referencias de su coroutine;
las dos ejecuciones posteriores terminaron limpias.

Estos ensayos incluyen DoorCatalog candidato y prendas fit3; no son evidencia
del EXE anterior ni una comparación aislada de la optimización de puertas.
