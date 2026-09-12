# Recuperación humana — primera muestra real

Responsable actual: Modelador Humanos. Worktree `N:/LetMeSleep/Worktrees/characters`.
Rige TEAM-RECOVERY-20260912 y CHARACTER-QUALITY-BAR; el aviso de reanudación reemplaza la pausa histórica.

## Referencia y diagnóstico directo

Se abrieron las ocho imágenes originales CharacterQuality-20260912 mediante `view_image` en esta tarea.
Ref-01/02/04/06/08 muestran frente facial definido, sien biselada, pómulo, nariz con perfil, mandíbula estrecha, cuello separado y manos relajadas; las prendas tienen borde, puños y volumen sin parecer cilindros rígidos.
Ref-03/05/07 definen mosquito; su dueño recibe esa responsabilidad y la comparación pendiente.
Las palabras, accesorios y roles dibujados no se incorporan como contenido.

Comparación directa con `characters-silhouette7/Human_Idle_{0,90,35}.png` y `Human_Clap_35.png`: cabeza ovoide ancha, cuello oculto, ojos sobresalientes, collar como cordón, palmas redondas/dedos largos y pantuflas con altura de botas. Se abrieron también `Mosquito_Idle_{35,90}.png`: tórax horizontal, cabeza esférica y patas cortas; continúa rechazado.

## Fuentes actuales

- Heredado: `author_human_geometry.py`, geometría por planos, collar/bolsillo con espesor, pantuflas bajas; ajustes de manos, prendas, Jaw y expresiones en build/motion/auditores.
- Revisado: la boca tiene una abertura real entre los anillos de piel y cavidad interior. Los bordes de labios y cavidad comparten pesos con la piel, en vez de superponer una placa negra sobre cara cerrada.
- `build_characters.py` requiere `--species Human`, `Mosquito` o `Flyswatter`; sólo regenera las especies nombradas. Conserva los audits existentes de las demás al formar el manifiesto.
- `render_human_witness.py` abre la fuente guardada y evalúa el clip/slot real. Cycles CPU, 2 hilos, luz fija, 576×720, recibo por imagen con SHA, cámara y fase. No cambia la fuente guardada.
- `author_mosquito_geometry.py` es la extracción inicial del mosquito previo, transferida al dueño Mosquitos. `create_mosquito` devuelve Character ligado y contacto, sin animar/exportar. La igualdad AST de todos los statements previos a animación está registrada en `MOSQUITO-EXTRACTION-20260912.json`.

## Contratos y pendientes

Jaw es hijo de Head, bind `(0,-.015,1.46)` a `(0,-.035,1.393)`. No se reparentan huesos anteriores. Se conservan Root, sockets, escala, cápsula, nombres de renderers y 15 clips. HumanHead contiene la cara y mandíbula para el ocultamiento en primera persona. Presentation conoce la jerarquía; rutas Animator y reproducción quedan por validar tras integración.

Preparación verificada: sintaxis de scripts y `git diff --check`. Ninguno demuestra calidad ni deformación. Se guarda un registro de hashes de las seis fuentes/audits de especies no seleccionadas antes de generar.

## Primer turno y evidencia real

Director concedió CPU exclusivo para Human y seis vistas. Generación PID 2008 terminada con `LMS_CHARACTER_GENERATION_PASSED`; render PID 33888 con `LMS_HUMAN_WITNESS_DONE` y `Blender quit`. Ambos procesos finalizados; turno devuelto. No se obtuvo código numérico de salida del proceso externo: se conservan marcadores, archivos y verificaciones independientes, sin inventar exit=0.

Blender 5.2.1 LTS; Cycles CPU 2 hilos, 16 muestras, 576×720. Carpeta `art_source/unity/characters/review/human-reference8`: frente, tres cuartos, cara frente/perfil, mano abierta y pantufla perfil, todos abiertos con `view_image`. Cada PNG tiene recibo con identidad de fuente y clip/slot evaluado. `HUMAN-REFERENCE8-EVIDENCE.json` resume hashes, procesos y alcance.

Audit de geometría: 10.476 triángulos, 65 huesos, pesos/triángulos válidos. Root/cadena axial y sockets conservados; únicos binds anteriores cambiados son las 30 falanges para acortar dedos, Jaw es el único hueso nuevo. Mismos 15 clips. SHA de los seis archivos de mosquito/matamoscas intactos.

Resultado visual propio: ahora se distinguen nariz/mentón/cuello y construcción de collar/bolsillo; se acortaron dedos y bajó el calzado. **No aprobado:** persisten bandas horizontales de facetas en cara/perfil, ojos como discos y pantufla demasiado rectangular. El detalle de pantufla corta la punta y necesita encuadre más ancho. Muestra enviada a revisión visual independiente.

Pendientes: corregir esos defectos, completar perfiles/espalda/detalles y movimiento. `audit_motion.py` ahora registra malla/arista/fase, extremos y pesos del máximo estiramiento para localizar señales anteriores; todavía no se ejecutó sobre esta fuente. Los reports motion/roundtrip existentes son anteriores y NO verifican este candidato. Después corresponde integración Unity por Director, rutas Animator, agarre real y clips completos. Las imágenes fijas no certifican movimiento ni aceptación artística.
