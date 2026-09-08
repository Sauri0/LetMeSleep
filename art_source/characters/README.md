# Personajes seleccionados · integración 0.7

La producción usa **humano A compacto y mosquito B alargado**. Sus fuentes son los `.blend` elegidos de `art_source/samples07/characters/A/human` y `B/mosquito`, preservados sin escritura. Los archivos de producción mantienen el sufijo histórico `lms06` para conservar referencias y nombres del rig.

## Generación

Con Blender 4.5.3 LTS:

```text
blender --background --python art_source/export_presets/characters_selected_pipeline.py -- --species both
```

El generador abre las fuentes elegidas, conserva su topología, pesos, cosméticos, acciones y diez shape keys en cada una de las tres caras por especie. Guarda la fuente de producción y exporta explícitamente los GLB; no vuelve a remallar ni escribe las muestras congeladas. `manifest.json` registra selección, generador y SHA256 de la fuente original.

Se corrige exclusivamente la conversión sRGB→lineal en los materiales Blender humanos `skin` (e3ac83), `skin_shadow` (c88e6a) y `lip` (ab735a). Los colores elegidos en Tu pinta, el acento y el shader de ropa se siguen gestionando en Godot y no se convierten dos veces. Los GLB conservan materiales básicos; el patrón de tela procedural sigue dependiendo de `cloth.gdshader` y `CharacterSkin`.

## Pose y contacto

Las ocho zonas frontales mantienen sus IDs y su sentido, la localización longitudinal del antebrazo sigue en 0.83 del eje codo→muñeca, y el golpe manual conserva dirección, alcance, ventana y radio. La cámara sigue usando HumanPose y conserva giro libre al mirar abajo.

HumanPose ajusta la piel compartida al humano A: torso de radio 0.267 m, pelvis 0.22 m y una esfera inferior de 0.245 m encerrada en reposo que acompaña el pliegue del abdomen al agacharse. El antebrazo usa tres cápsulas que siguen su estrechamiento; la palma usa una cápsula corta, independiente del radio de impacto manual 0.095 m. `limb_key` permite reconocer los nuevos tramos como el mismo brazo. ActorView actualiza esas mismas formas, incluidas las cápsulas cuyo eje se reduce a cero, sin que la geometría antigua oculta vuelva a sobrescribirlas.

La postura compartida adelanta 1 cm el codo y 2 cm la mano en reposo y reduce su recorrido longitudinal de marcha. Así el extremo atrasado del antebrazo permanece visible dentro de los 75° de inspección mientras el extremo adelantado sigue dentro del radio corporal. La malla, las cápsulas y el mosquito adherido reciben la misma pose; no se mueve únicamente el marcador ni se altera el seguimiento de cámara.

MosquitoPose comparte entre visualización e impacto una orientación basada en estado público y tres volúmenes: tórax, cabeza y abdomen. La esfera de navegación de 0.04 m permanece intacta. Alas, patas, antenas y probóscide son apéndices visuales. ActorView interpola las orientaciones recibidas sin añadir inclinación basada en aceleración privada del render. Durante aturdimiento, el pequeño alzado visual de 16 mm evita el roce de los ojos con el suelo; ese estado no admite impactos.

El cuerpo humano visible en primera persona conserva sus brazos reales. Sólo cabeza, cara, pelo y gorro/accesorios de cabeza se ocultan en la vista propia. Las siete categorías de personalización y los perfiles guardados conservan sus índices.

## Verificación

- `selected07_mesh_checks.gd -- --production --verify --output=RUTA_ABSOLUTA`: comparación de las superficies contra triángulos deformados, tres atuendos y siete posturas; comprueba el aro a 9 mm y los ocho centros visibles. Requiere Godot nativo para la lectura de malla.
- `selected07_actor_checks.gd`: formas de raycast coincidentes con HumanPose, orientación del mosquito en seis superficies, escala del editor, seis caras y colores importados.
- `selected07_facial_envelope_checks.gd`: extremos reales de las mezclas faciales dentro de la anatomía de impacto; usa el bake específico de blend shapes, no el de esqueleto que las ignora.
- `v07_character_client_checks.gd`: cliente real, carga E, ocho zonas de pie/agachado, cámara y golpe manual confirmado.
- `v07_facial_state_checks.gd` y `v07_character_cache_checks.gd`: interpolación, contextos públicos y caché sin perder la animación.

Evidencia de integración en `outputs/0.7-integracion`. La elección de proporciones no equivale a declarar terminado el acabado de la casa ni a certificar rendimiento en otra GPU.
