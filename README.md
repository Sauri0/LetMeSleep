# Let me sleep — prototipo 0.3.0

Juego nativo para Windows de humanos contra mosquitos: humano en primera persona, mosquito en tercera, casa caricaturesca y tres modos. Godot **4.5.2 stable**, GDScript y autoridad de juego en la PC anfitriona. No requiere Steam, navegador ni cuentas.

La versión 0.3 incorpora **práctica local con rivales automáticos**, invitación de una sola entrada, carrera/salto/agacharse, animación corporal compartida y un patio de espera separado de la casa. La interfaz usa títulos de cómic con Bangers y texto Atkinson Hyperlegible. El nombre oficial es **Let me sleep**; al primer inicio se recuperan ajustes y apariencias de la edición anterior si todavía no existe un archivo nuevo.

## Jugar y practicar

Descomprimí **Let-me-sleep-0.3.0-Windows.zip** completo y abrí **Let-me-sleep.exe** o **Jugar.cmd**. No necesitás instalar Godot para jugar.

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

La espera transcurre en un **patio de 8 × 6 × 4 m**, con bancos y personajes de espera; no anticipa equipos ni reutiliza puestos u objetos de partida. La ronda usa la **casa de 12 × 10 × 2,8 m**. Desde el botón de caminar podés recorrer la sala; Esc devuelve los paneles.

## Controles y reglas

| Personaje | Controles iniciales |
|---|---|
| Humano | WASD y ratón; **Shift correr, Espacio saltar, Ctrl agacharse**; clic palmada/golpe; Q defensa propia; R recoger/cambiar; G soltar; E mantenida hacer tarea. |
| Mosquito | WASD y ratón; Espacio subir, Ctrl bajar; E pulsada picar/desprenderse; F posarse cerca de una superficie. |

La defensa propia con Q usa la mirada: frente/arriba para cabeza y hombros, algo abajo para torso y bien abajo para piernas. Con un humano, todas las zonas se defienden con las manos iniciales. Las traseras, habilitadas con varios humanos, requieren un compañero que apunte desde el lado expuesto. Matamoscas, raqueta eléctrica, diario y escoba ofrecen alcances, áreas y recuperaciones diferentes.

Cada mosquito recibe solo su marca. Picar lo ancla al cuerpo; sigue sus movimientos, salto, postura y extremidades animadas. Conserva su zona mientras pica. **Otra pulsación de E desprende; soltar E no libera.** Al soltarse recibe otra zona inmediatamente y mantiene el calendario individual de rotación, sin contador visible.

| Modo | Victoria y vidas |
|---|---|
| Recolección de sangre | Cuota compartida; sangre conservada al desprenderse y morir. Mosquitos ganan al alcanzarla. Humanos ganan al vencer el tiempo o eliminar a todos. Una vida, sin reapariciones. |
| Supervivencia | Un mosquito vivo al final gana para su equipo. Humanos ganan si eliminan a todos antes. Una vida, sin hambre ni picadura obligatoria. |
| Tareas | Humanos cumplen la meta colectiva al final o agotan antes todas las vidas de los mosquitos. Cada mosquito tiene 3 vidas totales propias por defecto y reaparece mientras conserve alguna. Fallar reduce solo el plazo de futuras tareas del humano que falló. |

Una desconexión durante la ronda la interrumpe sin ganador y devuelve a sala. Un resultado ya cerrado se conserva. Los eliminados esperan sin cámara libre.

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

Los datos de mapa viven en **map_catalog.gd**, las colisiones y locomoción en **arena.gd**, las articulaciones y marcas compartidas en **human_pose.gd**, y las reglas en **simulation.gd**. **lobby_rules.gd** sortea equipos; **network.gd** aplica permisos y privacidad; **invitation.gd** codifica la conexión.

**0.3.0 / protocolo 3 / Windows x86_64 / OpenGL de compatibilidad.** Arte y audio procedural originales. Bangers y Atkinson Hyperlegible se distribuyen bajo SIL Open Font License; los avisos se incluyen con los recursos y licencias del paquete. Los artefactos 0.1 y 0.2 permanecen como historia, sin usarse como evidencia de esta compilación.
