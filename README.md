# Let me sleep — prototipo 0.4.0

**[Descargar para Windows](https://github.com/Sauri0/LetMeSleep/releases/download/v0.4.0/Let-me-sleep-0.4.0-Windows.zip)** · [Todas las versiones](https://github.com/Sauri0/LetMeSleep/releases)

Descomprimí todo el ZIP y abrí **Let-me-sleep.exe**. Elegí **PRÁCTICA** para jugar contra bots. No necesitás Godot ni una cuenta de GitHub. La guía **LEEME.html** está en la carpeta.

La descarga actual es la entrega 0.4 existente. La versión 0.5 está en desarrollo e incluirá correcciones al arranque local, dirección/puerto y mensajes de conexión. El timeout entre casas todavía no está resuelto ni verificado en Internet real.

Juego nativo para Windows de humanos contra mosquitos: humano en primera persona, mosquito en tercera, casa caricaturesca y tres modos. Godot **4.5.2 stable**, GDScript y autoridad de juego en la PC anfitriona. No requiere Steam, navegador ni cuentas.

La versión **0.4** amplía la casa a dos pisos y cambia el vuelo y la picadura: avanzar hacia la mira en 3D, frenar al soltar y mantener concentración antes del anclaje. La defensa propia se muestra antes de recibir la picadura. Se conservan práctica local, invitación única, personalización y estética de cómic.

## Jugar y practicar

Descomprimí **Let-me-sleep-0.4.0-Windows.zip** completo y abrí **Let-me-sleep.exe** o **Jugar.cmd**. No necesitás instalar Godot para jugar.

Desde **PRÁCTICA**, elegí **Humano** o **Mosquito**, luego **Recolección de sangre**, **Supervivencia** o **Tareas**, y pulsá **¡A PRACTICAR!**. Corre localmente sin servidor de red ni otros jugadores: humano contra dos mosquitos automáticos, o mosquito contra un humano automático. Los rivales se mueven, buscan objetivos, atacan, pican y trabajan según el modo; usan las mismas reglas y solo estado público y datos privados propios. El resultado permite repetir o volver al menú.

La elección de rol existe en práctica. En las salas con amigos, el servidor lo sortea.

## Crear una sala e invitar

1. Elegí **CREAR SALA**, escribí tu nombre y pulsá **1 · Encender servidor en esta PC** y **2 · Crear sala**. Esto conecta tu cliente a 127.0.0.1:27840. Como alternativa, arrancá **Iniciar-servidor.cmd** y usá ese servidor, sin encender otro.
2. En la sala, abrí **Dirección para amigos** o **Invitar**. Elegí tu IP local si están en la misma LAN; para otras casas, indicá una dirección alcanzable desde Internet y el puerto público correspondiente.
3. Pulsá **GUARDAR Y COPIAR INVITACIÓN** y compartí el texto **DD3-…**. Tus amigos abren **UNIRME CON INVITACIÓN**, escriben su nombre y pegan ese único texto.
4. El anfitrión elige el modo y la cantidad exacta de humanos. Todos se preparan y el anfitrión inicia.

La dirección para compartir es independiente de la conexión local del anfitrión. **127.0.0.1 no sirve para invitar a otra PC.** La invitación empaqueta dirección, puerto y sala; no está cifrada, no es un secreto criptográfico y no abre puertos. No se consulta un servicio HTTP para averiguar la IP pública ni se instaló un relay. WAN necesita una ruta UDP alcanzable; CGNAT puede impedir el acceso directo. La guía [LEEME](distribution/LEEME.md) desarrolla LAN, Internet y diagnóstico. La conexión avanzada conserva campos separados de dirección/puerto/código.

## Equipos y espacios

El anfitrión define **entre 1 y 5 humanos exactos**; todos los demás serán mosquitos. Ambos bandos deben tener participantes: **1v1 es válido**. Límites técnicos provisionales: **12 mosquitos y 16 personas en total**. Una combinación imposible informa su motivo, sin reducir silenciosamente la cantidad elegida.

Nadie elige equipo en la sala social. Cada ronda y revancha hacen un sorteo independiente; puede repetirse el rol varias veces. Cambiar reglas invalida los listos.

La espera transcurre en un **patio de 8 × 6 × 4 m**, con bancos y personajes de espera; no anticipa equipos ni reutiliza puestos u objetos de partida. La ronda usa la **casa de dos pisos, de 28 × 22 × 6,4 m**. Desde el botón de caminar podés recorrer la sala; Esc devuelve los paneles.

La casa ocupa **28 × 22 m**, con dos plantas a **0 y 3,2 m** y techo a **6,4 m**. Tiene **16 ambientes amueblados**, pasillos, puertas reales y dos escaleras laterales que ofrecen rutas alternativas. Las escaleras usan 16 peldaños de 0,2 m por lado y huecos reales en el entrepiso. Las **8 tareas y 8 herramientas recogibles** están distribuidas entre ambas plantas. Humanos y mosquitos aparecen separados, sin solapamiento ni línea de visión inicial entre bandos. Los bots pueden subir y bajar por rutas válidas.

El mosquito es aproximadamente **65% menor visualmente** que en 0.3 y usa radio físico **0,04 m**. El humano se representa con **15 piezas corporales** y **22 zonas** posibles ligadas a la pose compartida; con un solo humano se usan las 16 frontales. Materiales estilizados de madera, tela y paredes, marcos y mobiliario dan identidad a los ambientes sin cambiar las colisiones autoritativas.

## Controles y reglas

| Personaje | Controles iniciales |
|---|---|
| Humano | WASD y ratón; **Shift correr, Espacio saltar, Ctrl agacharse**; clic palmada/golpe; Q defensa propia; R recoger/cambiar; G soltar; E mantenida hacer tarea. |
| Mosquito | W hacia la mira en 3D; soltar frena; A/S/D relativo y ratón; Espacio/Ctrl altura auxiliar opcional; E mantenida concentrar y cargar, nueva E al picar desprende; F posarse. |

La defensa propia con Q está disponible antes de adherirse el mosquito. El HUD indica «Q cubrir cabeza/torso/piernas» según la mirada: frente/arriba para cabeza y hombros, algo abajo para torso y bien abajo para piernas. Con un humano, todas las zonas se defienden con las manos iniciales. Las traseras, habilitadas con varios humanos, requieren un compañero que apunte desde el lado expuesto. Matamoscas, raqueta eléctrica, diario y escoba ofrecen alcances, áreas y recuperaciones diferentes.

El mosquito vuela hacia donde apunta la cámara: **W avanza en 3D**, incluso al mirar arriba o abajo, y **soltar avance frena**. A/S/D conservan movimiento relativo; Espacio/Ctrl son ayudas de altura opcionales. Cerca de la marca propia, **mantener E concentra durante 1,2 s**, estabiliza y asiste el acercamiento. Soltar antes del anclaje cancela la carga. Ya picando, **una nueva pulsación de E desprende; soltarla no libera**. La carga exige alcance, orientación, lado exterior y recorrido libres; no atraviesa paredes ni el cuerpo.

Cada mosquito recibe solo su marca. Picar lo ancla al cuerpo; sigue sus movimientos, salto, postura y extremidades animadas. Conserva su zona mientras pica. **Otra pulsación de E desprende; soltar E no libera.** Al soltarse recibe otra zona inmediatamente y mantiene el calendario individual de rotación, sin contador visible.

| Modo | Victoria y vidas |
|---|---|
| Recolección de sangre | Cuota compartida; sangre conservada al desprenderse y morir. Mosquitos ganan al alcanzarla. Humanos ganan al vencer el tiempo o eliminar a todos. Una vida, sin reapariciones. |
| Supervivencia | Un mosquito vivo al final gana para su equipo. Humanos ganan si eliminan a todos antes. Una vida, sin hambre ni picadura obligatoria. |
| Tareas | Humanos cumplen la meta colectiva al final o agotan antes todas las vidas de los mosquitos. Cada mosquito tiene 3 vidas totales propias por defecto y reaparece mientras conserve alguna. Fallar reduce solo el plazo de futuras tareas del humano que falló. |

Una desconexión durante la ronda la interrumpe sin ganador y devuelve a sala. Un resultado ya cerrado se conserva. Los eliminados esperan sin cámara libre.

Valores candidatos: ronda **120 s**, cuota compartida **12**, rotación **14 s**. Extracción **0,8 unidades/s por mosquito**, con **tope agregado de equipo de 1 unidad/s**, después de **1 s de preparación** tras adherirse. La cuota configurada no cambia por escalado oculto. Tareas cada **36 s**, plazo inicial **30 s**, trabajo **3 s**, penalización propia **2 s**, piso **24 s**, meta **0 = automática** de dos tercios de oportunidades, redondeados hacia arriba. Tareas conserva **3 vidas totales personales** por defecto y **4 s** para reaparecer. Son hipótesis de prototipo; requieren juego humano para decidir balance.

El **piso predeterminado es 24 s**. El mínimo configurable de plazo y piso es **tiempo de trabajo + 21 s de traslado**; la frecuencia mínima es ese mínimo más **0,5 s**. Con el trabajo habitual de 3 s, los límites son 24 s de plazo y 24,5 s entre tareas; los valores iniciales siguen siendo 30 s y 36 s. La reserva permite recorrer la casa de dos pisos también caminando: el piso anterior de 8 s era menor que numerosos trayectos. La penalización continúa siendo personal y el piso queda visible en las reglas; no hay extensiones ocultas de plazo ni garantía de completar el trabajo bajo ataque.

**No aparecen nuevos encargos si el tiempo restante de ronda no alcanza para traslado y trabajo.** El plazo visible de una tarea tampoco supera el tiempo que queda de ronda. La meta automática cuenta únicamente esas oportunidades: con los valores iniciales hay **3 encargos posibles por humano** y una meta colectiva equivalente a **2 tareas por humano**, sumadas entre todos. La cadencia y la penalización personal se conservan. Al configurar rondas muy cortas de Tareas, el mínimo mostrado deja tiempo para el primer encargo de cada humano; por ejemplo, con 8 s de trabajo exige 33 s para un humano o 39 s para cinco. Sangre y Supervivencia conservan su mínimo de 30 s.

## Apariencia y menús

**TU PINTA** guarda color y accesorio por separado para cada rol, con vista previa 3D. Los cosméticos no cambian estadísticas ni colisiones. La migración por el cambio de nombre conserva las preferencias anteriores y no sobrescribe un perfil nuevo ya existente.

Esc/Volver restauran la pantalla y el foco anteriores; Esc cancela primero una captura de tecla. En partida, el menú libera el ratón y bloquea tus controles, pero **la ronda sigue tanto en práctica como online**. Se pueden reasignar teclas y ajustar sensibilidad por rol, volumen, inversión y pulso de marca.

## Fuentes y verificación

El proyecto está en **game/**. **work/setup-tools.ps1** restaura Godot y plantillas con sumas oficiales; **work/build.ps1** importa, comprueba y exporta. Los scripts de prueba no sustituyen la práctica jugable: esta usa **practice_session.gd** y **bot_brain.gd**; el harness ENet sirve para diagnósticos entre procesos.

Suites principales: **rules_test.gd**, **lobby_rules_test.gd**, **locomotion_test.gd**, **maps_test.gd**, **practice_test.gd** e **invitation_test.gd**. Se ejecutan con el editor Godot en modo headless, por ejemplo:

```powershell
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/locomotion_test.gd
```

La evidencia vigente está en [PRUEBAS.md](distribution/PRUEBAS.md); compilación y hash corresponden a [BUILD.txt](distribution/BUILD.txt). Loopback no certifica juego entre casas ni balance o rendimiento en el hardware del grupo.

Los datos de mapa viven en **map_catalog.gd**, las rutas reutilizables en **map_navigation.gd**, las colisiones y locomoción en **arena.gd**, las articulaciones y marcas compartidas en **human_pose.gd**, y las reglas en **simulation.gd**. **lobby_rules.gd** sortea equipos; **network.gd** aplica permisos y privacidad; **invitation.gd** codifica la conexión.

**0.4.0 / protocolo 4 / Windows x86_64 / OpenGL de compatibilidad.** Arte y audio procedural originales. Bangers y Atkinson Hyperlegible se distribuyen bajo SIL Open Font License; los avisos se incluyen con los recursos y licencias del paquete. Los artefactos 0.1, 0.2 y 0.3 permanecen como historia, sin usarse como evidencia de esta compilación.
