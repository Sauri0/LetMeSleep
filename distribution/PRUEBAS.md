# Let me sleep 0.4.0 — verificación de entrega

7 de septiembre de 2026. Windows x86_64, Godot 4.5.2, protocolo 4; invitación DD3, formato interno 1.

## Ejecutable entregado

**Cierre técnico completado.** Se ejecutó el mismo Let-me-sleep.exe que contiene el ZIP. SHA256:

`F82635685B1D19E74F2788AF246179348091FD33311E5290A2DCD9151BF50332`

Commit de código y diagnósticos: `3b6563815931e95c00ce8d09f6087903f7ebd772`. Las reglas y la interfaz jugable quedaron cerradas en `68e28d0034c613137b418eb43c5796d1b987cc59`; los cambios siguientes reforzaron los diagnósticos y la comprobación de compilación. Los hashes de los ZIP y el commit de documentación están en el manifiesto externo y sus archivos SHA256, para evitar una referencia circular dentro del propio ZIP.

| Prueba del EXE de entrega | Resultado |
|---|---|
| Práctica nativa desde menú: ambos roles y tres modos | 73/73; movimiento, cámara, W en 3D, frenado, salto, agacharse, ataque, pausa, resultado, repetir y salir. |
| Demo nativa de controles | 8/8; concentración mantenida/cancelada, acople, extracción, desprenderse, retirada y escalera completa sin saltar. |
| Plazos de Tareas mediante simulación embebida | 59/59; viaje caminando, penalización personal, límites, corte de encargos tardíos y partida completa. |
| Encuentros completos mediante simulación embebida | 5/5 escenarios; cuotas y resultados reales, sin forzar posiciones ni finales. |
| Auditor de privacidad con paquetes reordenados y filtraciones deliberadas | 24/24. |
| ENet Sangre 1v1, invitación y dos rondas | PASS; cuota diagnóstica 3, retorno a sala, nuevo sorteo, cosméticos y controles de patio. |
| ENet Tareas 1v1 | PASS; tarea real, objetivo diagnóstico 1 y resultado humano. |
| ENet Supervivencia: 4 humanos + 12 mosquitos | PASS; 16 clientes, resultado correcto, privacidad y controles confirmados. |
| Desconexión, protocolo inválido y EXE 0.3 real contra servidor 0.4 | PASS; interrupción sin ganador y rechazo de versión incompatible. |

Los registros de entrega no contienen errores de stderr. Son procesos locales en una misma PC, no equipos ni conexiones de Internet independientes.

En los 16 clientes se verificaron **19.144 paquetes privados**; **92** esperaron un estado público del tick correspondiente, y quedaron **0 pendientes y 0 fallos**. Un intento anterior marcó dos saltos no observados y una alarma de privacidad con un verificador que comparaba el paquete privado contra un rol público todavía no confirmado. Se conservó ese informe. El verificador actualizado espera confirmación autoritativa del movimiento, correlaciona ticks y rechaza datos públicos prohibidos, asignaciones destinadas a humanos y paquetes que no pueda validar. No se descartaron pendientes para aprobar. El juego no cambió durante esta corrección del harness.

## Reglas, geometría e interfaz sobre fuente

| Suite | Comprobaciones sin fallos |
|---|---:|
| Reglas autoritativas | 3256 |
| Sala social y sorteo | 321 |
| Mapas y spawns | 431 |
| Rutas físicas y alternativas | 303 |
| Locomoción y colisiones | 16013 |
| Concentración y combate | 277 |
| Práctica: seis pares rol/modo | 69 |
| Invitaciones | 51 |
| Orden de mensajes de red | 11 |
| UI nativa y límites de configuración | 77 |
| Migración de preferencias | 13 |
| Audio / privacidad visual / poses | 28 / 13 / 33 |
| Presentación visual nativa 0.4 | 32 |

También pasó la suite de cosméticos. La primera compilación ejecutó la matriz completa; tras los cambios de Tareas se repitieron reglas, práctica, plazos y UI. Los diagnósticos de red incorporaron sus propias regresiones. La compilación comprueba además la sintaxis de los puntos de entrada cargados dinámicamente. Las pruebas que modifican ajustes se ejecutaron secuencialmente y restauraron el perfil original byte por byte.

## Encuentros y alcance del balance

