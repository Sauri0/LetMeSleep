# Herramientas: ejes, agarre y contacto

Lectura del GLB real con sus transformaciones de nodo, antes de modificar modelos. Datos completos y script de reproducción: `work/tool07-asset-audit.json` y `work/audit_tool_assets.py`. Este diagnóstico no reemplaza la revisión posterior de FPS/TPS en Godot.

| Objeto | Dimensiones actuales ancho × largo × fondo (m) | Centro de agarre canónico +Y (m) | Grip → centro activo (m) | Radio melee (m) | Gesto / contacto / recarga (s) | Arrojable |
|---|---|---:|---:|---:|---|---|
| Manos | Palma del humano A, sin objeto duplicado | Centro de palma, 0.05 tras muñeca sobre su eje | 0 | .095 | .36 / .08–.25 / .80 | No |
| Matamoscas | .247 × .602 × .029 | .05855 | .40145 | .115 | .32 / .065–.22 / .60 | No |
| Raqueta | .308 × .742 × .056 | .025 | .485 | .14 | .44 / .11–.32 / 1.05 | No |
| Diario | .094 × .319 × .094 | .06 | .24 | .06 | .28 / .05–.19 / .43 | Sí |
| Escoba | .340 × 1.020 × .098 | .0125 | .8675 | .17 | .52 / .14–.38 / 1.20 | No |
| Pantufla de mano | .128 × .290 × .077, malla exportada | .035 | .18 | .075 | .34 / .07–.23 / .60 | Sí |

`reach` pasa a significar hombro → centro de la cara activa: brazo máximo 0.93 m más la longitud grip→cara una sola vez. El alcance real sobre la mira se obtiene intersectando el rayo del ojo con esa envolvente y resolviendo el agarre, no reutilizando los antiguos techos 1.70/1.65 m medidos desde el ojo. El radio es la extensión de contacto de la cara; no se suma otra vez al mango.

## Fallas observadas en la construcción anterior

1. Las fuentes de matamoscas/diario exportan el eje largo +Y y las de raqueta/escoba −Y. ActorView compensaba sólo las dos primeras mediante PI. Se reemplaza por transformación explícita de cada asset hacia la convención común +Y, sin dar por hecho que todas las fuentes están invertidas.
2. El socket y `strike_contact` usan el punto de muñeca, mientras que la palma real se prolonga 5 cm desde allí. El agarre de la malla tampoco se descuenta. Esto puede colocar el mango fuera del puño y separar la palma visible del centro de impacto. La pose compartida debe exponer el grip real y resolver muñeca/brazo alrededor de él.
3. Todas las herramientas apuntan hacia abajo en reposo. La escoba actual alcanza y≈−.115 m de pie y −.515 m agachado; matamoscas y raqueta también atraviesan el piso al agacharse. La postura de reposo debe preparar cada objeto con su parte activa elevada y adelantada, conservando el cuerpo y las ocho superficies visibles.
4. Los pickups usan elevaciones constantes y un mismo giro oblicuo, sin apoyo calculado. `visual.bounds` y su orientación canónica permitirán a World/Simulation obtener la altura de apoyo por objeto y superficie.
5. El diario termina en aros planos: una cápsula con los extremos contraídos un radio puede omitir sus vértices terminales. El diario nuevo redondea sus extremos y se midió contra todos sus vértices: desviación máxima 0.0027 mm; la pantufla completa queda dentro de su cápsula. Ambas pasan el margen explícito de 1 mm en work/tool07-throw-coverage.json.

## Contratos de implementación

- `ToolCatalog`: datos originales en metros/segundos; +Y grip→cara, +Z normal de cara. La transformación de fuente precede a restar `visual.grip`. `visual.bounds`, `contact` y las cápsulas de lanzamiento se expresan respecto del grip final.
- La mano libre conserva su centro a 5 cm de la muñeca. Al agarrar, `tool_grip` es el centro de prensión del mango: la palma se apoya contra su sección medida y la muñeca se resuelve detrás, con `hand_direction_r`/`hand_width_r` compartidos. Falanges y pulgar envuelven la sección; la cápsula de mano sigue ese mismo eje. El origen y cara de contacto no cambian por el cierre de dedos.
- Diario: carga .85 s, salida 4–10 m/s. Pantufla: 1.15 s, 4–12 m/s. Gesto de salida .12 s y recuperación .45 s; gravedad 9.8 m/s², vida 8 s, rebote .10/.12. Autoridad de Simulation; mano real de salida, nunca el ojo.
- La pantufla de mano es un pickup independiente. No se retira ni modifica el cosmético de los pies.
- Evidencia actual en `outputs/0.7-herramientas`: reposo, inspección, preparación, contacto, retorno, carga, salida y recuperación desde FPS/TPS y dos primeros planos. FPS usa el ojo real y su dirección; la inspección mira abajo sin cámara desplazada. `selected07_mesh_checks --production --verify --tools`: 972 comprobaciones nativas de superficies y ocho zonas con seis herramientas, tres prendas, pie/carrera/agachado. `tool07_visual_checks`: 364 comprobaciones headless de grip, centro activo, apertura, orientación y salida. La autoridad de ataque/lanzamiento se verifica aparte en las suites de Simulation.


## Reparación localizada del rig

La fuente de producción conserva la malla elegida del humano A. Los nudillos se sitúan a 52 mm tras la muñeca; se repararon pesos locales y se añadió un pulgar de dos segmentos por mano. El driver de falanges conserva la rotación de la palma al resolver cada segmento, evitando retorcer sus uniones. La mano se abre durante el último tramo de salida y recupera su postura libre; ninguna zona privada ni radio de ataque depende de dedos/cosméticos. Las muestras A/B congeladas no se reescriben.
