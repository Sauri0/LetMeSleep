# Componente Opus aislado para Let me sleep

Esta copia conserva el código y la reconstrucción del componente Opus v2,
incorporado en `game/addons/lms_opus`. La integración de captura, transporte,
DSP, interfaz y animación está en `game/scripts/voice_*.gd` y sigue en pruebas.
Un resultado correcto del componente aislado no certifica
voz en partida, conexiones entre casas, calidad del filtro o rendimiento del juego.

## Reconstrucción en Windows x64

Se necesita Git y Python 3.14, además del Godot 4.5.2 oficial para ejecutar las
pruebas. Herramientas CMake/Ninja/LLVM y Opus se descargan de publicaciones oficiales
con hashes SHA-256 fijados en `bootstrap_downloads.py`. El checkout godot-cpp se
verifica contra `e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77` (API 4.5 estable).

```powershell
./build.ps1 -Python 'C:/ruta/python.exe' -PrepareDependencies -Godot 'C:/ruta/Godot_v4.5.2-stable_win64_console.exe'
```

Omitir `-PrepareDependencies` para reconstruir con las dependencias ya preparadas.
Omitir `-Godot` para compilar solamente. No se instala nada global ni se modifica
PATH. El build utiliza dos tareas de CPU. La prueba se ejecuta sin gráficos y con
audio Dummy, entrada de audio desactivada y señales sintéticas. No usa micrófono,
reproducción real, credenciales ni red. No ejecutarla junto a mediciones del juego.
La reproducción del procedimiento está definida; no se afirma identidad binaria
entre rutas/equipos sin haber comparado una segunda compilación limpia.

La DLL se genera en `extension/smoke/bin/lms_opus.windows.x86_64.dll`.
El archivo `.gdextension` apunta a `res://bin/`; el integrador debe adaptar esa ruta
al copiarlo al juego. Consultar `extension/README.md` para API, persistencia por
hablante, validación y límites. Registrar el hash de la DLL que se integra.

## Evidencia y límites

La reconstrucción escribe configuración, compilación, carga y pruebas en `logs/`.
Un log de compilación no sustituye a una ejecución correcta del codec.
Sólo aceptar el smoke con proceso terminado en 0 y marcador de cero fallos.
`integration-manifest.json` identifica los archivos incorporados de v2.
`bootstrap-v1-manifest.json` conserva la procedencia histórica de la versión
anterior; sus hashes no identifican la DLL actual.

## Licencias

`../../game/addons/lms_opus/licenses/` conserva sin alteraciones avisos de Opus,
godot-cpp, LLVM y runtime MinGW. Conservarlos al distribuir la DLL.
Los compiladores y otros ejecutables de
`_tools/` son herramientas de desarrollo y no forman parte del paquete del juego.
Sus archivos originales de licencia permanecen en las distribuciones descargadas.
No se asigna una nueva licencia al código propio: se mantiene bajo la política del
proyecto, sin conceder permisos adicionales por este documento.

## Trabajo de integración pendiente

Captura mediante pulsar para hablar, remuestreo, secuencias, jitter acotado, pérdida
de paquetes y desconexiones; filtro agudo sin acelerar la voz del mosquito;
atenuación diferente mosquito→humano y mosquito→mosquito; puertas/zonas/distancia;
movimiento de boca derivado del PCM reproducido y sincronizado con ese audio.
La extensión ofrece compresión/decodificación mono 48 kHz en bloques de 20 ms y
PLC explícito. Ninguna de las funciones de integración se da por implementada aquí.
