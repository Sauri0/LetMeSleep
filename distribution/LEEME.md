# Let me sleep · 0.9.1 · Windows 64 bits

Descomprimí toda la carpeta y abrí **Let-me-sleep.exe**. No necesitás Godot. Todos los jugadores de una sala deben usar **0.9.1 / protocolo 10**.

Esta versión mejora las proporciones y animaciones de los personajes, genera una casa nueva en cada ronda e incorpora puertas con bisagras, iluminación local y ajustes gráficos. La interfaz ocupa menos espacio y la cámara sigue girando al mirar hacia abajo. El menú usa Epic Online Services para crear salas e invitar con un código **LMS1-**. **Esta candidata todavía requiere comprobar una partida entre dos casas; consultá PRUEBAS.md para ver el alcance validado.**

## Empezar a jugar

Elegí **PRÁCTICA**, tu personaje y un modo. El humano juega contra dos mosquitos automáticos; el mosquito, contra un humano automático. La práctica funciona sin conexión ni servidor externo. En partidas con amigos, los roles se sortean cada ronda.

## Controles esenciales

| Acción | Humano | Mosquito |
|---|---|---|
| Moverse | WASD | WASD respecto de la mirada |
| Mirar | Mouse | Mouse; mirar arriba/abajo dirige el vuelo |
| Acción principal | Clic izquierdo: palmada o herramienta | Mantener E: concentrarse en la marca propia |
| Movimiento especial | Shift correr; Espacio saltar; Ctrl agacharse | Soltar movimiento frena; Espacio/Ctrl ajustan altura opcionalmente |
| Lanzar objetos | Mantener clic derecho para cargar; soltar para lanzar diario o pantufla | — |
| Objetos / superficies | R recoger o cambiar; G soltar | F posarse o volar |
| Puertas | Pulsar E mirando una manija cercana | Pasar por debajo; no puede abrirlas |
| Tareas | Mantener E cerca del puesto asignado | — |
| Gestos / voz | B gestos; mantener V para hablar | Mantener V para hablar |
| Guía de controles | F1 abre/cierra | F1 abre/cierra |
| Pausa y ajustes | Esc | Esc |

Son los valores iniciales; las teclas pueden reasignarse. Ajustes guarda sensibilidad separada por personaje, inversión vertical, volumen general, Música, Efectos, Ambiente, Interfaz y controles. La guía está plegada durante la partida; los avisos importantes de picadura, aturdimiento, ayuda, tareas y tiempo permanecen visibles. El menú libera el mouse y bloquea tus acciones, pero la ronda sigue avanzando.

## Herramientas y lanzamientos

Los humanos empiezan con las manos libres. Hay matamoscas, raqueta eléctrica, diario enrollado, escoba y pantufla de mano sobre muebles de la casa. Cada objeto tiene alcance, preparación y recuperación propios. El clic izquierdo conserva el golpe. Sólo el diario y la pantufla se lanzan: mantener clic derecho aumenta la fuerza; soltar inicia el lanzamiento. La gravedad curva la trayectoria y paredes, puertas, muebles y personajes la interrumpen. Cuando el objeto se detiene, puede recogerse de nuevo con R.

Esc, F1, abrir un menú, perder foco o cambiar de objeto cancelan la carga. Después hace falta una nueva pulsación para lanzar. La pantufla de mano es un objeto independiente del calzado del personaje. Los proyectiles no dañan humanos; a los mosquitos los aturden en Sangre/Tareas y eliminan en Supervivencia.

## Puertas e imagen

Mirá la manija de una puerta cercana y pulsá E cuando aparezca Abrir o Cerrar. Si alguien ocupa el recorrido de cierre, vuelve a abrirse; una apertura bloqueada se detiene. Las puertas cerradas frenan golpes y ocultan marcas. Los mosquitos pueden pasar por el hueco inferior.

En **Esc → Ajustes → Imagen y fluidez** podés elegir 720p, 1080p, 1440p o 4K, pantalla completa, VSync, límite de FPS, sombras y reflejos. Un perfil nuevo empieza **sin límite de FPS y con VSync desactivado**. Podés elegir un tope o activar VSync; tus elecciones quedan guardadas. Con VSync, los FPS dependen de la frecuencia del monitor. La resolución elegida es la de la imagen, incluso si la ventana se ajusta al escritorio. Sombras ligeras o desactivadas y reflejos apagados reducen el trabajo gráfico. Los reflejos del baño son una aproximación estática del ambiente.

