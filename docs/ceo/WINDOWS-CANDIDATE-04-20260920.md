# Candidata Windows 04 — verificación acotada

Fuente `4947f728ccc87e51538e7557265204d6c29f168e`, Unity 6000.3.24f1.
Artefacto privado: `N:/LetMeSleep/Artifacts/0.2.0-20260920-172000`.
Build04 terminó con exit 0; recibo Succeeded, errors 0, sourceDirty false,
310570306 bytes reportados por Unity. Incluye GUIA-DE-PRUEBA.md.
Log y verificación de procedencia en Validation/V020/WindowsCandidateBuild04.

## Prueba ejecutada sobre esta candidata

WindowsCandidateSmoke04/casa-blood: PASS, exit 0. Inicia entrenamiento Sangre
en Casa con humano y mosquito, comprueba apoyo inicial/ausencia de deriva del
humano, vuelve al menú y crea/sale de una sala EOS con una identidad. Cero
errores runtime registrados. Recibo SHA256:
`151418F0A0DE5FAF7B6437FAAEC28533E584299C78D79B28CE88C28F903B0BC1`.

CEO inspeccionó menu.png, human.png y mosquito.png: 1920×1080, versión 0.2.0
visible, interfaz legible. Durante la espera los bots de entrenamiento
incapacitaron al humano y aturdieron al mosquito; los PNG muestran esos estados.
Es evidencia del ejecutable real. Los personajes visibles siguen siendo los
antiguos; no constituyen aceptación de los personajes nuevos requeridos.

El muestreo observado fue mediana 18,03 ms y p95 24,58 ms sobre 1138 frames,
RTX 3060 Ti. No es un benchmark de recorrido ni acredita 1080p60. El log
advierte reducción ×4 para encajar 54 mapas de sombras en atlas 2048²,
agotamiento de Graphics Ring Buffer y ausencia de prefabs slipper/racket/spray.
Auditoría de luces Casa asignada a red, sólo lectura y sin modificar calidad.

La matriz 15/15 anterior pertenece a la candidata a654d8b; no se atribuye a
esta build. Se eligió una prueba de arranque dirigida al cambio de
personalización y empaquetado, sin repetir mapas/modos que no cambiaron.
No certifica personalización modular con arte final, rondas completas,
micrófonos, dos identidades, WAN, descarga pública ni release final.

## Puerto: defecto de producto confirmado

SurfaceAcquisition06/PUERTO-READONLY.md demuestra desprendimiento real:
ApproachingSurface adquiere GroundFloor/1000132, desciende 21,6664 mm y recibe
Terrain/1000455 en la consulta siguiente. Ambos soportes pasan adquisición;
la continuidad estricta de ID llama Detach y deja Flying. Las superficies
coplanares difieren aproximadamente 8 nm en profundidad del ray.

Se conserva como defecto de autoría pendiente. PuertoCoplanar01 medirá una
pulsación F sin exigir un ID concreto y una grilla local, antes de corregir
contenido. No se relajaron tolerancias ni se modificaron los resultados Frozen.

La aclaración J25/O08 permanece vigente: ningún bot online, reserva de 30 s
sin IA. Ya está registrada en ACLARACIONES.md y PLAN-ACTUALIZADO.md.
