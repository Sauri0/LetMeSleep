# Dejame dormir 0.2.0 — verificación de entrega

7 de septiembre de 2026. Godot 4.5.2, Windows x86_64, protocolo 2.

## Resultado

Importación, exportación, arranque nativo, reglas, red local y navegación verificados. Se incluyen seis capturas del EXE entregado y evidencia en validaciones/. No se probó una partida humana entre conexiones de Internet diferentes.

| Comprobación | Evidencia |
|---|---|
| Simulación autoritativa | 3242 comprobaciones, 0 fallos: zonas privadas, picadura anclada, tareas, vidas, herramientas, colisiones y finales. |
| Sala y sorteo | 321 comprobaciones, 0 fallos: humanos exactos 1..5, ambos equipos, 1v1, límites 12 mosquitos/16 total, listos y sorteos independientes. |
| Cosméticos | Allowlist, tipos, límites, datos malformados, apariencia por rol y copias independientes aprobados. |
| Geometría y audio | 13 comprobaciones de visibilidad y 28 de audio aprobadas. Otros 24 controles de personalización visual y cuatro capturas de accesorios aprobados. |
| Navegación UI | 27 comprobaciones con InputEvents: Tab/Shift+Tab/Enter, Esc desde campos y subpantallas, foco modal, cancelar reasignación, permisos de configuración y guardado/recarga real de ambas apariencias. Preferencias originales restauradas byte por byte. |
| Integración cliente/interfaz/mundo | 33 comprobaciones nativas, 0 fallos: paseo en sala, mouse capturado/liberado, pausa de ambos roles, bloqueo de movimiento/mirada/acciones en menús, retorno a resultados/sala/inicio. Solo el transporte fue sustituido en este fixture. |
| Sangre ENet | 1v1, dos rondas consecutivas: sorteo, ready, movimiento en sala, apariencia sincronizada, picadura y cuota compartida. |
| Tareas ENet | 1v1, dos rondas con fuente y una con EXE candidato: tareas completadas y resultado compartido. |
| Supervivencia ENet | 5v1 con fuente y 4v12 con EXE candidato: 16 clientes reales ENet en procesos locales, resultado compartido y sin errores. |
| Autoridad y privacidad | Intentos de elegir rol, iniciar/configurar sin ser anfitrión y cambiar reglas/apariencia durante ronda rechazados. Sin asignaciones privadas en estados públicos. |
| Desconexión y compatibilidad | EXE candidato: desconexión aborta sin ganador y vuelve a sala; versión/protocolo incompatible rechazados. |
| EXE definitivo | Nueva partida Sangre 1v1 aprobada tras el último ajuste de contraste; seis capturas nativas de inicio, sala, dos previews y dos roles, todas sin stderr. |

Los sorteos pueden repetir roles de una ronda a otra. Los tests comprueban independencia y cantidad exacta, no exigen alternancia. La prueba de 5v1 también confirma que las rotaciones no favorecen permanentemente al primer humano.

## Trazabilidad

- EXE definitivo SHA256: `8E9231CA792C9A2E7B972850EFB341BFD5BD923B671AD4B96C71AE0B7C8FEFB6`.
- Candidato que verificó tres modos, 16 clientes, desconexión y compatibilidad: `9FEDA5D28A7595DE2759BB65774DC4675C4A864248DB78B28077BC2B7064075C`.
- Exportación intermedia tras corregir preview/foco, con dos rondas de Sangre aprobadas: `94483F860F71ED779F2CD8BCF11EC63F1E5F80E5A684876BC222199F944AF723`.
- Entre esos candidatos y el definitivo solo se corrigieron UI de personalización, foco diferido y contraste de sus títulos. La red, simulación y sorteo no cambiaron.
- `build-02.log` contiene la suite completa; `build-02-final.log`, la exportación final. `release02-*.json` contiene los informes de clientes. Los nombres históricos human/mosquito de los archivos del harness no determinan su rol: ver `roles_by_round`.
- El fallo de preview detectado por pruebas fue corregido antes de entrega. Las capturas de entrega muestran el personaje, color y accesorio seleccionados.

## Reproducción desde fuentes

Ejecutar `work/setup-tools.ps1` si faltan editor/plantillas. `work/build.ps1` importa, ejecuta suites y exporta. `work/test-network.ps1 -Mode blood -Humans 1 -Mosquitoes 1 -Rounds 2` prueba revancha; `-Executable` permite indicar el EXE.

El fixture nativo se ejecuta con Godot, `--path game --script res://tests/client_ui_checks.gd -- --screens`. El de navegación está en `work/ui_v02_navigation.gd` y respalda/restaura preferencias. Los bots son herramientas de validación, no rivales disponibles en el menú.

## Límites pendientes

Falta jugar entre PCs y conexiones distintas, medir latencia/pérdidas, balance y requisitos de hardware en los equipos de los amigos. Los 16 clientes locales no certifican capacidad en Internet. El código de sala requiere una dirección alcanzable y UDP 27840; no configura NAT. No se modificó router/firewall ni se contrató alojamiento. Una desconexión interrumpe la ronda; la reconexión ocurre en la sala. El menú no detiene una ronda online.

La versión 0.1.0 y sus artefactos se conservan por separado. Sus capturas y hashes no se usan como evidencia de 0.2.0.
