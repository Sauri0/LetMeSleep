# Let me sleep · 0.3.0 · Windows 64 bits

**Descomprimí toda la carpeta antes de jugar.** Abrí **Let-me-sleep.exe** o **Jugar.cmd**. No necesitás instalar Godot. Para jugar juntos, todos necesitan **0.3.0 / protocolo 3**.

## Probar ahora, sin organizar una sala

1. En el inicio elegí **PRÁCTICA**.
2. Elegí **HUMANO** o **MOSQUITO** y uno de los tres modos: **Recolección de sangre**, **Supervivencia** o **Tareas**.
3. Pulsá **¡A PRACTICAR!**.

Como humano te enfrentás a dos mosquitos automáticos; como mosquito, a un humano automático. Los rivales tienen comportamiento de juego: buscan objetivos, se desplazan, atacan, pican y hacen tareas según el modo. En Supervivencia los mosquitos intentan seguir vivos y no necesitan picar. La práctica corre en tu PC, sin conectarte a una sala ni abrir un servidor de red.

El cartel **PRÁCTICA / RIVALES AUTOMÁTICOS** identifica esta sesión. Al terminar podés **REPETIR PRÁCTICA** o **VOLVER AL MENÚ**. Las reglas de golpes, zonas, vidas y resultados son las mismas que con amigos. Los rivales son una ayuda para aprender; su dificultad y el balance siguen siendo valores de prototipo.

**Solo en práctica elegís el rol.** En una sala con amigos se sortea al comenzar cada ronda.

## Alojar una partida con amigos

1. En el inicio elegí **CREAR SALA** y escribí tu nombre.
2. Pulsá **1 · Encender servidor en esta PC** y luego **2 · Crear sala**. El servidor y tu juego corren como procesos separados. Tu cliente se conecta a **127.0.0.1:27840**.
3. En la sala abrí **Dirección para amigos**. Indicá la dirección que ellos pueden alcanzar y el puerto correspondiente. En la misma red podés elegir tu IP local en la lista. Para otras casas, leé el apartado de Internet.
4. Pulsá **GUARDAR Y COPIAR INVITACIÓN**. Compartí por tu chat habitual el texto completo que empieza por **DD3-**. **Invitar** permite copiarlo otra vez.
5. Tus amigos eligen **UNIRME CON INVITACIÓN**, escriben su nombre, pegan ese único texto y pulsan **Unirme a la sala**.
6. Elegí modo y **cantidad exacta de humanos, de 1 a 5**. Todos los demás serán mosquitos. Todos pulsan **Estoy listo** y el anfitrión inicia.
7. Al terminar, **VOLVER A LA SALA** prepara otra ronda. Todos vuelven a estar listos y el servidor hace un sorteo nuevo.

También podés iniciar el servidor con **Iniciar-servidor.cmd** y dejar su ventana abierta. Elegí una sola forma de arrancarlo para evitar que dos procesos intenten usar el mismo puerto. El servidor iniciado desde el juego se cierra al salir de ese juego.

La sala requiere ambos bandos: **1 contra 1 es válido**. Los límites provisionales son **12 mosquitos y 16 participantes totales**. Si la cantidad exacta de humanos no deja un equipo mosquito válido, aparece el motivo y no se inicia; no se modifica esa cantidad automáticamente.

**Nadie elige equipo en la sala social.** El sorteo es independiente en cada ronda y puede repetirte el mismo rol varias veces. Cambiar reglas vuelve a dejar a todos sin preparar.

## Qué contiene la invitación

La invitación **DD3-…** empaqueta **dirección, puerto y código de sala** para que el amigo pegue un solo texto. No está cifrada ni es un secreto criptográfico. Solo funciona mientras la sala exista y la dirección siga siendo alcanzable.

La conexión del anfitrión y la **Dirección para amigos** se guardan por separado. El anfitrión puede usar **127.0.0.1** en su propia PC; compartirla enviaría a cada amigo a su propia computadora, por eso no se acepta como dirección de invitación.

La invitación no abre puertos ni resuelve NAT. El juego no consulta servicios HTTP para detectar tu IP pública. No se instaló ni contrató relay, alojamiento o servicio externo.

