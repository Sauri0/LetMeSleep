<!--
Texto para la release v0.3.0 de GitHub (gh release create ... --notes-file este archivo).
Los comentarios HTML no se ven en la release. Antes de publicar, el integrador hace la
auditoría de afirmaciones de docs/v030/RELEASE-0.3.0-PASOS.md (paso 4) sobre ESTE archivo,
docs/player/PRUEBA-V0.3.0.md (va dentro del ZIP) y README.md: cada novedad tiene que estar
en el sourceCommit del build. El punto "Build de release" sólo queda si el recibo dice
profile=release y releaseProblems=[], package-v030.ps1 terminó bien y walkthrough.json
(paso 9) tiene pass=true; si no, se borra.
-->
# Let me sleep 0.3.0 — versión de prueba

La 0.3.0 acerca el juego a los bocetos de arte: interfaz, personajes y
personalización rediseñados, mapas con nueva ambientación y correcciones del
online.

## Descargar e instalar

1. Descargá **Let-me-sleep-0.3.0-Windows.zip** (abajo, en *Assets*).
2. (Opcional) Verificá el SHA-256 con
   `Get-FileHash .\Let-me-sleep-0.3.0-Windows.zip -Algorithm SHA256` y compará
   el resultado con **Let-me-sleep-0.3.0-Windows.zip.sha256.txt**.
3. Clic derecho → **Extraer todo…** en una carpeta tuya y abrí
   `Let-me-sleep.exe` desde la carpeta extraída (no desde adentro del ZIP).
4. El ejecutable no está firmado: si Windows muestra **"Windows protegió tu
   PC"**, tocá **Más información → Ejecutar de todas formas**.

No hay launcher: se retiró en la 0.2.0 y cada versión se instala desde su ZIP.
Si ya tenés la 0.2.0, extraé la 0.3.0 en otra carpeta: los ajustes y la
personalización se guardan en tu usuario de Windows. Guía completa con
controles: `GUIA-DE-PRUEBA.md` dentro del ZIP.

## Novedades

- **Interfaz rediseñada** según los bocetos: menú principal, entrenamiento,
  jugar online con pestañas para crear sala o unirse, sala de espera,
  personalización, HUD, pausa, resultados y ajustes.
- **Personajes nuevos**, low-poly con ojos grandes: el humano en pijama (remera
  crema y pantalón azul con lunares), pantuflas y gorro de dormir rojo, y un
  mosquito rediseñado.
- **Personalización rediseñada**: pestañas HUMANO y MOSQUITO, el personaje en
  grande para girarlo arrastrando, vista previa de frente, espalda y lado, y
  botones ALEATORIO y DESHACER.
- **Mapas con nueva ambientación** en los cinco escenarios (Casa del patio,
  Campamento del pinar, Puerto del faro, Isla del laguito y Yate a la deriva):
  luz cálida, faroles con halo, fuego y ventanas encendidas de noche. El menú y
  la sala también tienen escena nueva.
- **Online más robusto**: la sala se resincroniza sola después de un corte del
  enlace entre dos jugadores, pueden entrar amigos mientras se muestran los
  resultados, la ronda termina bien si se va un equipo entero y los avisos de la
  sala dicen lo que pasó de verdad (por ejemplo, que la sala se llenó mientras
  te reconectabas).
- La 0.3.0 no se mezcla con la 0.2.0: cada versión tiene sus propias salas.
- **Build de release**: sin la marca "Development Build", sin el puerto de
  conexión del profiler de Unity y sin símbolos de depuración ni archivos
  internos de compilación en el ZIP.

## Limitaciones conocidas

- El online entre redes distintas **todavía no se verificó** con dos personas
  reales en dos PCs: es lo que más necesitamos que prueben.
- La voz con varias personas reales tampoco está certificada.
- Sin launcher ni actualización automática.
- Ejecutable sin firma digital (aviso de SmartScreen).
- No se garantizan 60 FPS en todas las PCs.
- Las animaciones son las de la 0.2.0 sobre los personajes nuevos, y los mapas
  conservan su decoración anterior.

## Contanos cómo te fue

Abrí un issue o avisanos con versión (0.3.0), mapa, modo, rol, qué hiciste y qué
pasó; si es visual, sumá una captura. Para el online, contá si estaban en la
misma red o en redes distintas. No mandes contraseñas ni direcciones IP.

Compilado con Unity 6000.3.24f1 para Windows x64. `BUILD.json`, dentro del ZIP,
lista el commit de origen y el SHA-256 de cada archivo.
