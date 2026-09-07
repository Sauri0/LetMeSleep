# Let me sleep — prototipo 0.5.0

**[Descargar para Windows](https://github.com/Sauri0/LetMeSleep/releases/download/v0.5.0/Let-me-sleep-0.5.0-Windows.zip)** · [Todas las versiones](https://github.com/Sauri0/LetMeSleep/releases)

Descomprimí todo el ZIP y abrí **Let-me-sleep.exe**. Elegí **PRÁCTICA** para jugar contra bots. No necesitás Godot ni una cuenta de GitHub. La guía **LEEME.html** acompaña al juego.

Juego de humanos contra mosquitos: humano en primera persona, mosquito en tercera, casa caricaturesca de dos pisos y tres modos. La versión 0.5 reduce el HUD, incorpora defensa manual sobre el cuerpo visible, amplía la personalización y mejora el mobiliario y las animaciones.

**La conexión automática entre casas todavía está pendiente.** Esta entrega usa ENet directo: funciona en una LAN o con una dirección UDP alcanzable. La invitación DD3 contiene dirección, puerto y sala; no abre puertos ni supera CGNAT. La integración EOS requiere adaptación, configuración del producto y pruebas en conexiones independientes. No hay SDK ni credenciales EOS en este juego.

## Jugar

- **Práctica:** elegí humano o mosquito y uno de los tres modos. Los bots se mueven, atacan, pican, completan tareas y ayudan a sus compañeros aturdidos.
- **Crear sala:** un botón inicia el servidor en la PC y entra automáticamente. No hace falta abrir otra consola. El puerto local se conserva separado de la dirección para amigos.
- **Invitar:** copiá la invitación DD3 desde la sala; tus amigos la pegan en **UNIRME CON INVITACIÓN**. La conexión directa necesita una ruta de red alcanzable.
- **Equipos:** el anfitrión elige entre 1 y 5 humanos exactos; el resto son mosquitos, con un máximo de 12 y 16 jugadores totales. 1v1 es válido. Los roles se sortean en cada ronda.

## Controles y modos

| Personaje | Controles iniciales |
|---|---|
| Humano | WASD y ratón; Shift correr, Espacio saltar, Ctrl agacharse; mirar hacia abajo para inspeccionar el cuerpo y clic para golpear donde apuntás. Q es otra tecla para la misma palmada manual. R recoge, G suelta y E mantenida hace tareas. |
| Mosquito | W avanza hacia la mira en 3D; soltar frena. A/S/D relativo, Espacio/Ctrl altura auxiliar. E mantenida concentra antes de picar; una nueva pulsación desprende. E mantenida cerca de un compañero caído ayuda a recuperarlo. |

La defensa no selecciona automáticamente zonas. La cámara permite inspeccionar pecho, abdomen, antebrazos y muslos; las marcas y golpes siguen la misma pose física. Con varios humanos pueden aparecer zonas traseras que requieren ayuda de un compañero. Cada mosquito ve únicamente su propia marca. Al desprenderse recibe otra y conserva su calendario individual de rotación.

| Modo | Regla |
|---|---|
| Recolección de sangre | Mosquitos ganan al alcanzar la cuota compartida; humanos al agotar el tiempo. Los golpes aturden durante 35 segundos. La sangre acumulada se conserva. |
| Supervivencia | Un mosquito vivo al final gana para su equipo. Los humanos ganan si eliminan a todos antes. Una vida, eliminación definitiva. |
| Tareas | Los humanos cumplen la meta colectiva al final de la ronda. Fallar reduce solamente el plazo de futuras tareas del humano que falló. Los mosquitos quedan aturdidos 35 segundos al recibir un golpe. |

En Sangre y Tareas, el mosquito cae al piso y recupera el control en ese lugar. Otro mosquito puede mantener **E** cerca, mirando al caído y sin obstáculos, para acelerar el tiempo a **4×**: ayudar durante todo el período lo reduce a unos **8,75 segundos**. Varios ayudantes no suman velocidad. Los golpes posteriores no reinician el contador y que todos estén aturdidos no termina la ronda.

Valores iniciales de prototipo: ronda 120 s, cuota 12, rotación 14 s. Extracción 0,8 unidades/s por mosquito, tope agregado de 1 unidad/s y 1 s de preparación. Tareas cada 36 s, plazo 30 s, trabajo 3 s y piso 24 s; se reservan 21 s de traslado. No se asignan encargos que no puedan caber en el tiempo restante. La meta automática usa dos tercios de las oportunidades, redondeados hacia arriba. El balance requiere partidas humanas.

## Interfaz y aspecto

El HUD deja libre el centro y muestra indicaciones según la acción. **Esc** abre los controles y ajustes; la ronda sigue mientras el menú está abierto. **TU PINTA** tiene una vista previa 3D con color, accesorio, cara, pelo o antenas, ropa o abdomen y acento separados para cada rol. Los cosméticos no cambian estadísticas ni colisiones.

La sala de espera es un patio independiente. La casa tiene 16 ambientes, dos escaleras y tareas y herramientas en ambas plantas. Una desconexión interrumpe la ronda sin ganador. Al salir el anfitrión se cierra su sala; no existe migración de anfitrión.

## Proyecto y verificación

Godot **4.5.2 stable**, GDScript, Windows x86_64, OpenGL de compatibilidad y protocolo **5**; 0.4 es incompatible. El proyecto está en **game/**. **work/setup-tools.ps1** prepara herramientas con sumas oficiales; **work/build.ps1** importa, prueba y exporta, y **work/package.ps1** empaqueta sin sobrescribir versiones publicadas.

```powershell
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/stun_help_test.gd
```

La evidencia y sus límites están en [PRUEBAS.md](distribution/PRUEBAS.md); el hash del ejecutable en [BUILD.txt](distribution/BUILD.txt); las instrucciones de juego en [LEEME.md](distribution/LEEME.md). Las pruebas entre procesos de una misma PC no certifican Internet entre casas, rendimiento mínimo ni balance humano.

Arte y audio procedural originales. Bangers y Atkinson Hyperlegible se distribuyen bajo SIL Open Font License; sus avisos acompañan al paquete. Las versiones históricas se conservan en las Releases.
