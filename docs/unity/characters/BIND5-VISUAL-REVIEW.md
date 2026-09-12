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
