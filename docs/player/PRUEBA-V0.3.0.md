# Let me sleep 0.3.0 — guía de prueba

Juego cómico para Windows: humanos que quieren dormir contra mosquitos que no
los dejan. Esta es una versión de prueba para jugar con amigos y contarnos qué
anda y qué no.

## Instalar

1. Entrá a la release **v0.3.0** en
   <https://github.com/Sauri0/LetMeSleep/releases/tag/v0.3.0> y descargá
   `Let-me-sleep-0.3.0-Windows.zip`.
2. (Opcional) Verificá que el ZIP llegó entero. En PowerShell, desde la carpeta
   de descargas:

   ```powershell
   Get-FileHash .\Let-me-sleep-0.3.0-Windows.zip -Algorithm SHA256
   ```

   El resultado tiene que coincidir con el texto de
   `Let-me-sleep-0.3.0-Windows.zip.sha256.txt`, publicado en la misma release.
3. Clic derecho sobre el ZIP → **Extraer todo…** y elegí una carpeta tuya, por
   ejemplo `Documentos\Juegos`. No abras el juego desde adentro del ZIP.
4. Abrí `Let-me-sleep-0.3.0-Windows\Let-me-sleep.exe`. Dejá todos los archivos
   y carpetas del paquete juntos, al lado del ejecutable.
   Si no abre (se cierra enseguida), borrá la carpeta y volvé a extraer el ZIP
   en una carpeta nueva, con el juego cerrado.
5. El ejecutable no está firmado, así que Windows puede mostrar **"Windows
   protegió tu PC"** (SmartScreen). Tocá **Más información → Ejecutar de todas
   formas**. Si el Firewall de Windows pregunta, permití el acceso en redes
   privadas para poder jugar online.

No hace falta instalar nada más ni crear cuentas. El launcher de versiones
anteriores ya no se usa: cada versión se instala desde su ZIP. Para actualizar,
extraé el ZIP nuevo en otra carpeta; tus ajustes y tu personalización se
guardan aparte, en tu usuario de Windows.

## Probar solo

En el menú entrá a **ENTRENAMIENTO**, elegí mapa y modo y tocá **INICIAR** en
la tarjeta de humano o de mosquito. Los bots existen sólo acá. Los mapas son
Casa del patio, Campamento del pinar, Puerto del faro, Isla del laguito y Yate a
la deriva; los modos, Sangre, Supervivencia y Tareas. En **AJUSTES** están la
resolución, la pantalla completa, la sensibilidad, el audio y el micrófono.

## Jugar con amigos

1. Todos tienen que usar la **0.3.0**: una sala 0.3.0 no acepta jugadores de la
   0.2.0 y al revés.
2. Una persona entra a **JUGAR**, pestaña **CREAR SALA**, escribe su nombre y
   crea la sala. En la sala aparece el código: compartilo.
3. Los demás entran a **JUGAR**, pestaña **UNIRSE A SALA**, escriben su nombre,
   pegan el código (**PEGAR**) y se unen.
4. El anfitrión elige mapa, modo y reglas. Cada uno marca **LISTO** y el
   anfitrión toca **INICIAR RONDA**. Los roles se sortean en cada ronda.
5. Al terminar, vuelvan a la sala para la siguiente.

El anfitrión corre la partida en su propia PC y tiene que seguir conectado: si
se va, la sala se cierra. Si alguien se cae durante una ronda, su lugar queda
reservado 30 segundos; ningún bot juega por él. Para hablar, mantené **V** (se
cambia en AJUSTES). La voz depende de la distancia y de las paredes.

## Controles

| Acción | Humano | Mosquito |
|---|---|---|
| Moverse | W, A, S, D | W, A, S, D (W va hacia donde mirás) |
| Mirar | Mouse | Mouse |
| Saltar / subir | Espacio: saltar | Espacio: subir |
| Agacharse / bajar | Ctrl izquierdo | Ctrl izquierdo: bajar |
| Correr | Shift izquierdo | — |
| Interactuar | E (mantener para trabajar en una tarea) | Mantener E pegado a un humano para picar; E otra vez para soltarte |
| Posarse / despegar | — | F, apuntando a una superficie |
| Ayudar a otro mosquito | — | Mantener R cerca del compañero caído |
| Golpear / usar | Clic izquierdo | — |
| Lanzar la pantufla | Mantener clic izquierdo para cargar, soltar para tirar | — |
| Inventario | 1, 2, 3; 0 para manos; rueda para recorrer | — |
| Soltar lo que tenés | G | — |
| Cámara más cerca / lejos | — | Rueda |
| Hablar | Mantener V | Mantener V |
| Pausa / volver | Esc | Esc |

Si el inventario está lleno, el juego te propone qué reemplazar: tocá E otra vez
para confirmarlo.

## Qué hay de nuevo en 0.3.0

- **Interfaz nueva** según los bocetos: menú, entrenamiento, jugar online con
  pestañas para crear sala o unirse, sala, personalización, HUD, pausa,
  resultados y ajustes.
- **Personajes rediseñados**, low-poly y con ojos grandes: el humano en pijama
  (remera crema, pantalón azul con lunares), pantuflas y gorro de dormir rojo, y
  un mosquito nuevo.
- **Personalización rediseñada**: pestañas HUMANO y MOSQUITO, el personaje en
  grande para girarlo arrastrando, vista previa de frente, espalda y lado, y
  botones ALEATORIO y DESHACER.
- **Mapas con nueva ambientación**: luz cálida, faroles con halo, fuego y
  ventanas encendidas de noche en los cinco mapas; el menú y la sala también
  cambiaron.
- **Correcciones online**: la sala se resincroniza sola si se corta el enlace
  entre dos jugadores, pueden entrar amigos mientras se ven los resultados, la
  ronda termina bien aunque se vaya un equipo entero y los avisos de la sala
  dicen lo que pasó de verdad (por ejemplo, que la sala se llenó mientras te
  reconectabas).

## Limitaciones conocidas

- El online entre casas y redes distintas **todavía no se verificó** con dos
  personas reales en dos PCs. Justamente eso es lo que más necesitamos probar.
- La voz con varias personas reales tampoco está certificada.
- No hay launcher ni actualización automática: se instala desde el ZIP.
- El ejecutable no está firmado (de ahí el aviso de SmartScreen).
- No se garantizan 60 FPS en todas las PCs.
- Todavía hay parpadeos de texturas y algunos objetos mal ubicados en ciertos
  mapas, y los efectos de sonido y la música no tienen su pulido final: eso
  llega en la 0.3.1.
- Es una versión de prueba: puede haber detalles de arte, colisiones o cámara
  sin terminar.

## Contarnos un problema

Decinos la versión (0.3.0), mapa, modo, rol, qué hiciste y qué pasó. Si es de
cámara, animación, colisión o interfaz, sumá una captura o un video corto. Para
el online, contá si estaban en la misma casa o en redes distintas. No hace falta
mandar contraseñas, direcciones IP ni datos de cuentas.
