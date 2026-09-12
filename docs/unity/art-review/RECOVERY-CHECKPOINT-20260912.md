# Revisión visual alfa — checkpoint 19:10 UTC

## Actualización posterior: referencias obligatorias y prueba round5

- Branko rechazó explícitamente el acabado de personajes y fijó el mismo nivel de referencia para UI, objetos y mapas. Contratos `CHARACTER-QUALITY-BAR.md` y `UI-ENVIRONMENT-QUALITY-BAR.md`; originales conservados bajo `N:/LetMeSleep/References/{CharacterQuality,UIQuality,EnvironmentQuality}-20260912`. El catálogo muestra rechazo artístico; una importación correcta no lo revierte.
- M1 `bd2bf50` integrado `cb9119e`: nueva silueta de mosquito. Tras un primer fallo de idempotencia, Refresh y segundo ciclo estable PASS 19:19:33 UTC, sin cambios de hashes entre ciclos estables. Causa del primer fallo no confirmada. Source digest `10347aca34b32de9f67d04f1386e4924e30a6bc8c6ae3e6f8202bd5d704dc6a2`. Diecisiete capturas nativas en `Validation/ArtCatalog/characters-silhouette7`; no equivalen a validar clips completos ni aprobación artística.
- W1 `05f9ab6` integrado `fc3d97b`: orientación de presentación con normal de apoyo; validación visual en piso/pared/techo y transiciones todavía pendiente.
- W2 `c3a3a11` integrado `ed1c395`: round4 confirma cara más legible y halos de faroles eliminados. Núcleos de faroles aún apagados visualmente. El diagnóstico confirma actualización SH; CustomizationKey sólo afecta capa 30.
- W2 `50e8b91` integrado `8aba711`: ambiente de casa corregido considerando conversión a espacio lineal. Captura real `N:/LetMeSleep/Validation/Alfa-VisualRecovery/living-round5.png` muestra pared posterior y techo legibles. La escena sigue plana y el mobiliario insuficiente: cojines cúbicos, mesa simple, cortinas rígidas y ventana sin profundidad. Resultado técnico parcial, arte pendiente de rehacer.
- Equipo activo: M1 rehace humano con planos faciales, mandíbula y ropa; M2 rehace un living completo con geometría y composición propias; UI rehace componentes y pantallas alfa; W2 coordina materiales/luminaria con M2. No ampliar variantes ni mapas para sustituir calidad.
- Editor Director PID23792 sigue abierto con `-noaudio`, Play detenido después de round5. Slot Unity/GPU reservado Director. M1 trabaja fuentes y solicita turno antes de Blender. Nuevos PNG round4/round5/silhouette7 todavía no incorporados a catálogo; ZIP anterior quedó desactualizado.
- Cambios generados de personajes, mapas, audio y presentación permanecen deliberadamente sin commit global. No restaurar ni agregar todo. Alfa continúa sin aprobación; beta, escucha real y WAN entre casas siguen pendientes.

Alfa sigue en recuperación; no publicar esta integración como arte aprobado ni avanzar a beta.

- M1 membranas `3eba9a` integrado `a03bb97`. CharacterBuilder6 importado tras Refresh, PASS 19:05:27 UTC. Source cf2ebb28253b3d1ac971333e5779f559b6766a253c92d06c8ba83b800c7008c8; builder b18fad25e08186ac9add5cafe4008958c96c03e738f9717c08657ce6836e4f18. Renders reales en `N:/LetMeSleep/Validation/ArtCatalog/characters-wing6`, 17 poses/vistas. Membrana menos saturada; aún faltan silueta, proporciones, contacto y clips completos. No prueba de transmisión sin fondo contrastado.
- M2 living `4605ea0` integrado `7d669ef`, mapas regenerados PASS 19:06:08 UTC. Mesa baja, estantería fuera de ventana, cojines y paño visibles en `living-round3.png`. Recoger herramienta y colisión de vidrio todavía requieren prueba runtime.
- W2 presentación regenerada PASS 19:07:08 UTC. `menu-round3-1080.png` y `living-round3.png` demuestran que persisten cara a medio iluminar, halos de faroles y techo oscuro. W2 investiga estado real de iluminación/SH antes de otro ajuste.
- Director amplió únicamente mosquito de presentación del menú de 2.5 a 4; escala de partida intacta.
- W1 coordina con W2 la orientación visual al posarse en paredes/techo; los tests anteriores demostraban estados/cámara, no apoyos renderizados de patas.
- Editor Director PID33148 cerrado; proceso ausente y log de salida confirmados. Pruebas ejecutadas con `-noaudio`. Slot CPU cedido a M1 para regenerar nueva silueta y auditores de superficie; no abrir otro Unity/Blender simultáneo.
- Catálogo: 32 fichas, 5 con capturas parciales actuales, 33 PNG contando historia. Índice local `N:/LetMeSleep/Validation/ArtCatalog/index.html`; no es release ni certificación visual.

Pendiente: integrar siguiente M1 sólo con auditorías coherentes; resolver iluminación observada, validar contacto visual y uso de objetos afectados, completar animaciones/evidencia y escucha real. WAN con dos casas continúa pendiente de Branko/amigos. El próximo paquete exige esos gates y mantener pruebas técnicas apropiadas sin repetir por rutina toda la batería antigua.

W2 entregó c3a3a11 al cierre de esta tanda: orientación de spots hacia interior/piso, fill adelantado y DynamicGI.UpdateEnvironment tras Trilight. Integrado para la próxima captura; no probado nativamente todavía porque M1 ocupa CPU. Script de diagnóstico preparado en Validation/Alfa-VisualRecovery/DumpPresentationLighting.cs.