Si necesitás ingresar datos separados, **Opciones de conexión** muestra dirección, puerto y código. Para usar esa vía al unirte, dejá vacío el campo de invitación.

## Amigos en la misma red / LAN

En **Dirección para amigos**, elegí la IPv4 del adaptador Wi-Fi o Ethernet de la red compartida, por ejemplo **192.168.1.25**. Si aparecen varias direcciones, usá la del adaptador por el que ambos equipos se conectan; una VPN u otro adaptador puede tener una dirección distinta. Podés compararlas con la salida de **ipconfig**.

Usá el puerto del servidor, **UDP 27840** por defecto, y copiá una invitación nueva si cambia la dirección o el puerto. La red debe permitir comunicación entre dispositivos; algunas redes de invitados la bloquean.

Si Windows pregunta por acceso del servidor, permitilo en la red donde van a jugar. Esta entrega no crea reglas globales de firewall automáticamente.

## Amigos desde otras casas / Internet

En **Dirección para amigos**, escribí una IP pública o un nombre de servidor alcanzable por ellos. El acceso directo requiere una ruta UDP hasta tu servidor.

Con un router doméstico que permita ese acceso, el esquema habitual es **UDP externo 27840 → IPv4 local de tu PC:27840**, junto con permiso de entrada al servidor en el firewall de esa PC. Si usás otro puerto externo, colocá ese número en **Puerto para amigos**. El cliente local puede seguir conectándose a 127.0.0.1:27840. La configuración concreta depende de tu router y conexión.

Si tu proveedor usa **CGNAT**, una redirección en tu router puede no alcanzar; habrá que resolver la conectividad con el proveedor o una solución de red compatible con UDP. La invitación no corrige CGNAT, doble NAT ni bloqueos del proveedor. Una comprobación de puertos que solo use TCP no verifica este servidor UDP.

Si un amigo no entra, revisen versión, invitación completa, servidor encendido, dirección elegida, puerto y ruta UDP. El juego no configura el router ni detecta públicamente esa ruta. La PC anfitriona debe seguir encendida y sin suspenderse.

## La previa y tu apariencia

La sala de espera es un **patio 3D separado de la casa de juego**, con bancos y espacio central. Usá el botón para caminar y recorré el patio con WASD, ratón, Shift para correr, Espacio para saltar y Ctrl para agacharte. **Esc** recupera los paneles de sala. Los avatares humanos de espera no anticipan el rol de la ronda.

Desde **TU PINTA** elegís color y accesorio para humano y mosquito por separado, con vista previa 3D. Las dos apariencias se guardan localmente y se aplica la del rol que toque. Son cambios visuales; no modifican velocidad, alcance, vidas ni colisiones.

Al pasar de la edición anterior a **Let me sleep**, el juego copia los ajustes anteriores cuando todavía no existen preferencias nuevas. Conserva apariencias y controles, deja intacto el archivo anterior y no sobrescribe un perfil nuevo.

## Controles iniciales

| Acción | Humano | Mosquito |
|---|---|---|
| Desplazarse / mirar | WASD / ratón | WASD / ratón |
| Shift | Correr | — |
| Espacio | Saltar | Subir volando |
| Ctrl | Agacharse | Bajar volando |
| Clic izquierdo | Palmada o golpe | — |
| Q | Defensa del propio cuerpo | — |
| E | Mantener para hacer tarea | Pulsar para picar; pulsar otra vez para desprenderse |
| R / G | Recoger o cambiar / soltar herramienta | — |
| F | — | Posarse cerca de una superficie / volver a volar |
| Esc | Abrir/cerrar menú | Abrir/cerrar menú |

Podés reasignar controles y ajustar sensibilidad por rol, volumen, inversión y pulso de marca. Las ayudas del juego reflejan tus teclas guardadas.

### Humano: defenderse y ayudar

Empezás con manos. Clic da una palmada; los objetos del mapa ofrecen golpes diferentes: matamoscas, raqueta eléctrica, diario enrollado y escoba. R recoge o cambia un objeto cercano; G lo deja y vuelve a las manos. Cada golpe necesita recuperarse antes de repetir.

