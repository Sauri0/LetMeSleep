# C01 — entrega de movimiento y cámara

**Registro del primer tramo `f1796c2`.** El salto de raíz descrito aquí fue
corregido posteriormente; el comportamiento y las pruebas actuales están
en `motion091-attachment-results.md`. Se conserva esta medición anterior
para comparar el cambio, no como descripción del estado final.

Worktree: `C:/Users/brank/Documents/Codex/2026-09-06/lms091-motion`.
Base: `59af8ad`. Sin cambios de simulación, reglas, zonas, red ni activos.

## Resultado

Se suavizan yaw/cabeza/marcha remotos y marcha local entre snapshots.
El yaw y pitch locales quedan exactos: el prototipo que filtraba el yaw local
añadía hasta 14.15 cm de desplazamiento orbital calculado a 6 rad/s y fue
descartado ANTES de las pruebas nativas. Client toma el origen del ojo desde
la misma pose que representa ActorView; el ratón y los comandos no cambian.

Los colliders conservan yaw, ejes y dimensiones de autoridad mediante
compensación de la raíz render. Fuera de adhesión su origen sigue teniendo
la interpolación posicional preexistente: **no son posiciones mundiales
idénticas a `data.p`**. Amenaza, golpe y lanzamiento conservan esa política
para no introducir tirones traslacionales al atacar.

Mientras un humano está `bitten` y el mosquito `biting`, ambos usan posición
mundial exacta y el insecto usa normal exacta. Esto elimina el retraso
independiente del insecto respecto de la extremidad que lo sostiene.

## Pruebas nativas

Godot 4.5.2, OpenGL Compatibility, RTX 3060 Ti. Todas las ejecuciones finales
salieron con código 0, stderr vacío y sin timeout. No es una medición de FPS
ni una prueba de GTX 1660 Ti/WAN.

| Prueba | Checks | Fallos | Evidencia |
|---|---:|---:|---|
| motion091_presentation_test | 4013 | 0 | motion091-presentation-r2.json |
| human09_presentation_test | 1457 | 0 | motion091-legacy-r1.json |
| motion091_camera_visual | 11 | 0 | motion091-visual-r1.json |
| camera_turn_checks | 6 | 0 | motion091-camera-r1.stdout.log |
| manual_defense_test | 6897 | 0 | motion091-manual-r1.stdout.log |

Total: 12384 checks. El primer intento de la prueba nueva encontró un tipo
inferido ambiguo en una variable del test; se declaró `float` y se repitió
con resultado limpio. No fue un error de runtime.

20 snapshots/s -> 60 frames/s: paso máximo de yaw remoto .04368 rad frente
a .10000 rad sin filtro; fase de marcha .19654 frente a .45000 rad. El cruce
de ±PI no invierte el giro. También pasan 20->120, 30->60 y 30->120.

La cámara real local tuvo error de dirección < 0.000001, error respecto al
ojo mostrado 0 m y desplazamiento extra frente al baseline < 0.000003 m
(redondeo). Recorrió 6.398 m, con giro de 360° y pitch entre -1.35 y 1.0.
El test separado de controles verificó después palmada efectiva y stun.

## Límites medidos y pendientes

- La entrada a `bitten` añade una corrección de raíz de .10539 m en el caso
  reproducido: movimiento total del frame .14226 m frente a .03687 m del
  filtro anterior. **Es un tirón potencial, no una transición visual aprobada.**
- El esqueleto remoto tenía hasta .07252 m de diferencia previa en manos/pies
  al volver a pose exacta y .17408 rad de paso angular en el escenario de
  bypass. No se afirma suavidad completa de todas las transiciones.
- Amenaza/golpe/lanzamiento: desplazamiento extra de raíz frente al baseline
  0 m. Mantienen error mundial traslacional previo (hasta .15893 m medido
  en esas muestras). No se modificó la autoridad para ocultarlo.
- El filtro posicional basal de la cámara local llegó a .19350 m de atraso
  respecto a la posición publicada durante movimiento sostenido. Este tramo
  no añade atraso angular local ni resuelve ese atraso previo.
- No se agregó `attached_to`, ningún dato de asignación privada ni segundo
  pase de World. Requiere decisión del Director si se quiere quitar el tirón
  de adhesión manteniendo una transformación compartida coherente.

## Revisión de capturas locales

Se inspeccionaron las seis PNG `work/motion091-visual-r1-*.png` (1920x1080
efectivos por la configuración del viewport del juego). Semilla 1306743498,
300 frames de simulación a 60 Hz, publicación a 30 Hz. Las imágenes son de
este escenario generado, no del video personal de Branko.

La vista hacia abajo muestra antebrazos, torso y pies conectados y el cuerpo
orientado durante la marcha/giro. Al volver al frente no queda el cuerpo
invadiendo el centro de la vista. No se aprecia una cabeza local visible
accidentalmente. Una secuencia de seis imágenes no demuestra por sí sola
la suavidad de cada frame; se complementa con las métricas y el test real.

Se ven bordes angulosos en muñecas/puños y pliegues/rayas del pijama, y una
mano próxima a la estantería durante el giro. Son observaciones de la base
artística para Modelador 2; no se modificaron geometrías ni colisiones para
ocultarlas. El entorno corresponde al baseline, aún sin integrar los otros
worktrees. No se concede aprobación visual global.

## Recuperación de importación

Dos imports anteriores al reinicio se cerraron con fallo nativo durante las
fuentes. Sus archivos se preservan; r2 puede contener NUL tras el reinicio y
no debe tratarse como evidencia íntegra. No se tocaron alias de clases.

Se copiaron únicamente seis archivos .fontdata/.md5 desde la caché importada
de `lms091-ui`, tras verificar hash de las tres fuentes y settings .import
idénticos normalizando CRLF. No se copió caché global de clases, editor,
settings, assets fuente ni configuración EOS. El import posterior
`motion091-import-r3seeded` terminó en 19.2 s con código 0/stderr 0.
Procedencia en `motion091-cache-seed.json`.

Motor liberado tras las pruebas, sin procesos Godot/Blender pendientes.
