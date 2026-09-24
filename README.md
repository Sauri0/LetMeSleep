# Let me sleep

Juego cómico online para Windows: de noche, en una casa, un campamento, un
puerto, una isla o un yate, los **humanos** quieren dormir y los **mosquitos**
no los dejan. Los roles se sortean en cada ronda. Low-poly, colorido y para
jugar con amigos.

**Versión actual: 0.3.0 (prueba)** · [Descargar el instalador](https://github.com/Sauri0/LetMeSleep/releases/download/v0.3.0/Let-me-sleep-0.3.0-Setup.exe) · [ZIP](https://github.com/Sauri0/LetMeSleep/releases/download/v0.3.0/Let-me-sleep-0.3.0-Windows.zip) · [Release v0.3.0](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.3.0) · [Guía de prueba](docs/player/PRUEBA-V0.3.0.md) · [Notas de la versión](docs/v030/RELEASE-NOTES-0.3.0.md)

## Instalar

1. Descargá **`Let-me-sleep-0.3.0-Setup.exe`** de la
   [release v0.3.0](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.3.0) y abrilo.
2. El instalador no está firmado: si aparece **"Windows protegió tu PC"**, tocá
   **Más información → Ejecutar de todas formas**.
3. Siguiente → Instalar. Se instala en tu usuario (no pide administrador), crea
   accesos directos en el Menú Inicio y, si querés, en el Escritorio, y queda en
   **Configuración → Aplicaciones** para desinstalarlo.

Funciona en cualquier PC con Windows 10 u 11 de 64 bits sin instalar nada más:
el paquete trae todo lo que el juego necesita. Para actualizar a una versión
nueva, basta con correr su instalador.

**Alternativa sin instalar:** el ZIP `Let-me-sleep-0.3.0-Windows.zip`. Extraelo
completo (clic derecho → **Extraer todo…**) en una carpeta nueva y abrí
`Let-me-sleep.exe` desde ahí; el `.exe` solo, sin el resto de la carpeta, no
funciona. Podés verificar la descarga con
`Get-FileHash .\Let-me-sleep-0.3.0-Windows.zip -Algorithm SHA256` y el
`.sha256.txt` de la release.

**Si el juego no abre** (se cierra enseguida sin mostrar nada): casi siempre es
una extracción incompleta, por ejemplo por haber extraído el ZIP encima de una
carpeta con el juego abierto. Cerrá el juego, borrá la carpeta extraída y volvé
a extraer el ZIP en una carpeta nueva y vacía (mejor fuera del Escritorio, por
ejemplo `C:\Juegos`). El paso 2 (SHA-256) confirma que la descarga llegó entera.

## Jugar con amigos

1. Todos usan la **0.3.0** (no se mezcla con la 0.2.0).
2. Una persona entra a **JUGAR → CREAR SALA** y comparte el código.
3. Los demás entran a **JUGAR → UNIRSE A SALA** y pegan el código.
4. Todos marcan **LISTO** y el anfitrión toca **INICIAR RONDA**.

El anfitrión corre la partida en su PC y tiene que seguir conectado; si se va,
la sala se cierra. No hay que abrir puertos ni configurar IP. Para practicar
solo está **ENTRENAMIENTO**, con bots, en los cinco mapas y los tres modos
(Sangre, Supervivencia y Tareas).

## Controles

- **Humano:** WASD y mouse, Espacio salta, Ctrl se agacha, Shift corre, E
  interactúa (mantener para tareas), clic izquierdo golpea o carga y tira la
  pantufla, 1/2/3/0 y rueda eligen inventario, G suelta.
- **Mosquito:** W vuela hacia donde mirás, Espacio sube, Ctrl baja, F se posa o
  despega, mantener E pegado a un humano pica (E otra vez para soltarse),
  mantener R ayuda a un compañero caído, rueda acerca o aleja la cámara.
- **Todos:** mantener V para hablar, Esc para pausa. La tabla completa está en
  la [guía de prueba](docs/player/PRUEBA-V0.3.0.md#controles).

## Estado

La 0.3.0 acerca el juego a los bocetos de arte:

- **Interfaz nueva** según los bocetos: menú, jugar online, sala, entrenamiento,
  personalización, HUD de humano y mosquito, pausa, resultados, ajustes y
  pantallas de conexión.
- **Personajes rediseñados**: humano en pijama con gorro de dormir rojo y ojos
  enormes; mosquito rojo con ojos gigantes, alas translúcidas y patas largas.
- **Personalización por piezas**: gorros, pelo, lentes, remera o buzo,
  pantalón, calzado y mochila para el humano; alas, probóscide, marcas y
  accesorios para el mosquito; colores y miniaturas.
- **Animaciones nuevas**: salto y aterrizaje cómicos, manotazo con matamoscas,
  brazos que se balancean, caminata agachada, aleteo según la velocidad,
  inclinación en vuelo, expresiones, bostezo y celebración.
- **Mapas y escenas**: nueva ambientación de día y de noche, decoración, menú en
  el dormitorio con el humano dormido y sala de espera cálida.
- **Chat de voz de proximidad**: se escucha según la distancia y la dirección,
  se amortigua detrás de paredes y suena con eco en interiores.
- **Online**: se corrigió que la conexión entre jugadores no se iniciaba, el fin
  de ronda cuando se va un equipo y la sala que quedaba trabada.

Es para Windows x64 (Unity 6000.3.24f1); el ZIP trae `BUILD.json` con el commit
de origen y el SHA-256 de cada archivo.

Lo que todavía **no** está verificado, y por eso es una versión de prueba:

- el online y la voz entre redes distintas con personas reales;
- 60 FPS en todas las PCs.

Pendiente para la 0.3.1: pulido de efectos de sonido, ambientes y música con
audio espacial completo; corrección de parpadeos de texturas y objetos mal
ubicados en los mapas; vegetación con viento y vida ambiental.

## Enlaces

- [Guía de prueba 0.3.0](docs/player/PRUEBA-V0.3.0.md) y
  [notas de la versión](docs/v030/RELEASE-NOTES-0.3.0.md).
- [Guía de estilo derivada de los bocetos](docs/v030/GUIA-ESTILO-BOCETOS.md) y
  [mapa de sistemas](docs/v030/MAPA-SISTEMAS.md).
- Proyecto Unity: [unity](unity/).
- Versiones anteriores: [v0.2.0](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.2.0).
  La versión Godot 0.9.3 se conserva en [game](game/) y en su
  [release](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.3).
