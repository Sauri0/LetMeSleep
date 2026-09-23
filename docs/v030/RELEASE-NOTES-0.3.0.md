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
- **Personalización por piezas**: pestañas HUMANO y MOSQUITO con el personaje
  en grande para girarlo arrastrando. Humano: tono de piel, gorros, pelo,
  lentes, remera o buzo, pantalón, calzado y mochila; mosquito: color, alas,
  probóscide, marcas y accesorios. Miniaturas, vista previa de frente, espalda y
  lado, y botones ALEATORIO y DESHACER. Los demás jugadores ven tu apariencia.
- **Animaciones nuevas**: salto y aterrizaje cómicos, manotazo con el
  matamoscas, brazos que se balancean al caminar, caminata agachada de sigilo,
  aleteo según la velocidad, inclinación y bamboleo en vuelo, mosquito noqueado
  panza arriba, expresiones (sueño, enojo, sorpresa, alegría), bostezo y
  celebración en los resultados.
- **Mapas con nueva ambientación** en los cinco escenarios (Casa del patio,
  Campamento del pinar, Puerto del faro, Isla del laguito y Yate a la deriva):
  luz cálida, faroles con halo, fuego y ventanas encendidas de noche, faro
  encendido y decoración nueva (flores, rocas, faroles, cajas, muelles,
  alfombras…). El menú ahora es el dormitorio del humano dormido con el mosquito
  encima, y la sala de espera es un cuarto cálido y decorado.
- **Chat de voz de proximidad**: mantené V para hablar; la voz se escucha según
  la distancia y la dirección, se amortigua detrás de paredes y suena con eco en
  interiores.
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
- Queda para la 0.3.1: pulido de efectos de sonido, ambientes y música con
  audio espacial completo; corrección de parpadeos de texturas y de objetos mal
  ubicados en algunos mapas; vegetación con viento y vida ambiental.

## Contanos cómo te fue

Abrí un issue o avisanos con versión (0.3.0), mapa, modo, rol, qué hiciste y qué
pasó; si es visual, sumá una captura. Para el online, contá si estaban en la
misma red o en redes distintas. No mandes contraseñas ni direcciones IP.

Compilado con Unity 6000.3.24f1 para Windows x64. `BUILD.json`, dentro del ZIP,
lista el commit de origen y el SHA-256 de cada archivo.
