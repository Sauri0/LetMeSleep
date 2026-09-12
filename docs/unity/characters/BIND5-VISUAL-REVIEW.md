# Inspección de capturas bind5

M1 abrió mediante `view_image` siete PNG reales de `N:/LetMeSleep/Validation/ArtCatalog/characters-bind5`: Human_Idle_35, Human_Clap_35, Human_Crouch_35, Mosquito_Idle_35, Mosquito_Idle_90, Mosquito_Fly_35 y Mosquito_BiteLoop_35. También volvió a abrir `work/references094/expanded/mosquito-turnarounds.png`. No se afirma haber revisado los otros diez PNG ni videos completos.

- Humano: Idle, Clap y Crouch son poses distintas y legibles. En la palmada los dedos permanecen unidos a las manos y los brazos no explotan. El agachado mantiene pies a nivel y flexión clara; los hombros muestran pliegues angulosos que aún requieren primeros planos. Pijama, pantuflas y gorro son reconocibles. Estas capturas no acreditan fluidez temporal ni agarre de herramienta.
- Mosquito: Idle90 permite reconocer abdomen alargado y afilado, pero el conjunto permanece bajo y horizontal, con patas cortas y probóscide más corta/oscura que la hoja. La referencia apoya el cuerpo más alto y destaca patas largas. Es una diferencia geométrica pendiente, no una variante cosmética ni algo resuelto por el gate numérico.
- Alas: Idle35/90, Fly35 y BiteLoop35 muestran membranas blancas planas, muy brillantes, sin translucidez legible. W2 verificó que Presentation no modifica su material ni aplica PropertyBlock a esos renderers. El material integrado tiene Surface=Transparent, alpha=0,42, ZWrite=0 y CullBack; no es un fallo de SurfaceOpaque.

## Corrección del material para comparación

El builder anterior combinaba `_Blend=Premultiply` y `_ALPHAPREMULTIPLY_ON` con smoothness 0,6. En el URP instalado (`com.unity.render-pipelines.universal@a8b4b2fc3560`), BaseShaderGUI líneas 1098–1114 reserva esa keyword para conservar especular y la desactiva con Premultiply. BRDF.hlsl líneas 65–71 atenúa sólo el difuso en esa ruta, manteniendo el brillo especular completo. La luz de la captura tiene intensidad 2,2. La combinación es una causa candidata de la apariencia quemada; la causalidad visual requiere comparación nativa.

`alpha-characters-6-wing-transmission` configura la membrana con Alpha directo, SrcAlpha/OneMinusSrcAlpha, sin conservar especular, sin reflejos especulares/ambientales y smoothness 0,15. Conserva alpha 0,42, dos caras físicas, CullBack, ZWrite=0 y ausencia de sombras. Contrato de integración actualizado; runtime y geometría intactos. Compilación PASS; pendiente builder/idempotencia y capturas A/B con misma luz, pose y ángulo, sin sobrescribir bind5. No se marca la corrección visual como aprobada antes de verlas.

## Siguiente corrección de proporciones autorizada

Director autorizó continuar dentro de alfa con abdomen alargado, patas finas articuladas que eleven la silueta y probóscide de color más coherente/legible. Se deben conservar Root, radio de colisión 0,055 m y Socket.Mouth en reposo Unity `(0,0,+0.095)`. Cualquier incompatibilidad geométrica con esos anclajes debe coordinarse con Director/W1 antes de alterar el contrato. Preparación de fuente y auditor permitida; regeneración pendiente de turno, porque Director mantiene el editor PID 33148.

La verificación posterior debe incluir apoyos y separación de superficie en piso, pared y techo, además de comparación visual frontal/perfil/tres cuartos contra la hoja. Un PASS estructural no acredita parecido con la referencia.

## Resultado wing6 y borrador de proporciones

Director confirmó builder6 PASS a las 19:05:27 UTC y 17 PNG nuevos a las 19:06:25 UTC. M1 abrió Mosquito_Idle_35 y Mosquito_Idle_90 de `N:/LetMeSleep/Validation/ArtCatalog/characters-wing6`. La membrana deja de verse blanca saturada y aparece gris azulada, pero sigue leyéndose como lámina plana. El fondo uniforme no permite acreditar por sí solo cuánto se transmite a través del ala: se propuso una banda/cuadrícula detrás, con pose, luz y alpha constantes. No hay aprobación artística.

Borrador fuente preparado sin Blender mientras Director mantiene el turno:

- Tórax/cabeza elevados 55 mm en Blender (27,5 mm en Unity), con secciones más delgadas; Root permanece fijo. Abdomen prolongado hasta Y=0,235 m de fuente y afinado hacia la punta.
- Patas más largas con articulación intermedia visible y margen analítico mínimo de alcance 4,972 mm para el paso de ±13 mm. Un primer borrador falló con -4,8 mm; se corrigió abriendo esa articulación a X=±0,085 m antes de generar.
- Probóscide roja, descendente desde la cabeza elevada a la punta contractual inalterada de Unity `(0,0,+0.095)`.
- Alas delgadas con una cresta central y caras triangulares explícitas para leer facetas; nervadura azul gris tenue. Misma transparencia alpha 0,42 y dos caras físicas.
- W1 confirmó Root a 0,057 m del soporte (piso observado 0,056 m). El antiguo GroundContact a -0,0512 m dejaba 4,8–5,8 mm de hueco. El nuevo plano distal/GroundContact será -0,057 m de Unity; radio 0,055 m, Root y boca se conservan. W1 posee la corrección de orientación visual hacia la normal, que faltaba en paredes/techo.

`audit_surface_support.py` preparará 636 filas: fuente/FBX, 53 fases, tres orientaciones de soporte y dos separaciones Root–superficie. Exigirá seis patas apoyadas en poses estáticas/final de aterrizaje, al menos tres en SurfaceWalk y penetración máxima 1,5 mm. Rotar geometría según el contrato no prueba la aplicación real por runtime: ese estado permanece explícitamente no verificado hasta la evidencia de W1/Director.

## Regeneración y evidencia de proporciones

Director cerró Unity PID 33148 y cedió el turno. Generación Blender PID 23808 terminó exit 0. Auditor completo de movimiento PID 1808: PASS en 60 evaluaciones, diferencia máxima fuente/FBX de 0,000887 mm. Auditor final de apoyos PID 33816: PASS en 636 filas; peor penetración 0,994 mm con Root a 0,056 m, seis apoyos estáticos y al menos tres durante SurfaceWalk. Roundtrip PID 5384: PASS para los tres assets. Cada proceso fue secuencial, con dos hilos y límite de 55 segundos; no hubo render ni editor de M1. CPU liberado al terminar.

El mosquito generado tiene 2344 triángulos y los mismos 33 huesos. El margen de alcance medido durante SurfaceWalk es 5,203 mm de fuente. Root permanece `(0,0,0)`; las mediciones de fuente/FBX mantienen Mouth en `(0,0,+0.095)` y GroundContact en `(0,-0.057,0)` de Unity, con error inferior a 0,001 mm. Los `.blend`/FBX ahora corresponden al generador de proporciones revision 4.

`motion_audit.json`, `surface_support_audit.json` y `fbx_roundtrip.json` vinculan resultados a hashes vigentes; el sellado requiere los tres gates. La receta de 94 capturas se regeneró con los nuevos hashes y conserva todos los estados como no generados/no revisados. Faltan reimportación e idempotencia en Unity, orientación real de W1 y nuevas capturas de silueta/facetas/translucidez; no se extrapola aprobación visual desde las capturas wing6 anteriores.
