# Revisión visual alfa — checkpoint 19:10 UTC

Alfa sigue en recuperación; no publicar esta integración como arte aprobado ni avanzar a beta.

- M1 membranas `3eba9a` integrado `a03bb97`. CharacterBuilder6 importado tras Refresh, PASS 19:05:27 UTC. Source cf2ebb28253b3d1ac971333e5779f559b6766a253c92d06c8ba83b800c7008c8; builder b18fad25e08186ac9add5cafe4008958c96c03e738f9717c08657ce6836e4f18. Renders reales en `N:/LetMeSleep/Validation/ArtCatalog/characters-wing6`, 17 poses/vistas. Membrana menos saturada; aún faltan silueta, proporciones, contacto y clips completos. No prueba de transmisión sin fondo contrastado.
- M2 living `4605ea0` integrado `7d669ef`, mapas regenerados PASS 19:06:08 UTC. Mesa baja, estantería fuera de ventana, cojines y paño visibles en `living-round3.png`. Recoger herramienta y colisión de vidrio todavía requieren prueba runtime.
- W2 presentación regenerada PASS 19:07:08 UTC. `menu-round3-1080.png` y `living-round3.png` demuestran que persisten cara a medio iluminar, halos de faroles y techo oscuro. W2 investiga estado real de iluminación/SH antes de otro ajuste.
- Director amplió únicamente mosquito de presentación del menú de 2.5 a 4; escala de partida intacta.
- W1 coordina con W2 la orientación visual al posarse en paredes/techo; los tests anteriores demostraban estados/cámara, no apoyos renderizados de patas.
- Editor Director PID33148 cerrado; proceso ausente y log de salida confirmados. Pruebas ejecutadas con `-noaudio`. Slot CPU cedido a M1 para regenerar nueva silueta y auditores de superficie; no abrir otro Unity/Blender simultáneo.
- Catálogo: 32 fichas, 5 con capturas parciales actuales, 33 PNG contando historia. Índice local `N:/LetMeSleep/Validation/ArtCatalog/index.html`; no es release ni certificación visual.

Pendiente: integrar siguiente M1 sólo con auditorías coherentes; resolver iluminación observada, validar contacto visual y uso de objetos afectados, completar animaciones/evidencia y escucha real. WAN con dos casas continúa pendiente de Branko/amigos. El próximo paquete exige esos gates y mantener pruebas técnicas apropiadas sin repetir por rutina toda la batería antigua.
