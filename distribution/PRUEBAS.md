# Let me sleep 0.9.1 — alcance de pruebas

Godot 4.5.2, Windows, protocolo 10. Online integrado mediante invitación LMS1-;
conexión directa avanzada DD5-. Todos los participantes deben usar esta versión.
BUILD.json identifica el commit y las huellas del paquete exportado.

## Verificación de la compilación

El ejecutable 0.9.1 se exportó desde eaf2df7. Pasaron las suites completas
sin interfaz y con renderizado nativo. BUILD.json registra los commits y
la procedencia de ambas fases: entre ellas sólo cambió el comparador nativo
de geometría heredada; no cambiaron el juego, los modelos ni las pruebas
de la fase sin interfaz.

Se verificaron techos y uniones, casa v2 con corredores junto a escaleras,
luces por tramo, movimiento, picaduras adheridas y controles de personalización.
El corpus de 1000 semillas produjo 1000 distribuciones distintas y se repitió
en procesos nuevos con las mismas huellas, sin fallos. El recorrido físico
de 11 semillas pasó 5591 comprobaciones. La geometría heredada pasó 49778;
la diferencia máxima de altura de cápsula cacheada fue 2,97 micras.
Los informes y sus límites permanecen en work/.

El EXE pasó nueve escenarios posteriores a la exportación: anfitrión real EOS,
dos clientes locales, red, sesión, menús, puertas, cámara/cuerpo y práctica como
humano y mosquito. Todos terminaron sin errores. Las preferencias del jugador
se restauraron al terminar las pruebas. Evidencia: work/release07-director091-exe-final*.

Se capturaron 23 vistas del mapa house-v2-1 sobre ese EXE. Director revisó
cuatro vistas de escaleras y pasillos: sin grandes planos superpuestos ni
obstrucción visible de los pasos. Persisten sombras duras, zonas oscuras bajo
escaleras y acabado simple. Las capturas estáticas no certifican ausencia de
parpadeo durante el movimiento ni todos los mapas/ángulos posibles.

## Alcance del online

El anfitrión crea la sala dentro del juego. Los invitados pegan el código LMS1-.
Las pruebas de sesión, invitaciones y dos clientes locales comprueban partes
del flujo, pero no prueban una conexión entre dos casas ni el relay de Epic.
La prueba WAN con un amigo sigue pendiente después del pulido. No se requiere
instalar otra aplicación ni arrancar un servidor externo.

## Rendimiento y calidad pendientes

Los FPS empiezan sin límite; el jugador puede elegir un tope y VSync en Ajustes.
1080p a 60 FPS en GTX 1660 Ti sigue siendo un objetivo, no un requisito mínimo
ya certificado. En RTX 3060 Ti + Ryzen 5 5600X, mapa house-v2-1 a 1080p,
12 segundos por escenario y FPS sin límite, se midieron estos tiempos de cuadro:

| Escenario | Mediana | Percentil 90 | Percentil 99 |
|---|---:|---:|---:|
| 2 participantes, sombras altas | 3,13 ms | 16,13 ms | 22,00 ms |
| 16 participantes, sombras altas | 11,67 ms | 44,04 ms | 60,97 ms |
| 16 participantes, sombras desactivadas (diagnóstico) | 11,06 ms | 16,05 ms | 24,27 ms |

Con 16 participantes y sombras altas, el 23,97% de los cuadros superó
16,67 ms: todavía no hay 60 FPS sostenidos. Los bots y poses varían entre
ejecuciones; la fila sin sombras es diagnóstico, no una comparación A/B
controlada ni un cambio del ajuste predeterminado. La medición no certifica
hardware mínimo. El micrófono real y la escucha espacial requieren prueba.

La mejora completa de rasgos/modelados y la distribución doméstica por uso
continúan en 0.9.2. Los controles automáticos de geometría no sustituyen la
revisión visual de todos los ángulos y combinaciones.
