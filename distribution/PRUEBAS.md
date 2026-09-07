# Let me sleep 0.5.0 — verificación de entrega

7 de septiembre de 2026. Windows x86_64, Godot 4.5.2, protocolo 5; invitación DD3.

## Ejecutable

SHA256 del ejecutable comprobado:

`F52F96BACC0A54F229150EE42A3B6365C7D8675F780B51DA6F6AA6453BB47E38`

Código: `a55574c4a511d34d98bc4925baf2df85682aa304`. El manifiesto externo identifica los ZIP y el commit final de documentación, sin referencias circulares dentro de los paquetes.

| Prueba del EXE | Resultado |
|---|---|
| Práctica nativa, ambos roles y tres modos | 73/73: menú, mundo, movimiento, controles, resultado, repetir y salir. |
| Rescate con cliente, cámara y HUD reales | 20/20: golpe, caída, contador, compañero cercano, E mantenida, ayuda y recuperación del vuelo. |
| Crear sala desde UI | 13/13: servidor propio, puerto independiente, confirmación real, puerto ocupado, reintento, cancelación y cierre sin huérfano. Preferencias restauradas byte a byte. |
| Demo nativa de controles sin capturas PNG intermedias | 8/8: vuelo hacia la mira, frenado, concentración, cancelación, acople, extracción, desprenderse y escaleras. |
| Aturdimiento y ayuda, simulación embebida | 297/297. |
| Plazos de Tareas, simulación embebida | 59/59. |
| Auditor de privacidad, simulación embebida | 37/37. |
| ENet Sangre 1v1 por invitación, dos rondas | PASS: resultados coincidentes, regreso a sala, nuevo sorteo y 2308 paquetes privados auditados por cliente, sin pendientes. |
| ENet Tareas 1v1 | PASS: tarea real y meta colectiva, 1216 privados auditados por cliente, sin pendientes. |
| ENet Supervivencia 4 humanos + 12 mosquitos | PASS: 16/16 clientes, 19.433 privados auditados, 16 diferidos resueltos, cero pendientes, errores o stderr. Todos comprobaron movimiento, salto, carrera y agacharse en el lobby, cosméticos y el mismo resultado. |
| Desconexión y rechazos de versión | PASS: ronda interrumpida sin ganador; protocolo inválido y cliente EXE 0.4 real rechazados en unos 0,175 s, sin recibir datos privados. |

Los ensayos ENet son procesos en una misma PC. No prueban redes independientes ni conexión entre casas. Sus rondas naturales no observaron aturdimiento o ayuda; esos estados se verificaron por separado en simulación y cliente nativo. Los registros aceptados no contienen errores de stderr. El cierre por muerte del proceso padre se comprobó también con el candidato anterior 697B, cuyo Main/Network es idéntico al entregado: hijo cerrado y puerto liberado.

## Reglas y presentación sobre fuente

La compilación completa pasó reglas 1622, sala 321, mapas 431, rutas 303, locomoción 7325, defensa manual 5511, concentración 145, aturdimiento/ayuda 297, plazos 59, práctica 79, invitación 88, orden de red 11, conexión 23, privacidad 37, UI 106 y migración de preferencias 15. Cosméticos, geometría visual y audio también pasaron. La exportación final posterior sólo ajustó estilos de barras y fixtures de demostración/lobby; se repitieron sus pruebas relevantes en el EXE.

El aturdimiento dura 35 segundos en Sangre y Tareas y conserva la sangre. Ayudar acelera a 4× sin acumular ayudantes; no se puede ayudar a través de paredes ni durante una picadura. Los golpes posteriores no reinician el contador. Se probaron caída, piso superior, escaleras, recuperación en el lugar, colas de marcas y ausencia de victoria por quedar todos aturdidos. Supervivencia conserva eliminación definitiva. Un bot ayudante recuperó a su compañero en 8,85 s, incluido su tiempo de reacción.

Cinco encuentros completos de Sangre pasaron sobre fuente. El humano defensor aturdió cuatro veces y hubo tres recuperaciones; ganó al terminar los 120 s. El mosquito que permaneció adherido cayó a los 22,62 s y recuperó a los 57,62 s, sin morir. Dos pilotos con retirada ganaron a los 119,45 s y 99,18 s. Son sondas deterministas de reglas y bots, no evidencia de diversión o balance humano.

La revisión visual verificó 86 superficies y gestos de defensa, 19 casos de visualización del aturdimiento, audio de impacto y recuperación y personalización de ambos roles. El EXE final muestra relleno amarillo visible al 38% de ayuda, en una barra compacta de 104×4 px.

## Incidencias conservadas

- Un primer harness de lobby caminaba siempre hacia el mismo borde; uno de 16 clientes podía empezar a observarse contra la pared y no confirmar desplazamiento. Se corrigió la dirección del bot de prueba hacia el centro, conservando las comprobaciones de movimiento, salto y agacharse. El informe fallido permanece archivado.
- La demo con guardado de PNG intermedio no observó la primera carga de E y dio 7/8; la misma demo del EXE sin esas capturas pasó 8/8. No se reprodujo con instrumentación externa sobre el PCK final: PNG de 76–124 ms, E e interacción activas y edad de entrada máxima de 0,033 s. La demo original monitorizada también pasó 8/8 a 20 FPS con física de 60 Hz y a 20 Hz de física. El foco del sistema operativo durante el fallo original no fue verificado: la causa continúa indeterminada. Se conservan los registros y el caso queda en seguimiento si reaparece, sin afirmar que esté explicado o corregido. Las capturas independientes de rescate pasaron 20/20. No se modificó la lógica de concentración para aprobarla.
- El primer ensayo del rescate exportado usó un directorio de capturas inexistente y falló sólo al guardar cinco PNG. Ejecutado con el directorio correcto, pasó completo.
- La apariencia de zapatos suspendidos se midió: humano quieto, grounded=true y ambos zapatos a −5 mm del piso; al correr alternan pie de apoyo y elevación de 58 mm. No había salto congelado ni pivote alto. La puntera redonda y sombra débil a ras del piso aún pueden reducir la lectura visual del contacto.

## Pendientes y límites

**Internet integrado sigue pendiente.** ENet directo requiere una dirección alcanzable. EOSG 2.3.0 se probó aislado para carga de clases, exportación y apertura nativa; eso no equivale a inicio DeviceID, creación/unión de sala ni P2P/relay funcionales. El alta/configuración del producto no fue recibida.

La adaptación requiere listen server en una instancia, vinculación de identidad real con peer ID, fragmentación/reensamblado acotado y pruebas entre redes independientes. El codec local pasó 114 comprobaciones y 12 mensajes reales de la sonda anterior a aturdimiento; permanece sin conexión a Network. Se preparó un parche aislado de cabecera/PUID con 188 comprobaciones de diseño/aplicación, todavía sin compilar ni probar con SDK. Un primer import del plugin aislado tuvo crash al salir; los siguientes y el EXE aislado pasaron, pero la causa no está demostrada. Ningún SDK ni configuración EOS se incluye en el juego.

No se certifican rendimiento mínimo, tolerancia a pérdida real de Internet, accesibilidad completa, balance humano ni hardware del grupo. El clip de controles es una demostración determinista rotulada, no un benchmark ni una partida competitiva. La música de menú, nuevos modelos y mejoras de la siguiente etapa quedan fuera de 0.5.
