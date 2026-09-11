# Let me sleep 0.9.1 — alcance de pruebas

Godot 4.5.2, Windows, protocolo 10. Online integrado mediante invitación LMS1-;
conexión directa avanzada DD5-. Todos los participantes deben usar esta versión.
BUILD.json identifica el commit y las huellas del paquete exportado.

## Estado de preparación

El paquete final 0.9.1 todavía está pendiente de exportación y pruebas.
No se atribuyen a esta versión los resultados del ejecutable 0.9.0-rc.1.

En fuente se integraron correcciones de techos y uniones, casa v2 con corredores
junto a escaleras, luces por tramo, suavizado de movimiento y mejoras en el
selector de personalización. Cada parte tiene pruebas propias; falta cerrar la
verificación conjunta, las transiciones al recibir picaduras y el recorrido
físico de las casas generadas. Los informes de cada parte permanecen en work/.

## Alcance del online

El anfitrión crea la sala dentro del juego. Los invitados pegan el código LMS1-.
Las pruebas de sesión, invitaciones y dos clientes locales comprueban partes
del flujo, pero no prueban una conexión entre dos casas ni el relay de Epic.
La prueba WAN con un amigo sigue pendiente después del pulido. No se requiere
instalar otra aplicación ni arrancar un servidor externo.

## Rendimiento y calidad pendientes

Los FPS empiezan sin límite; el jugador puede elegir un tope y VSync en Ajustes.
1080p a 60 FPS en GTX 1660 Ti sigue siendo un objetivo, no un requisito mínimo
ya certificado. Debe medirse el conjunto nuevo; la medición de la candidata
anterior no representa esta versión. El micrófono real y la escucha de voz
espacial también requieren prueba del jugador.

La mejora completa de rasgos/modelados y la distribución doméstica por uso
continúan en 0.9.2. Los controles automáticos de geometría no sustituyen la
revisión visual de todos los ángulos y combinaciones.
