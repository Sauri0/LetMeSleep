# Let me sleep 0.9.0 — estado del candidato

8 de septiembre de 2026. Fuente Godot 4.5.2, protocolo 9 e invitaciones DD5.
Este documento está en preparación: todavía no certifica un EXE 0.9.0.
Los resultados anteriores de 0.6/0.7 permanecen con sus paquetes históricos y
no se atribuyen a esta versión.

## Verificaciones de fuente completadas

- Aturdimiento de 35 segundos y ayuda continua 4× fuera de Supervivencia;
  HUD compacto y controles contextuales.
- Orientación de superficies: 213 comprobaciones de autoridad, 149 nativas y
  53 de ENet; cámara 1680. Patas: tres estilos con 18776 comprobaciones por
  estilo, sin penetraciones en los recorridos de esquina preparados.
- Casas: 1000 semillas y 1000 repeticiones en procesos independientes,
  16–24 habitaciones y dos o tres pisos. Tareas 86; mobiliario 2490.
- Puertas: 2578 equivalencias de cierre, 17117 de consultas de colisión y
  342 regresiones. Las mejoras de CPU se midieron en replays idénticos;
  sus porcentajes no representan una mejora equivalente de FPS.
- Voz: PCM 4758, transporte de producción 212, Session 77 con captura
  sintética/Dummy. Contexto de UI 38 y selector 27. La sesión comprobó
  conservación de duración, tono agudo del mosquito y fin de frase, con enlace
  estabilizado previamente. No prueba arranque frío en cualquier red.
- El micrófono real no se abrió durante estas automatizaciones. Los checks
  iniciados desde el ejecutable reciben explícitamente --no-microphone.

## Cierre pendiente

La prenda humana se corrigió en su fuente editable y en el GLB; los últimos
nueve casos de cuello dieron cero contactos inesperados en un bloque de 27
poses. La revisión completa de 285 poses y la galería de personalización se
están renovando con la malla actual; no se consideran cerradas por ese bloque.

También están pendientes el recorrido completo de práctica con mapas nuevos,
la exportación, pruebas del EXE, matriz final de red y vídeo. BUILD identificará
el hash y los archivos exactos del paquete cuando exista.

## Rendimiento y conexión

La casa generada de 16 actores todavía no cumple 60 FPS sostenidos en la
medición de estrés a 1080p sobre RTX 3060 Ti / Ryzen 5600X. Se midió Forward+
como alternativa, con menor coste de dibujo, pero cambia la iluminación y
sigue siendo sólo un diagnóstico; el proyecto conserva Compatibility.
No se certifican requisitos mínimos ni rendimiento en otros equipos.

ENet local y una invitación válida no prueban conexión entre casas. La ruta
WAN automática/EOS continúa pendiente. La voz real requiere además validación
por escucha y micrófono en el equipo del jugador.
