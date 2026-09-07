# Dejame dormir · 0.2.0 · Windows 64 bits

**Descomprimí toda la carpeta antes de jugar. No necesitás instalar Godot.** Todos los participantes deben usar la misma versión: **0.2.0, protocolo 2**.

## Tu PC aloja la partida

1. Abrí **Iniciar-servidor.cmd** y dejá su ventana abierta. También podés usar **Iniciar servidor local** desde el juego; ese servidor se cierra al salir del juego que lo inició. Elegí una sola de estas formas.
2. Abrí **Dejame-dormir.exe** o **Jugar.cmd**.
3. Escribí tu nombre. En la propia PC anfitriona usá dirección **127.0.0.1**, puerto **27840**, y pulsá **Crear sala**.
4. Compartí con tus amigos **la dirección del servidor, el puerto y el código de sala**. El código existe mientras dure esa sala y no reemplaza la dirección.
5. El anfitrión elige modo y **cantidad exacta de humanos, de 1 a 5**. Todos los demás serán mosquitos. Ambos equipos deben tener al menos un jugador: **1 contra 1 es válido**. Hay un tope provisional de **12 mosquitos y 16 personas en total**.
6. Todos pulsan **Estoy listo** y el anfitrión inicia. El servidor sortea quiénes serán humanos. **Nadie elige equipo y puede repetirse el mismo rol en rondas consecutivas.**
7. Al terminar, el anfitrión pulsa **Volver a la sala · revancha**. Todos vuelven a prepararse; se realiza un sorteo independiente para la próxima ronda.

Si faltan jugadores para la cantidad de humanos elegida, la sala explica el motivo y espera una configuración válida. No cambia esa cantidad automáticamente.

## Sala 3D y apariencia

Mientras esperan, pueden recorrer la casa con **WASD y ratón** usando el botón para caminar. **Esc** permite recuperar los paneles de sala. En espera se usa una representación humana; el equipo real se decide al comenzar la ronda.

**Personalizar** permite elegir color y accesorio para el humano y para el mosquito por separado, con una vista previa 3D. Las dos apariencias se guardan en esa computadora y se conservan al cerrar el juego. El rol sorteado usa su apariencia correspondiente. Son cambios visuales: no dan ventajas ni modifican las colisiones.

**Esc** o **Volver** cierran personalización y ajustes y devuelven el foco al menú anterior. Si estás reasignando una tecla, Esc cancela esa captura. En partida, Esc abre o cierra el menú y libera o captura el ratón. **La pausa no detiene el juego online.**

## Amigos en la misma casa / LAN

En la PC del servidor abrí una terminal y ejecutá **ipconfig**. Buscá la **Dirección IPv4 del adaptador Wi-Fi o Ethernet conectado**, por ejemplo 192.168.1.25. Los amigos usan esa dirección, puerto **27840** y código; **127.0.0.1** solo sirve en la propia PC que aloja el servidor. La red debe permitir comunicación entre dispositivos; algunas redes de invitados la bloquean.

Si Windows pregunta por acceso del servidor, permitilo en la red donde van a jugar. Esta entrega no agrega reglas de firewall automáticamente.

## Amigos desde otras casas / Internet

El acceso directo requiere una dirección pública alcanzable. En el router anfitrión se necesita redirigir **UDP externo 27840 → IPv4 local de tu PC:27840** y permitir ese tráfico al ejecutable en el firewall de esa PC. Conviene reservar esa IPv4 local en el router. Los amigos ingresan tu **IP pública**, el mismo puerto y el código.

El código identifica la sala y **no abre puertos ni resuelve NAT automáticamente**. No se configuraron tu router o firewall desde esta tarea. Si tu conexión usa CGNAT, una redirección local puede no alcanzar; habrá que revisar la conexión con el proveedor o una solución de red compatible con UDP. No se instaló ni contrató un relay o servidor externo. Una comprobación de puertos que solo use TCP no verifica este servidor UDP.