## Si te están picando

Mirá hacia abajo y girá la vista hacia el mosquito visible sobre tu cuerpo. **Apuntale y hacé clic izquierdo.** La vista puede girar respecto del torso para alcanzar pecho, abdomen, antebrazos y muslos. Podés seguir girando aunque estés mirando abajo: el torso acompaña al superar el giro cómodo de la cabeza. Al volver a mirar al frente, el cuerpo acompaña suavemente.

El pequeño arco amarillo alrededor de la mira indica una oportunidad visible dentro del recorrido de la herramienta; no selecciona blancos ni garantiza acertar. La palmada tiene recorrido y recuperación: apuntar cerca no garantiza acertar. El aviso «¡Tocó!» confirma un impacto; el fallo también tiene respuesta. Q conserva una segunda tecla para la misma palmada manual. Si sostenés una herramienta y apuntás a su brazo, la otra mano puede dar la palmada.

Las marcas de espalda aparecen sólo con varios humanos y requieren ayuda de otro jugador. Los humanos ven avisos de picaduras reales; no ven las futuras marcas privadas de los mosquitos.

## Volar y picar

W vuela hacia donde apuntás, incluso arriba o abajo. Soltalo para frenar. Acercate por el lado visible de **tu** marca y mantené E para concentrarte. Soltar antes de terminar cancela la carga. Ya adherido, soltar E te mantiene picando: soltá y pulsá E de nuevo para desprenderte, retrocedé con S y buscá tu nueva marca.

La sangre empieza a extraerse después de la preparación de la picadura. La marca rota con un calendario privado; mientras estás adherido queda fija. Si todas las zonas válidas están ocupadas, esperás turno sin recibir una marca imposible. No se muestra el temporizador de rotación.

## Aturdimiento y ayuda entre mosquitos

En Recolección de sangre y Tareas, una palmada te hace caer aturdido al piso durante **35 segundos**. Conservás la sangre conseguida, seguís en la ronda y podés mirar alrededor, pero no volar ni picar hasta recuperarte. Los golpes sobre un mosquito ya aturdido no reinician el tiempo.

Otro mosquito puede acercarse, apuntarte y **mantener E** para ayudarte. La recuperación avanza a cuatro veces la velocidad normal mientras siga cerca y sin obstáculos. Alejarse, soltar E o perder la línea de visión interrumpe la ayuda; varios compañeros no multiplican la velocidad. Al levantarte volvés a controlar al mosquito en ese lugar y recibís una marca válida cuando haya una disponible.

Si todos los mosquitos caen, la ronda continúa: los humanos aprovechan ese tiempo para defender la sangre o hacer tareas. En **Supervivencia**, los golpes siguen eliminando al mosquito por el resto de la ronda.

## Crear una sala dentro del juego

1. Elegí **CREAR SALA**, escribí tu nombre y pulsá **CREAR SALA**. El juego conecta con Epic y abre la sala 3D; tu misma aplicación ejecuta el servidor.
2. Copiá la invitación **LMS1-** de la sala y pasásela a tus amigos. Conservá todo el texto; no hace falta indicar una IP ni un puerto.
3. Tus amigos eligen **UNIRME CON INVITACIÓN**, escriben su nombre y pegan el texto completo.
4. El anfitrión elige modo y cantidad de humanos. Todos se preparan y el anfitrión inicia.

No hace falta abrir una consola, otro ejecutable ni Iniciar-servidor.cmd. Salir de la sala o cerrar el juego termina la sala propia. Si se cierra el anfitrión, la sala termina; no se transfiere a otra PC. El juego usa una identidad local de Epic: no te pide crear una cuenta ni instalar otra aplicación. La primera conexión necesita Internet.

Epic permite intentar una conexión directa y usar su relay cuando haga falta. El código identifica la sala y autoriza el ingreso; no contiene la contraseña del cliente Epic. Compartilo sólo con quienes quieras invitar. Copiarlo no demuestra que la conexión se haya completado: esperá a que ambos aparezcan en la sala 3D.

**Conexión directa / LAN**, dentro de Opciones, conserva el método avanzado anterior. Sus invitaciones **DD5-** sí contienen dirección y puerto y requieren una ruta de red alcanzable. Los scripts de servidor del paquete pertenecen a este modo avanzado; no son el procedimiento habitual de online.

## Si no conecta

