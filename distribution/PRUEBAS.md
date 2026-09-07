# Let me sleep 0.6.0 — verificación de entrega

7 de septiembre de 2026. Godot 4.5.2, Windows x86_64, Compatibility/OpenGL, protocolo 6 e invitaciones DD3. Código `b794548399eac22f40f30d7cfa94027657f4d351`. EXE SHA256 `4B47820C2238B12482CE6D38C557B89F701AA04C30C68129FE660E2818E902AE`. Los hashes de los ZIP y el commit final de documentación están en el manifiesto externo, sin referencias circulares.

## Ejecutable exacto

| Prueba | Resultado |
|---|---|
| Práctica nativa, ambos roles y tres modos | 73/73; menú, controles, bots, resultados, repetir y salir. |
| Crear sala desde UI | 13/13; servidor propio, puerto, confirmación, reintento, cancelación y cierre. |
| Ayuda F1 con Main/Client/Practice reales | 28/28; abre/cierra, bloquea entradas, mantiene estado y devuelve captura sin salto de cámara. |
| Giro continuo mirando el cuerpo | 6/6 a 60 FPS; torso acompaña, movimiento y palmada siguen disponibles. |
| Malla deformada y personalización | 166/166; rayos contra malla, posturas, combinaciones y editor. |
| Contacto con Client y simulación | 86/86; ocho zonas propias de pie/agachado, concentración y defensa. Transporte de este fixture simulado. |
| Casa GLB | 218/218, 52 muebles; posiciones, límites físicos y cinco vistas. |
| Demo con audio del juego | 25/25; vuelo, frenado, carga, acople, sangre, desprenderse, LMB, caída y rescate. |
| Red ENet | 6 escenarios; 24 informes de cliente, incluidos 16 simultáneos. |

ENet verificó Sangre 1v1 por invitación y revancha con dos rondas, Tareas 1v1, Supervivencia 4 humanos + 12 mosquitos, desconexión sin ganador, rechazo de protocolo y rechazo del EXE 0.5 real. 26223 paquetes privados coinciden, 56 diferidos resueltos, cero pendientes, errores de cliente o stderr. Todos los clientes de rondas completas comprobaron movimiento, salto, carrera, agacharse y cosméticos. Son procesos en una misma PC, no redes independientes. Aturdimiento/rescate se comprueban por separado; no se afirma que las rondas ENet naturales los hayan ejercitado.

El vídeo dura 21,77 s, H.264/AAC, 1280×720, 30 FPS. MovieWriter capturó el mezclador real del EXE, sin sustituir ni normalizar audio: promedio −41,1 dB, pico −21,8 dB, decodificación completa sin errores. Preferencias observadas: master 0,59; música 0,55; efectos 0,80; ambiente 0,45; UI 0,65. La escena está rotulada como prueba preparada: rival quieto, un corte empieza con mosquito adherido y otro con aliado aturdido. Las acciones posteriores usan Client y autoridad reales. El rescate medido duró 8,63 s desde 34,43 s restantes, a una tasa observada de 3,95×. No representa una partida espontánea ni prueba de balance.

## Fuente, arte y audio

Reglas 1622, sala 321, mapas 431, rutas 303, locomoción 7325, defensa 6795, concentración 145, aturdimiento/ayuda 297, plazos 59, práctica 79, invitación 88, orden de red 11, conexión 23, privacidad 37, orientación/contacto 306, música 46, SFX 136, UI nativa 146 y migración 28 pasan. Cosméticos y geometría pasan. Las preferencias originales se restauran byte a byte. El codec aislado pasó 114 y sigue fuera de Network/EOS.

HUD: ayuda plegada con F1, indicadores contextuales y alertas críticas visibles. La comparación de rectángulos de paneles en seis estados y dos resoluciones pasó 36/36; ocupación de esos paneles baja 53–61,3%. Esa cifra no mide toda la oclusión ni rendimiento y procede de fixtures de interfaz.