Q defiende una banda del propio cuerpo según la mirada: **frente o arriba** para cabeza/hombros, **algo abajo** para torso y **bien abajo** para piernas. Con un solo humano, todas las marcas disponibles se pueden defender con las manos iniciales. Las marcas traseras se habilitan con varios humanos y requieren ayuda: acercate al lado expuesto de tu compañero y apuntá al mosquito con clic.

Correr, saltar y agacharse tienen colisiones y límites. El cuerpo, las extremidades y las marcas siguen la postura; saltar o agacharse no desprende por sí solo un mosquito adherido.

### Mosquito: picar y desprenderse

Acercate a tu marca privada sobre un humano y pulsá E. Quedás anclado al cuerpo y conservás esa zona mientras picás, incluso si el humano corre, salta o se agacha.

**Otra pulsación de E desprende; soltar la tecla no libera.** Al soltarte recibís otra zona inmediatamente, sin reiniciar el calendario individual de rotación. No hay cuenta regresiva de la marca. Si está detrás del cuerpo o de un mueble, rodealo para verla y alcanzarla.

F permite posarte solo cerca de piso, pared, techo o mueble; no congela el vuelo en el aire. Moverte vuelve a volar. Los eliminados esperan sin cámara libre ni marcas de compañeros.

## Tres modos

| Modo | Victoria y vidas |
|---|---|
| Recolección de sangre | Los mosquitos deben alcanzar una cuota compartida antes del reloj. Humanos ganan si lo impiden o eliminan a todos. La sangre obtenida se conserva al desprenderse y morir. Una vida por mosquito, sin reapariciones. |
| Supervivencia | Basta un mosquito vivo al terminar para que gane su equipo. Humanos ganan si eliminan a todos antes. Una vida; sin hambre ni obligación de picar. |
| Tareas | Humanos cumplen la meta colectiva al final o ganan antes al agotar todas las vidas de los mosquitos. Cada mosquito tiene 3 vidas totales personales por defecto y reaparece mientras conserve alguna. |

En **Tareas**, mantené E junto al puesto indicado. Una picadura pausa el trabajo y conserva el avance. Cada fallo reduce **solo el plazo de futuras tareas del humano que falló**; no cambia la duración de ronda, frecuencia de tareas ni plazos de compañeros. La meta es colectiva, aunque el plazo y la penalización sean personales.

Tres vidas totales significan tres oportunidades de vivir, no tres reapariciones. Si todos están temporalmente muertos pero queda alguna vida, la ronda sigue. Si todos agotan sus vidas, los humanos ganan inmediatamente aunque falten tareas.

Valores iniciales de prototipo: ronda **120 s**, cuota **30**, rotación **14 s**. Tareas cada **24 s**, plazo **18 s**, trabajo **3 s**, penalización **2 s**, piso **8 s**; meta automática de dos tercios de las oportunidades, redondeados hacia arriba. Tareas usa **3 vidas personales**, configurables entre 1 y 9, con reaparición tras **4 s** por defecto. No son valores de balance ya validados.

## Menús, resultados y desconexión

Esc o Volver cierran la vista activa y recuperan el foco anterior; al reasignar una tecla, Esc cancela primero esa captura. En partida, Esc libera el ratón y bloquea tus controles. **La ronda continúa, tanto en práctica como online.**

Una desconexión durante la ronda online la interrumpe sin ganador y devuelve al grupo a sala. Reconectar permite volver a la sala, no recuperar esa ronda. Una salida después de un resultado ya cerrado no cambia el ganador.

## Archivos y comprobaciones

**Godot 4.5.2 / Windows x86_64 / OpenGL de compatibilidad.** La carpeta incluye cliente, servidor mediante script, guía y avisos de licencia. Bangers para títulos de cómic y Atkinson Hyperlegible para lectura usan **SIL Open Font License**. Arte y audio procedural originales; no hay cuentas, tienda ni servicios contratados.

La evidencia vigente, su alcance y los pendientes están en [PRUEBAS.md](PRUEBAS.md). La identificación de compilación y el SHA256 del ejecutable corresponden a [BUILD.txt](BUILD.txt). Las partidas locales automatizadas no sustituyen jugar entre conexiones distintas ni medir comodidad, balance o rendimiento en las PCs del grupo.
