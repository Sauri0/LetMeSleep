# Dejame dormir · 0.1.0 · Windows 64 bits

**Descomprimí toda la carpeta antes de jugar. No necesitás instalar Godot.**

## Tu PC aloja la partida

1. Abrí `Iniciar-servidor.cmd` y dejá su ventana abierta. También podés usar el botón «Iniciar servidor local» del juego; ese servidor se cierra al salir del juego que lo inició. Elegí una sola de estas formas.
2. Abrí `Dejame-dormir.exe` o `Jugar.cmd`.
3. Escribí tu nombre. En tu propia PC, dirección **127.0.0.1**, puerto **27840**. Pulsá Crear sala.
4. Pasales a tus amigos **la dirección del servidor, el puerto y el código que aparece en la sala**. El código dura mientras exista esa sala y no reemplaza la dirección.
5. Elijan equipos y modo. Todos pulsan Estoy listo; quien creó la sala inicia. Mínimo 1 humano y 2 mosquitos. Máximo confirmado 5 humanos; al menos 2 mosquitos por humano al comenzar. Topes técnicos de esta prueba: 12 mosquitos y 16 personas en total.
6. Al terminar, el anfitrión vuelve a sala, pueden cambiar equipos y todos vuelven a prepararse.

## Amigos en la misma casa / LAN

En la PC que corre el servidor abrí una terminal y ejecutá `ipconfig`. Buscá la **Dirección IPv4 del adaptador Wi‑Fi o Ethernet conectado**. Por ejemplo, 192.168.1.25. Los amigos usan esa dirección, puerto27840 y código; no usan127.0.0.1. Todos deben estar en una red que permita comunicación entre dispositivos (una red de invitados puede bloquearla).

Si Windows pregunta por el servidor en el firewall, permití el acceso para la red donde van a jugar. Esta entrega no agrega reglas de firewall automáticamente.

## Amigos desde otras casas / Internet

Para acceso directo necesitás que tu conexión tenga una dirección pública alcanzable. En el router del anfitrión, una redirección **UDP externo27840 → IPv4 local de tu PC:27840**, y permitir ese tráfico al ejecutable en el firewall de esa PC. Conviene reservar esa IPv4 local en el router. Tus amigos ingresan tu **IP pública**, el mismo puerto y el código.

No se configuró tu router ni firewall desde esta tarea. El menú exacto depende de tu router. Si la IP WAN que muestra el router es privada o está en100.64.0.0/10, o si tu proveedor usa CGNAT, una redirección local por sí sola puede no alcanzar. En ese caso hace falta revisar la conexión con el proveedor o elegir una red privada/relay compatible con UDP; no se instaló ni contrató uno. No abras puertos al azar ni desactives todo el firewall. Un sitio que solo prueba TCP no verifica este servidor UDP.

**Todavía falta probar desde computadoras y conexiones distintas.** Los procesos locales verificaron funcionamiento técnico, no conectividad desde la casa de tus amigos. Tu PC y servidor deben permanecer encendidos; suspenderlos corta la partida.

## Cómo se juega

**Humano:** WASD+ratón, clic palmada/golpe, Q defensa propia, R recoger/cambiar objeto cercano, G soltar. Q apunta a cabeza/hombros mirando al frente o arriba, torso mirando algo abajo, piernas mirando bien abajo. Para sacar un mosquito de la espalda de un compañero, acercate por detrás y apuntale con clic. Empezás con manos; el mapa tiene matamoscas, raqueta, diario y escoba con alcances, áreas y recuperaciones diferentes. E mantenida realiza tu tarea cerca del puesto.

**Mosquito:** WASD+ratón, Espacio subir, Ctrl bajar. Buscá tu marca privada visible sobre un humano y acercate. E inicia picadura; **otra pulsación de E te desprende**. Soltar E no te libera. Mientras picás viajás con el humano. Tu marca cambia a otra al soltarte y también rota según un calendario fijo sin contador visible. Si la marca está del otro lado del cuerpo, volá alrededor: no atraviesa paredes ni personas. F permite posarte cerca de una superficie; moverte te hace volar de nuevo.

Esc abre menú/ajustes; **no pausa la partida online**. Podés reasignar controles, cambiar sensibilidad por rol, volumen, inversión y pulso de marca. La espera al morir no tiene cámara libre.

## Tres modos

| Modo | Victoria y vidas |
|---|---|
| Sangre | Mosquitos alcanzan una cuota compartida antes del reloj. Humanos ganan si lo impiden o eliminan a todos. La sangre ya obtenida se conserva; no hay reapariciones. |
| Supervivencia | Basta que quede un mosquito vivo al terminar. Humanos ganan si eliminan a todos antes. No hay picaduras obligatorias ni reapariciones. |
| Dejanos dormir | Humanos cumplen una meta colectiva de tareas al final. También ganan inmediatamente si todos los mosquitos agotan sus vidas personales. Cada mosquito comienza con3vidas totales por defecto; pierde1al morir y reaparece si le queda alguna. |

En Tareas, cada fallo reduce **solo el plazo de futuras tareas de ese humano**. No cambia la ronda, la frecuencia ni el plazo del compañero. La picadura pausa el avance mientras dura, conservando el progreso. Los relojes de ronda y tarea se muestran por separado.

Si un participante pierde conexión durante una ronda, se interrumpe sin ganador y se vuelve a sala. Puede reconectarse a la sala, pero no recuperar la ronda interrumpida.

## Valores iniciales y límites

Ronda120s; sangre30unidades; rotación14s; tareas cada24s, plazo18s, trabajo3s, penalización2s con piso8s. Meta automática: dos tercios de las oportunidades de tarea, redondeado hacia arriba. Reaparición en Tareas tras4s. **Estos valores son hipótesis ajustables;3vidas por mosquito es el default confirmado por el usuario.** Cada modo usa solo sus parámetros pertinentes.

Herramientas provisionales: manos alcance1.35m/recuperación0.80s; matamoscas1.65m/0.60s; raqueta1.75m/1.05s; diario1.50m/0.43s; escoba2.20m/1.20s. Área distinta en cada una. Las palmadas permiten defender las zonas habilitadas al jugar con un humano solo.

Versión0.1.0 / protocolo1 / Godot4.5.2. Solo Windows64bits en esta entrega. Gráficos OpenGL de compatibilidad, arte y audio originales. Ejecutable sin firma comercial: el hashSHA256 del paquete permite identificar esta compilación. No hay compras, cuentas, tienda ni servidor externo contratado.

Se probaron reglas, procesos ENet locales y renderización nativa; falta juego humano por Internet, balance, evaluación auditiva y rendimiento en las otras PCs. Ver `PRUEBAS.md` junto al ZIP para evidencia detallada.

Licencias del motor y componentes en `LICENCIAS-GODOT.txt`.