Fuentes editables Blender, GLB y generadores en art_source. Humano de pijama con pantuflas/gorro al iniciar un perfil nuevo; perfiles existentes preservados. Editor con siete categorías, tarjetas, giro y enfoque. Humano visible por defecto: 38.946 triángulos, ligeramente por encima de la guía inicial; fuente sin triángulos degenerados y nueve clips validados contra postura compartida (error máximo 0,00000013 m). Postura relajada sólo en lobby/editor; las superficies de contacto del gameplay mantienen el contrato autoritativo. Mosquito con 21 huesos y diez clips. Casa de 16 habitaciones y dos plantas; 18 modelos domésticos editables.

48 OGG (9,41 MB), 35 efectos/ambientes, seis acentos, seis stems y una pieza tranquila. Tema principal original «Pasos de puntillas», 104 BPM, y variante calma «La casa bosteza», 80 BPM. Samples instrumentales VSCO2 Community CC0 con procedencia/licencia; Foley y zumbidos sintetizados. Fuentes incluyen WAV, MIDI, eventos y scripts. Buses independientes de música, efectos, ambiente y UI; las capas no revelan información enemiga privada. Licencias de audio visibles también junto al ejecutable.

## Medición local

RTX 3060 Ti, 1280×720, misma escena y fixture, vsync desactivado; 60 frames de calentamiento y 120 muestras por caso. Un personaje visible usa autoridad válida 1v1; 16 usa 4 humanos/12 mosquitos. Comparación entre EXE release 0.5 y release 0.6, separados de la medición provisional de desarrollo.

| Visibles / trabajo | 0.5 mediana ms | 0.6 mediana ms | 0.6 p90 ms | Draw calls mediana |
|---|---:|---:|---:|---:|
| 1 / cliente | 3.817 | 2.707 | 3.300 | 515 |
| 1 / host + bots | 4.740 | 3.205 | 3.698 | 483 |
| 16 / cliente | 10.796 | 8.744 | 9.267 | 971 |
| 16 / host + bots | 18.357 | 16.050 | 16.399 | 875 |

Memoria de vídeo 0.6: 25,3–43,1 MiB; memoria estática no disponible en estos EXE release (monitor devuelve 0). El fixture mide una vista local fija y bots/autoridad en la variante host, no una ronda completa, WAN ni requisitos mínimos. No certifica otros equipos.

## Incidencias conservadas

- Al mover la mesa del living, el primer lugar invadió una ruta; quedó corregido y rutas303/303 pasa. Los fixtures antiguos de mesa se actualizaron a su nueva posición; aterrizar y bajar del mueble pasan.
- El runner intentó malla y captura de ratón con el backend dummy headless, que no ofrece esas capacidades. Se clasificaron como pruebas nativas: malla166/166 y UI146/146 pasan; el build ahora las ejecuta con renderizador.
- El primer hosting del EXE no logró reservar el puerto de su fixture después del cierre; repetición sin cambios13/13 y sin stderr. Causa del primer fallo no demostrada; no se presenta como corrección de runtime.
- El ensayo de cámara contaba frames de renderizado y a FPS libres no dio tiempo al recentrado. A60FPS pasó6/6; se conserva la diferencia y se especifica la frecuencia requerida del fixture. Las reglas de giro se prueban además a20/60Hz.
- El wrapper de red interpretó LASTEXITCODE null como fallo cuando el script había pasado. Se reprodujo en shell fresca y se añadió exit0 explícito al helper. Los informes completos originales se reauditaron con todas las aserciones, no se aceptaron resultados parciales.
- Los fallos de fixtures documentados en0.5 siguen archivados con su entrega; no se atribuyen causas nuevas sin evidencia.

## Límites

Conexión directa ENet con dirección alcanzable. Internet integrado/EOS sigue pendiente de adaptación, configuración y pruebas entre casas; no se incluye SDK, relay ni credenciales EOS. No se certifican balance humano, diversión, accesibilidad completa, hardware mínimo ni tolerancia a pérdida real de Internet. Sangre/Tareas conservan aturdimiento35s y ayuda4× no acumulable; sólo Supervivencia elimina definitivamente. Sin cambios de duración por golpes repetidos ni victoria instantánea por quedar todos aturdidos.