**Todavía falta probar entre computadoras y conexiones distintas.** La PC anfitriona y el servidor deben seguir encendidos: suspenderlos corta la partida.

## Cómo se juega

**Humano:** WASD y ratón; clic da palmada o golpe; Q defiende una parte del propio cuerpo. Mirá al frente o arriba para cabeza/hombros, algo abajo para torso y bien abajo para piernas. R recoge o cambia un objeto cercano; G lo suelta. Empezás con manos. Hay matamoscas, raqueta, diario y escoba con alcances, áreas y recuperaciones diferentes. Para quitar un mosquito de la espalda de un compañero, acercate por detrás y apuntale con clic. E mantenida realiza tu tarea junto al puesto.

**Mosquito:** WASD y ratón; Espacio sube y Ctrl baja. Acercate a tu marca privada sobre un humano y pulsá E para picar. **Otra pulsación de E te desprende; soltar la tecla no libera.** Mientras picás viajás con el cuerpo y conservás esa zona. Al soltarte cambia inmediatamente a otra y el calendario de rotación sigue su ritmo, sin contador visible. Si la marca queda detrás del cuerpo, volá alrededor. F permite posarte cerca de una superficie; moverte vuelve a volar.

Podés reasignar controles y ajustar sensibilidad por rol, volumen, inversión y pulso de marca. Los eliminados esperan sin cámara libre.

## Tres modos

| Modo | Victoria y vidas |
|---|---|
| Sangre | Mosquitos alcanzan la cuota compartida antes del reloj. Humanos ganan si lo impiden o eliminan a todos. La sangre obtenida se conserva al desprenderse y morir. No hay reapariciones. |
| Supervivencia | Basta que quede un mosquito vivo al terminar. Humanos ganan si eliminan a todos antes. No hay hambre, picaduras obligatorias ni reapariciones. |
| Dejanos dormir | Humanos cumplen la meta colectiva de tareas al final. También ganan inmediatamente si todos los mosquitos agotan sus vidas personales. Cada mosquito empieza con 3 vidas totales por defecto, pierde una al morir y reaparece si le queda alguna. |

En Dejanos dormir, cada fallo reduce **solo el plazo de futuras tareas de ese humano**. No cambia la ronda, la frecuencia ni el plazo del compañero. Una picadura pausa el trabajo conservando su progreso. Los relojes de tarea y ronda se muestran por separado.

Si alguien pierde conexión durante una ronda, se interrumpe sin ganador y el grupo vuelve a sala. Puede reconectarse, pero no recuperar la ronda interrumpida.

## Valores iniciales y estado de esta versión

Ronda **120 s**, sangre **30 unidades**, rotación **14 s**. Tareas cada **24 s**, plazo **18 s**, trabajo **3 s**, penalización **2 s**, piso **8 s**. Meta automática: dos tercios de las oportunidades de tarea, redondeado hacia arriba. En Dejanos dormir, reaparición después de **4 s** y **3 vidas personales totales** por defecto. El anfitrión puede configurar las vidas de ese modo entre 1 y 9. Cada modo utiliza sus parámetros correspondientes.

Estos tiempos, cuotas y estadísticas de herramientas son hipótesis de prototipo, sin balance validado. Elegir la cantidad de humanos configura el equipo completo; el sorteo decide qué personas lo integran.

**Godot 4.5.2 / Windows 64 bits / OpenGL de compatibilidad.** Arte y audio originales. No hay cuentas, tienda ni servicios contratados. Licencias en **LICENCIAS-GODOT.txt**.

La entrega **0.2.0** tiene verificadas las reglas, el sorteo, la red local, la interfaz y la exportación Windows. La evidencia y los límites están en [PRUEBAS.md](PRUEBAS.md), y el SHA256 del ejecutable definitivo está en [BUILD.txt](BUILD.txt). Falta probar partidas humanas entre conexiones de Internet diferentes; las validaciones de 0.1.0 se conservan como historia y no certifican esta versión.
