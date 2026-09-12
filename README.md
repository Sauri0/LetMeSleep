# Let me sleep — 0.9.2 para Windows

[Descargar 0.9.2](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.2).
Descomprimí el ZIP completo y abrí Let-me-sleep.exe.

Casa reorganizada, manos/agarres corregidos, personalización facial revisada
y cámara del mosquito con zoom y giro independiente al quedarse quieto.
Online integrado por invitación LMS1-. Todos deben usar0.9.2/protocolo11.
La compilación completa y9escenarios del EXE pasaron. La prueba entre dos
casas sigue pendiente. Alcance: [PRUEBAS](distribution/PRUEBAS.md).

**[Versiones publicadas y descargas](https://github.com/Sauri0/LetMeSleep/releases)**

Descomprimí todo el ZIP y abrí **Let-me-sleep.exe**. Elegí **PRÁCTICA** para jugar contra bots. No necesitás Godot ni una cuenta de GitHub. La guía **LEEME.html** acompaña al juego.

Juego de humanos contra mosquitos: humano en primera persona, mosquito en tercera y tres modos. Cada ronda genera una casa de dos o tres pisos, con puertas interactivas, tareas y herramientas. La casa v2 limita el total a 22 habitaciones y reserva pasillos a los lados de las escaleras. El HUD es compacto; F1 abre una guía que permanece plegada durante el juego. La cámara permite seguir girando al mirar el cuerpo.

La fuente integra **Epic Online Services**: Crear sala ejecuta el servidor dentro del juego y produce una invitación **LMS1-**. Los amigos pegan ese código, sin indicar IP ni instalar otra aplicación. Se probaron creación, autenticación y cierre reales del anfitrión; **la partida entre dos identidades y redes independientes sigue pendiente de validar**. El modo avanzado ENet directo conserva las invitaciones DD5 y sus requisitos de conectividad.

## Jugar

- **Práctica:** elegí humano o mosquito y uno de los tres modos. Los bots se mueven, atacan, pican, completan tareas y ayudan a sus compañeros aturdidos.
- **Crear sala:** un botón conecta con Epic e inicia la sala dentro del juego. No hace falta abrir otra consola ni configurar un puerto.
- **Invitar:** copiá la invitación LMS1- desde la sala; tus amigos la pegan en **UNIRME CON INVITACIÓN**. Todos deben usar la misma versión. La sala termina si sale el anfitrión.
- **Equipos:** el anfitrión elige entre 1 y 5 humanos exactos; el resto son mosquitos, con un máximo de 12 y 16 jugadores totales. 1v1 es válido. Los roles se sortean en cada ronda.

## Controles y modos

| Personaje | Controles iniciales |
|---|---|
| Humano | WASD y ratón; Shift correr, Espacio saltar, Ctrl agacharse; mirar hacia abajo para inspeccionar el cuerpo y clic para golpear donde apuntás. Q es otra tecla para la misma palmada manual. R recoge, G suelta; mantener clic derecho carga el diario o la pantufla y soltar lanza. E abre/cierra una puerta cercana y E mantenida hace tareas. |
| Mosquito | W avanza hacia la mira en 3D; soltar frena. A/S/D relativo, Espacio/Ctrl altura auxiliar. F permite posarse o despegar; WASD recorre pisos, paredes y techos al posarse. E mantenida concentra antes de picar; una nueva pulsación desprende. E mantenida cerca de un compañero caído ayuda a recuperarlo. |

**B** abre los gestos humanos y **V** mantenida transmite voz a jugadores cercanos.
Las teclas se pueden cambiar en ajustes. Elegí allí la entrada de micrófono,
el silencio propio y los controles por interlocutor. Los mosquitos tienen un
tono más agudo sin hablar más rápido; los humanos los oyen a menor volumen y
distancia. Las puertas y el recorrido entre habitaciones afectan la escucha.

La defensa no selecciona automáticamente zonas. La cámara permite inspeccionar pecho, abdomen, antebrazos y muslos; las marcas y golpes siguen la misma pose física. Con varios humanos pueden aparecer zonas traseras que requieren ayuda de un compañero. Cada mosquito ve únicamente su propia marca. Al desprenderse recibe otra y conserva su calendario individual de rotación.

| Modo | Regla |
|---|---|
| Recolección de sangre | Mosquitos ganan al alcanzar la cuota compartida; humanos al agotar el tiempo. Los golpes aturden durante 35 segundos. La sangre acumulada se conserva. |
| Supervivencia | Un mosquito vivo al final gana para su equipo. Los humanos ganan si eliminan a todos antes. Una vida, eliminación definitiva. |
| Tareas | Los humanos cumplen la meta colectiva al final de la ronda. Fallar reduce solamente el plazo de futuras tareas del humano que falló. Los mosquitos quedan aturdidos 35 segundos al recibir un golpe. |

En Sangre y Tareas, el mosquito cae al piso y recupera el control en ese lugar. Otro mosquito puede mantener **E** cerca, mirando al caído y sin obstáculos, para acelerar el tiempo a **4×**: ayudar durante todo el período lo reduce a unos **8,75 segundos**. Varios ayudantes no suman velocidad. Los golpes posteriores no reinician el contador y que todos estén aturdidos no termina la ronda.

Valores iniciales de prototipo: ronda 120 s, cuota 12, rotación 14 s. Extracción 0,8 unidades/s por mosquito, tope agregado de 1 unidad/s y 1 s de preparación. Tareas cada 36 s, plazo 30 s, trabajo 3 s y piso 24 s; se reservan 21 s de traslado. No se asignan encargos que no puedan caber en el tiempo restante. La meta automática usa dos tercios de las oportunidades, redondeados hacia arriba. El balance requiere partidas humanas.

## Interfaz y aspecto

Se eligieron el humano A compacto, el mosquito B alargado y el acabado liso de paredes y suelo. Se conservan paletas por habitación, juntas, muebles y materiales propios de cada objeto. Seis caras con diez controles faciales acompañan los estados del personaje. La piel conserva su color original y las superficies visibles comparten postura con picaduras e impactos.

El HUD deja libre el centro y muestra indicaciones según la acción. **F1** abre y cierra la guía; **Esc** abre la pausa y los ajustes; la ronda sigue mientras el menú está abierto. **TU PINTA** tiene una vista previa 3D con color, accesorio, cara, pelo o antenas, ropa o abdomen, calzado y acento separados para cada rol. Las opciones se eligen con tarjetas y muestras de color. Los perfiles nuevos usan pijama, pantuflas y gorro de noche; se conservan las elecciones guardadas en versiones anteriores. Los cosméticos no cambian estadísticas ni colisiones.

Las puertas tienen bisagras, bloqueo seguro y sonido espacial; los bots las usan y los mosquitos caben bajo ellas. La mira muestra una oportunidad discreta basada en el mismo recorrido manual y línea de visión del golpe. Ajustes permite resolución real hasta 4K, VSync, FPS, sombras y reflejos. La prueba actual de 16 participantes a 1080p todavía no alcanza una fluidez sostenida de 60 FPS; la optimización sigue pendiente.

La sala de espera es un patio independiente. El anfitrión comparte la semilla y la huella de la casa antes de empezar, para que todos jueguen en la misma distribución. Hay tareas y herramientas en todos los pisos. Una desconexión interrumpe la ronda sin ganador. Al salir el anfitrión se cierra su sala; no existe migración de anfitrión.

## Proyecto y verificación

Godot **4.5.2 stable**, GDScript, Windows x86_64, OpenGL de compatibilidad y protocolo **11**; las versiones anteriores son incompatibles. El proyecto está en **game/**. **work/setup-tools.ps1** prepara herramientas con sumas oficiales; **work/build.ps1** importa, prueba y exporta, y **work/package.ps1** empaqueta sin sobrescribir versiones publicadas. La voz usa la extensión Opus incluida; sus fuentes, construcción reproducible y avisos están en **native/voice/** y **game/addons/lms_opus/licenses/**.

```powershell
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/stun_help_test.gd
```

La documentación de **distribution/** acompaña al paquete que se está preparando; **ENTREGA-0.7.md** es histórica. Consultá [el cierre de la descarga publicada](work/CIERRE-0.9.0-rc.1.md) y [el plan de las próximas entregas](work/RELEASE-PLAN-0.9.1-0.9.2.md). Las pruebas entre procesos de una misma PC no certifican Internet entre casas, rendimiento mínimo ni balance humano.

Modelos originales con fuentes Blender y exportaciones GLB en `art_source/`. Composición musical y efectos originales; las notas instrumentales acústicas de VSCO 2 Community Edition son CC0 y tienen créditos y licencia incluidos. Los temas se funden entre menú, personalización y partida; Música, Efectos, Ambiente e Interfaz tienen volumen separado además del volumen general. Bangers y Atkinson Hyperlegible se distribuyen bajo SIL Open Font License; sus avisos acompañan al paquete. Las versiones históricas se conservan en las Releases.
