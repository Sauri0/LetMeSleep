# Dejame dormir — prototipo 0.2.0

Juego nativo para Windows de humanos contra mosquitos, con una casa estilizada y personajes caricaturescos. Godot **4.5.2 stable**, GDScript y servidor autoritativo ENet/UDP en la PC del anfitrión. No requiere Steam, navegador ni cuentas.

Esta versión incorpora sorteo de equipos por ronda, sala de espera 3D y apariencia guardada por rol. Reglas, red local, navegación, persistencia y exportación de **0.2.0** están verificadas. Ver **distribution/PRUEBAS.md**.

## Sala y equipos

Una persona inicia el servidor, crea la sala y comparte dirección, puerto **UDP 27840** y código. El código identifica la sala en ese servidor; requiere una dirección alcanzable y no resuelve NAT por sí solo. El juego del anfitrión y el servidor son procesos separados. No se contrató alojamiento ni se configuraron router o firewall automáticamente.

El anfitrión define la **cantidad exacta de humanos: de 1 a 5**. Todos los demás serán mosquitos. Se puede jugar **1 contra 1**; ambos equipos deben tener participantes. Los límites provisionales son **12 mosquitos y 16 personas en total**. La sala explica una configuración imposible y no reduce automáticamente el número de humanos para acomodarla.

Nadie elige equipo. Todos pulsan **Estoy listo** y el servidor sortea los roles al comenzar cada ronda. Cada sorteo es independiente: puede tocarte el mismo rol varias veces seguidas. Al finalizar, el anfitrión vuelve a sala y el grupo vuelve a prepararse para un sorteo nuevo.

En la sala 3D podés recorrer el espacio con **WASD y ratón** mediante el botón para caminar. **Esc** devuelve el control a los paneles de sala. Los personajes humanos de espera no anticipan el rol que tocará después.

## Apariencia y navegación

Desde **Personalizar** podés guardar color y accesorio por separado para humano y mosquito, con vista previa 3D. Las preferencias persisten localmente al cerrar el juego. Al comenzar la ronda se aplica la apariencia del rol sorteado; colores y accesorios no alteran golpes, colisiones, alcance ni vidas.

**Esc** y los botones **Volver** permiten salir de las vistas de personalización y ajustes, recuperando el foco del menú anterior. Mientras se espera una tecla para reasignar, Esc cancela esa captura. Durante la partida, Esc abre o cierra el menú y libera o captura el ratón. **El menú no pausa una partida online.**

## Reglas y controles

**Humano, primera persona:** WASD y ratón; clic da una palmada o golpe. Q defiende una banda del propio cuerpo según la mirada: frente/arriba para cabeza y hombros, algo abajo para torso, bien abajo para piernas. Las zonas traseras requieren un compañero. R recoge o cambia una herramienta cercana y G la suelta. Empezás con manos; matamoscas, raqueta eléctrica, diario y escoba tienen alcance, área y recuperación distintos. E mantenida realiza la tarea junto a su puesto si no te están picando.

**Mosquito, tercera persona:** WASD, Espacio para subir y Ctrl para bajar. E pulsada cerca de tu marca inicia la picadura; **otra pulsación de E desprende**. Soltar E no libera. Mientras picás quedás anclado al cuerpo y conservás esa zona. Desprenderte asigna otra inmediatamente sin reiniciar el calendario individual de rotación. No se muestra cuenta regresiva de la marca y solo recibís tu propia asignación. F permite posarte cerca de una superficie; moverte vuelve a volar.

| Modo | Condición |
|---|---|
| Recolección de sangre | Mosquitos ganan al alcanzar la cuota compartida. Humanos ganan al vencer el reloj o eliminar a todos. La sangre obtenida se conserva al desprenderse o morir. Sin reapariciones. |
| Supervivencia | Al menos un mosquito vivo al finalizar gana para su equipo. Humanos ganan si eliminan a todos antes. Sin hambre, picaduras obligatorias ni reapariciones. |
| Dejanos dormir | Humanos cumplen una meta colectiva de tareas al final o ganan antes si los mosquitos agotan todas sus vidas. Cada mosquito tiene 3 vidas totales personales por defecto y reaparece mientras conserve alguna. Fallar solo reduce el plazo de futuras tareas del humano que falló. |

Una desconexión durante la ronda la interrumpe sin ganador y devuelve al grupo a sala. La reconexión no restaura esa ronda. Los eliminados ven una espera neutra, sin cámara libre.

## Fuente y verificación

El proyecto está en **game/**. Las herramientas oficiales se guardan en **work/tools/**, sin instalación global; **work/setup-tools.ps1** restaura editor y plantillas y verifica sus sumas oficiales. El ZIP de fuentes no necesita incluir esas dependencias.

Pruebas directas desde PowerShell, una vez disponibles las herramientas:

```powershell
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/rules_test.gd
& ./work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe --headless --path game --script res://tests/lobby_rules_test.gd
```

**work/build.ps1** es el punto de entrada para importación y exportación. **work/test-network.ps1** ejecuta diagnósticos con procesos locales; los bots no son rivales disponibles para jugar. La evidencia vigente y las pruebas pendientes están en **distribution/PRUEBAS.md**. Ninguna prueba loopback sustituye una sesión entre computadoras y conexiones distintas.

## Módulos

- **simulation.gd**: autoridad de movimiento, vidas, zonas, tareas, herramientas y finales.
- **lobby_rules.gd**: capacidad, preparación y sorteo independiente del equipo.
- **arena.gd**: geometría y colisiones compartidas.
- **network.gd**: sala registrada por código, compatibilidad, permisos y estados privados dirigidos.
- **client.gd**: controles, cámaras, espera 3D y representación interpolada.
- **world.gd**, **actor_view.gd**, **audio_fx.gd**: arte y audio procedural original.
- **ui.gd**, **preferences.gd**, **cosmetics.gd**: menús, ajustes y apariencia persistente.

La documentación vigente corresponde a **0.2.0 / protocolo 2**. Los entregables anteriores de 0.1.0 se conservan como historia y no certifican esta compilación.