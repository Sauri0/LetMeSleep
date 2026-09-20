# Let Me Sleep — guía de prueba v0.2.0

Guía preparada para acompañar la candidata Windows. La existencia de esta guía
no significa que el paquete final ya esté publicado. Consultá las notas de la
candidata que recibiste para conocer sus pendientes.

## Empezar

Extraé la carpeta completa del juego antes de abrir `Let-me-sleep.exe`.
Conservá junto al ejecutable las carpetas y archivos que vienen en el paquete.
En AJUSTES podés elegir resolución, pantalla completa, sensibilidad y audio.

Para aprender los controles, entrá en ENTRENAMIENTO, elegí humano o mosquito,
mapa y modo, y pulsá EMPEZAR ENTRENAMIENTO. Los bots se usan sólo aquí.

Los cinco mapas son Casa del Patio, Campamento Pinar, Puerto del Faro,
Isla del Laguito y Yate a la Deriva. Probá ambos roles: la vista y la forma de
moverse son diferentes.

## Controles de teclado y mouse

| Acción | Humano | Mosquito |
|---|---|---|
| Moverse | W, A, S, D | W, A, S, D; W sigue la dirección de la mirada |
| Mirar | Mouse | Mouse |
| Saltar / subir | Espacio: saltar | Espacio: subir |
| Agacharse / bajar | Ctrl izquierdo: agacharse | Ctrl izquierdo: bajar |
| Correr | Shift izquierdo | — |
| Interactuar | E; mantener para trabajar en una tarea | Mantener E para picar en contacto con un humano |
| Apoyarse / despegar | — | F, apuntando a una superficie al apoyarse |
| Ayudar a otro mosquito | — | Mantener R cerca y mirando al compañero caído |
| Usar defensa | Clic izquierdo | — |
| Lanzar pantufla equipada | Mantener clic izquierdo para cargar; soltar para lanzar | — |
| Elegir inventario | 1, 2, 3; 0 para manos; rueda para recorrer opciones | — |
| Soltar herramienta | G | — |
| Acercar / alejar cámara | — | Rueda |
| Hablar | Mantener V; se puede cambiar en AJUSTES | Mantener V; se puede cambiar en AJUSTES |
| Menú de pausa / volver | Esc | Esc |

Si el inventario está lleno, revisá la propuesta de reemplazo que muestra el
juego y pulsá E otra vez para confirmarla. Sólo la pantufla se lanza; las otras
herramientas usan su propia acción. La carga de pantufla tiene un lanzamiento
automático si mantenés el botón hasta el límite.

## Jugar con amigos

1. Todos deben usar la misma candidata y versión del juego.
2. Un jugador entra en CREAR SALA y comparte el código que aparece.
3. Los demás eligen UNIRME CON CÓDIGO e ingresan ese código.
4. El anfitrión elige mapa, modo y reglas. Los jugadores se ponen listos y el
   anfitrión inicia la ronda. Los roles se sortean al comenzar cada ronda.
5. Después del resultado, vuelvan al lobby para preparar otra ronda.

Los bots nunca sustituyen a un jugador online. Si alguien se desconecta, se
reserva su plaza durante 30 segundos sin control de IA. Si se pierde el
anfitrión, la sala se cierra y hay que crear otra.

En AJUSTES elegí el micrófono y probá la tecla para hablar. La voz depende de
la distancia y de las paredes. Podés silenciar personas desde la interfaz;
ese silencio dura hasta salir de la sala. Abrir el menú de pausa online no
detiene la ronda para los demás.

## Recorrido de prueba

Primero completá una ronda de entrenamiento con cada rol. Probá movimiento,
defensa, picadura, apoyo en suelo/pared/techo y vuelta al menú. En Tareas,
comprobá que podés llegar al objetivo señalado y trabajar manteniendo E.

Con amigos, completá al menos dos rondas seguidas, cambiando mapa y modo entre
ellas. Comprobá que ambos ven el mismo resultado y pueden volver al lobby.
Incluí una desconexión y regreso dentro de 30 segundos, y otra salida del
anfitrión. Para evaluar voz, prueben cerca, lejos y separados por una pared.

Una prueba entre redes diferentes comprueba algo que no demuestra jugar en
una sola computadora. Anotá qué entorno usaron; no hace falta compartir
contraseñas, direcciones IP públicas ni credenciales.

## Limitaciones de esta candidata

Esta entrega prioriza disponer de una versión testeable. El menú y el HUD tienen
una primera mejora; el rediseño completo y el arte final de personajes siguen
pendientes. La marcha humana, la deformación de brazos y las caídas de humano y
mosquito todavía necesitan correcciones; no se incorporaron prototipos físicos
sin validar. Hay un conflicto visible de piso/terreno en el faro de Puerto y la
revisión de parpadeos de los cinco mapas aún no está completa.

Las pruebas locales no certifican partidas entre dos redes ni voz con dos
personas. No se garantiza 60 FPS. La personalización modular y los objetos
nuevos todavía no cuentan con todo su contenido visual final.

## Informar un problema

Incluí versión/candidata, mapa, modo, rol, qué hiciste y qué ocurrió. Una
captura o un video corto ayuda si se trata de cámara, animación, colisión o
interfaz. Para audio, indicá si usabas auriculares o parlantes y qué micrófono.
Si se repite, anotá los pasos más cortos que permiten provocarlo.