| Escenario reproducible | Resultado real |
|---|---|
| Humano inmóvil contra dos mosquitos | Primera picadura 26,87 s; pierde por cuota a 52,47 s. |
| Humano controlado por bot defensor | Gana a 32,67 s. |
| Mosquito que permanece adherido 4,2 s | Muere a 22,15 s. |
| Mosquito con retiradas a 2,75 s | Siete acoples; extrae 9,8 y muere a 115,85 s. |
| Mosquito con retiradas a 3,25 s | Gana cuota 12 a 97,07 s: siete acoples, seis retiradas y supervivencia. |

El ensayo usa inputs reales de PracticeSession y rutas físicas. La retirada ganadora combina desprendimiento, retroceso breve y nueva aproximación; no usa teleport ni concede sangre artificialmente. En la referencia 0.3, el primer contacto del escenario inmóvil ocurría a 1,38 s y la cuota a 25,77 s. Mapa, spawns y reglas cambiaron juntos: la comparación no aísla una sola causa. Estos resultados demuestran estrategias posibles, no diversión ni dificultad aprobadas por personas. Los humanos no tienen una barra de vida: Sangre se pierde por la cuota compartida.

## Tareas alcanzables en la casa ampliada

Se midieron 104 trayectos físicos entre spawns y puestos: 61 superaban el antiguo piso de 8 s, ninguno el plazo inicial de 30 s. El análisis de 94 nodos por 8 puestos cubrió 752 rutas. La mayor mide 52,43 m horizontales; con Simulation real, sin correr, se llega a los 16,4 s y se completa el trabajo a los **19,4 s**.

El piso predeterminado es ahora **24 s**, con 4,6 s de margen en ese recorrido. La autoridad y la UI comparten un mínimo de **trabajo + 21 s**; la frecuencia debe superar ese mínimo en 0,5 s. Se mantienen plazo inicial 30 s, frecuencia 36 s, trabajo 3 s y penalización personal 2 s. Cinco fallos naturales producen 28, 26, 24, 24 y 24 s, sin acelerar el calendario ni afectar a otros humanos.

No aparecen encargos si el tiempo real restante no alcanza para traslado y trabajo. La meta automática sigue siendo dos tercios de oportunidades válidas, redondeados hacia arriba: en 120 s y con un humano, hay encargos a 3, 39 y 75 s y meta 2; se omite el de 111 s. Una partida completa caminando realizó las tres tareas y ganó sin fallos ni colisiones. El HUD anuncia el tiempo efectivo disponible. La duración mínima configurable asegura una primera oportunidad por humano incluso con trabajo de 8 s: 33 s para uno y 39 s para cinco. La práctica ampliada de 180 s completó cinco tareas entre ambas plantas.

## Capturas, video y reproducción

El MP4 dura 13,4 s a 30 fps fijos y está rotulado **DEMO DE CONTROLES / RIVAL QUIETO**. Es una demostración controlada del EXE, separada de los encuentros automatizados. No representa FPS de juego. Las capturas de demo-final y practica-final proceden del mismo EXE; las ocho vistas de ambientes y poses son fixtures de la geometría final.

Evidencias y capturas se entregan en `0.4-validacion/` y `0.4-preview/`, junto a los ZIP. Los informes `release04-delivery-*` corresponden al hash de entrega; los intentos anteriores conservan sus nombres y contexto. `work/build.ps1` importa, comprueba y exporta; `work/test-network.ps1` reproduce ENet. Ejemplo desde la carpeta del EXE:

```powershell
./Let-me-sleep.exe --headless --script res://tests/task_deadline_test.gd
```

Las mediciones visuales orientativas, con RTX 3060 Ti y vsync desactivado, dieron medianas de 4,86 ms en dormitorio, 4,91 ms en escalera y 7,22 ms en pasillo; son vistas de diagnóstico, no requisitos mínimos ni mediciones de una partida completa.

## Límites de esta entrega

Falta juego humano para validar comodidad, lectura de marcas, dificultad y balance. Tampoco se verificaron varias computadoras físicas, Internet entre casas ni el rendimiento mínimo del grupo. Loopback y 16 procesos en una PC no certifican una LAN real ni latencia/pérdidas externas. La invitación no abre puertos ni resuelve CGNAT; el servidor sigue alojado en la PC del usuario, sin relay contratado ni cambios automáticos de router o firewall. Las entregas 0.1–0.3 se conservan.
