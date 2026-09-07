# Let me sleep 0.3.0 — registro de verificación

7 de septiembre de 2026. Godot 4.5.2, Windows x86_64, protocolo 3.

## Estado registrado

**Entrega Windows verificada.** El ejecutable definitivo, con SHA256 **EBE45FB53262E8E8A6BAB35B57104BF5C7FC7043E024AD0EAB5BCDE0D65BD9A5**, aprobó práctica nativa, los tres modos por ENet, invitación, revancha, desconexión y rechazo de versión incompatible. Los ensayos finales terminaron sin errores ni avisos en stderr. El código de juego corresponde al commit **f1ffa97**; los registros de entrega se completaron después sin modificar el binario.

No se utiliza un hash, captura o resultado de 0.2.0 como certificación del ejecutable 0.3.0.

| Comprobación | Evidencia 0.3 |
|---|---|
| Simulación autoritativa | **3242 / 0 fallos**: zonas privadas, anclaje, calendario, sangre compartida, tareas, vidas, herramientas, colisiones, finales y revancha. |
| Sala y sorteo | **321 / 0 fallos**: humanos exactos 1..5, ambos bandos, mínimo 1v1, topes 12 mosquitos/16 total, listos y sorteos independientes que pueden repetir rol. |
| Mapas | **61 / 0 fallos**: patio y casa separados, datos copiados, spawns válidos, límites y obstáculos propios, lobby no seleccionable como mapa de ronda. |
| Locomoción y poses | **16013 / 0 fallos**: correr/agacharse/saltar, aterrizar sobre muebles, techo, gravedad con entrada caducada, mensajes repetidos/reordenados, animación de palmada/herramientas, superficies expuestas y márgenes de cada zona. Anclaje, sangre, defensa propia y rescate trasero conservados al moverse. |
| Práctica automática | **49 / 0 fallos**, según integración: ambos roles y tres modos, decisiones funcionales y estado público/privado propio. Es una sesión local jugable, distinta del harness de red. |
| Invitación | **51 / 0 fallos**, según integración: paquete DD3 de dirección/puerto/sala, validación de versión, tipos, límites y entradas malformadas. |
| Interfaz y cliente nativos | **31 / 0 fallos** en cliente/interfaz y **46 / 0 fallos** de navegación, según integración. |
| Visual nativo | **40 / 0 fallos**, según integración: mundo y representación 0.3. |
| Práctica nativa del EXE definitivo | **72 / 0 fallos**: los seis pares rol/modo desde botones reales, rivales activos, carrera, salto, agachado y cámara, palmada, vuelo, menú, resultado, reinicio y salida. Cinco capturas del propio ejecutable. Los finales de esta prueba de interfaz se inducen; la suite de práctica automática comprueba los objetivos y resultados por simulación. |
| Migración de preferencias | **10 / 0 fallos**, según agente de UI: copia válida de ajustes/apariencias/bindings, origen intacto, sin sobrescribir destino y restauración exacta del fixture. |
| ENet del EXE definitivo | **Sangre 1v1 con invitación y dos rondas; Tareas 1v1; Supervivencia 4v12: PASS**. Todos los clientes completan los resultados previstos, conservan privacidad/apariencias y reciben carrera/salto/agachado en el patio. El amigo decodifica dirección, puerto y sala del mismo DD3. Son procesos locales en una PC, no conexiones domésticas distintas. |
| Desconexión e incompatibilidad del EXE | **PASS**: al salir un participante durante la ronda, el restante vuelve a sala sin ganador. Un saludo con versión/protocolo incompatibles es rechazado antes de entrar. |
| Orden de mensajes de red | **11 / 0 fallos**: un mensaje tardío de sala desde otro canal no cancela una ronda cuyo snapshot ya comenzó. Corrección incorporada a la nueva exportación. |
| Sonido y privacidad visual sobre fuente | **28 / 0** audio; **13 / 0** privacidad visual. Zumbido ocluido por la casa, eventos confirmados una vez, limpieza, y marcas de objetivo privadas. Cosméticos también aprobados. |
| Exportación y capturas definitivas | **PASS**: Windows x86_64 exportado, inicio Let me sleep, patio y personalización de ambos roles capturados desde el EXE. Las pruebas visuales nativas adicionales observan fases distintas de animación y contacto de 22 zonas en siete poses. |

La matriz de poses comprueba contacto y margen con estados de carrera, salto, agachado, orientación, herramientas y pulso de golpe. Los sorteos se prueban por cantidad exacta e independencia, sin exigir alternancia entre rondas.

## Reproducción desde fuentes

Restaurar herramientas con **work/setup-tools.ps1** si faltan editor o plantillas. **work/build.ps1** importa el proyecto y exporta Windows. Las suites pueden ejecutarse directamente:

```powershell
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/rules_test.gd
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/lobby_rules_test.gd
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/locomotion_test.gd
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/maps_test.gd
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/practice_test.gd
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/invitation_test.gd
```

El harness **work/test-network.ps1** prueba clientes ENet y revancha. La **práctica del menú** usa PracticeSession y BotBrain, corre sin red y aplica la simulación de juego. Sus rivales no reciben marcas o tareas privadas ajenas.

## Cierre de compilación

El nombre oficial es **Let me sleep**. Artefactos: **Let-me-sleep.exe**, **Let-me-sleep-0.3.0-Windows.zip** y **Let-me-sleep-0.3.0-fuentes.zip**. BUILD.txt identifica el EXE probado; MANIFIESTO-0.3.0.json y los archivos .sha256.txt acompañan los ZIP con sus hashes. La entrega conserva informes y logs en **0.3-validacion/** y capturas en **0.3-preview/** junto a los paquetes.

El primer candidato permitió reproducir un mensaje de lobby tardío que llegaba después de una ronda nueva por otro canal. Fue corregido, se agregó una regresión de 11 comprobaciones y se repitieron los ensayos sobre el hash definitivo indicado arriba, incluida la carga de 16 clientes. El ajuste de audio fue de fixture: cargar la casa explícitamente, porque el mundo ahora comienza en el patio.

Las guías LEEME remiten a este registro y BUILD.txt para el estado de entrega. Los entregables y documentos históricos 0.1 y 0.2 se conservan separados.

## Pruebas humanas pendientes

Jugar entre PCs y conexiones distintas; medir latencia, pérdidas, discrepancias de golpes, comodidad y comportamiento del hardware del grupo. Una GPU dedicada no demuestra requisitos mínimos. También falta balance humano de velocidades, salto, herramientas, sangre, tareas y capacidad máxima cómoda.

La invitación DD3 requiere una dirección y ruta UDP alcanzables. El selector LAN enumera IPs locales; no consulta HTTP para descubrir la dirección pública, no abre puertos ni resuelve CGNAT. No se configuró router/firewall ni se contrató alojamiento. Una desconexión interrumpe la ronda; el menú no detiene la simulación online ni la práctica.
