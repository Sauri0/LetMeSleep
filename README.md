# Let me sleep

Juego cómico online para Windows: de noche, en una casa, un campamento, un
puerto, una isla o un yate, los **humanos** quieren dormir y los **mosquitos**
no los dejan. Los roles se sortean en cada ronda. Low-poly, colorido y para
jugar con amigos.

**Versión actual: 0.3.0 (prueba)** · [Descargar el ZIP](https://github.com/Sauri0/LetMeSleep/releases/download/v0.3.0/Let-me-sleep-0.3.0-Windows.zip) · [Release v0.3.0](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.3.0) · [Guía de prueba](docs/player/PRUEBA-V0.3.0.md) · [Notas de la versión](docs/v030/RELEASE-NOTES-0.3.0.md)

## Instalar

1. Descargá `Let-me-sleep-0.3.0-Windows.zip` de la
   [release v0.3.0](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.3.0).
2. (Opcional) Compará `Get-FileHash .\Let-me-sleep-0.3.0-Windows.zip -Algorithm SHA256`
   con el archivo `.sha256.txt` de la misma release.
3. Clic derecho → **Extraer todo…** en una carpeta tuya y abrí
   `Let-me-sleep.exe` desde la carpeta extraída.
4. El ejecutable no está firmado: si aparece **"Windows protegió tu PC"**, tocá
   **Más información → Ejecutar de todas formas**.

No hay launcher: se retiró y cada versión se instala desde su ZIP. No hace falta
Unity ni una cuenta de GitHub.

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

La 0.3.0 acerca el juego a los bocetos de arte: interfaz nueva, personajes
rediseñados, personalización, mapas con nueva ambientación y decoración,
animaciones nuevas y correcciones online. Se compila con Unity 6000.3.24f1 para
Windows x64 como build de **release** (sin Development Build). El ZIP trae
`BUILD.json` con el commit de origen y el SHA-256 de cada archivo.

Lo que todavía **no** está verificado, y por eso es una versión de prueba:

- el online entre redes distintas con dos personas reales;
- la voz con varias personas reales;
- 60 FPS en todas las PCs.

## Enlaces

- [Guía de prueba 0.3.0](docs/player/PRUEBA-V0.3.0.md) y
  [notas de la versión](docs/v030/RELEASE-NOTES-0.3.0.md).
- [Guía de estilo derivada de los bocetos](docs/v030/GUIA-ESTILO-BOCETOS.md) y
  [mapa de sistemas](docs/v030/MAPA-SISTEMAS.md).
- Proyecto Unity: [unity](unity/).
- Versiones anteriores: [v0.2.0](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.2.0).
  La versión Godot 0.9.3 se conserva en [game](game/) y en su
  [release](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.3).
