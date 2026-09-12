# Let me sleep 0.9.1 — Windows

Esta entrega corrige techos y uniones, amplía la circulación junto a las
escaleras y mejora el movimiento, la continuidad de las picaduras adheridas y
la selección de personalización. La siguiente entrega 0.9.2 continúa la
distribución doméstica y los cambios de manos y rasgos.

Descargá **Let-me-sleep-0.9.1-Windows.zip**, extraé todo y abrí
**Let-me-sleep.exe**. No necesitás Godot. **PRÁCTICA** permite probar ambos
personajes con bots. **Crear sala** aloja dentro del juego; compartí la
invitación **LMS1-** y mantené abierto el juego del anfitrión. Todos deben usar
0.9.1: el protocolo 10 no acepta las versiones anteriores.

Pasaron las suites completas de compilación y nueve escenarios sobre el EXE:
creación real del anfitrión mediante Epic, conexión entre clientes locales,
sesión, red, menús, puertas, cámara/cuerpo y práctica de ambos personajes.
Se verificaron 1000 casas distintas y su repetición determinista, además de
los recorridos físicos de 11 semillas. El paquete incluye BUILD.json,
PRUEBAS.md y una suma SHA256 junto al ZIP.

Es una versión de prueba. La conexión entre dos casas y el micrófono real
siguen pendientes de validación; las pruebas locales no certifican el relay
entre redes distintas. Los FPS vienen sin límite y la fluidez de 60 FPS en
GTX 1660 Ti todavía no está certificada. Consultá PRUEBAS.md para los resultados
y las limitaciones de calidad y rendimiento.