El estado muestra el paso de conexión y permite cancelar o reintentar. Revisá que todos usen esta misma versión, que el anfitrión siga dentro de su sala y que la invitación esté completa. Una sala cerrada necesita una invitación nueva. Si vuelve a fallar, conservá el mensaje exacto y el archivo de registro para informar el problema.

El mensaje «No llegó respuesta UDP» corresponde al modo directo avanzado. En ese modo, 127.0.0.1 sólo apunta a la propia PC y una dirección local de otra casa no sirve como dirección de Internet. Para el online integrado usá la invitación **LMS1-** y dejá desmarcada Conexión directa / LAN.

## Voz cercana

Durante una ronda online, mantené **V** para hablar con jugadores cercanos de ambos equipos. El mosquito suena más agudo sin acelerar las palabras: los humanos lo oyen más bajo y a menor distancia; entre mosquitos se escucha mejor. Las puertas y el recorrido entre habitaciones afectan la voz.

Elegí la entrada de micrófono en ajustes. Podés silenciarte o silenciar a otro jugador y cambiar la tecla para hablar. La prueba local del micrófono se activa sólo al mantener su botón. En la sala de espera, la práctica o después de quedar eliminado, la interfaz indica que la transmisión no está disponible.

## Recorrer superficies

Con el mosquito, **F** permite posarte o despegar. Al posarte podés caminar por pisos, paredes y techos con WASD; la cámara acompaña el cambio de superficie. **Espacio** también permite despegar cuando estás apoyado.

## Tu personaje

La base humana es compacta y la del mosquito alargada. Ojos, boca y cejas se eligen por separado y conservan sus expresiones animadas; el humano también permite bigote, barba y color de pelo compartido. Paredes y suelo usan el acabado liso elegido, con los colores de cada habitación y sus juntas; los objetos mantienen sus materiales propios.

Abrí **TU PINTA** en el menú principal. Elegí humano o mosquito; girá el modelo arrastrando, acercá/alejá con la rueda y usá restablecer vista. Cada personaje guarda su propio color, detalle, cara, pelo o antenas, ropa o patrón, calzado y accesorio. Usá las tarjetas visuales y muestras de color; al elegir una categoría, la vista se acerca a esa parte. Los perfiles nuevos empiezan con pijama, pantuflas y gorro de noche; una apariencia guardada anteriormente se conserva. Los cambios aparecen en práctica y en la sala de los demás; son sólo visuales.

## Modos

- **Recolección de sangre:** los mosquitos comparten una meta de sangre que deben completar antes del final. Los humanos frenan la recolección aturdiéndolos durante 35 segundos; pueden ser ayudados por sus compañeros.
- **Supervivencia:** los mosquitos deben sobrevivir hasta terminar el tiempo, con una vida y sin hambre. Los humanos intentan eliminarlos.
- **Tareas:** los humanos comparten una meta de trabajos, con asignaciones y plazos personales. Fallar reduce plazos siguientes hasta el mínimo; no elimina la reserva necesaria para viajar. Los mosquitos pueden interrumpir tareas; si reciben un golpe, quedan aturdidos y pueden ser ayudados. No hay un límite de vidas en este modo.

Se permiten de uno a cinco humanos, hasta doce mosquitos y dieciséis participantes totales; también uno contra uno. Cada ronda genera una casa con 16–24 habitaciones y dos o tres pisos. Todos los participantes juegan la misma distribución. Mirá los puestos cercanos para encontrar tareas y herramientas; las etiquetas lejanas se ocultan para dejar ver el escenario.

## Archivos y límites de esta entrega

**PRUEBAS.md** registra qué se comprobó y qué falta. **BUILD.txt**, el manifiesto y los archivos SHA256 identifican el ejecutable y los paquetes. No se ha certificado balance con jugadores humanos, requisitos mínimos ni rendimiento en todas las PCs. Las fuentes y la documentación técnica se conservan por separado del ejecutable.

## Tu pinta

En el menú principal podés combinar ojos, boca y cejas por separado para cada personaje. El humano también tiene bigotes y barbas opcionales; el color de pelo se comparte con cejas, bigote y barba. Ropa, piel, pantuflas y accesorios conservan sus colores propios. Arrastrá la vista para girar y usá la rueda para acercar. Los cambios se guardan en esta PC y se muestran en sala y partida; son sólo apariencia. Los perfiles anteriores conservan el estilo de cara al migrar a las tres piezas, con bigote y barba desactivados.
