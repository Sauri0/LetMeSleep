# Muebles generados: selección y proporciones

## Causa observada

La selección anterior dependía de `room_name` y `label` del mapa autoral. El generador producía etiquetas distintas: en Baño, cualquier etiqueta salvo `Lavado` y `Canasto` caía en `bath_shower`; en Dormitorio azul, la cama sólo se escogía cuando su etiqueta era `Mesada`. Además, el ajuste por cada eje podía comprimir un GLB alto o ensancharlo sin relación con su proporción original.

## API nueva

`FurnitureBlueprint.for_theme(theme_id: String) -> Array[Dictionary]` devuelve cuatro registros independientes. `THEME_IDS` contiene los 16 IDs estables; el generador debe almacenarlos junto a sus temas y no reconstruirlos a partir del texto visible.

Cada registro contiene:

- `asset_id`: nombre exacto de un GLB existente.
- `label`, `style`: presentación y clasificación funcional; no seleccionan la malla.
- `visual_scale`: escala uniforme, común a los tres ejes.
- `size`: huella XZ y altura del cuerpo físico, en orientación canónica.
- `visual_size`: dimensiones completas del GLB escalado, incluidos grifo/olla/objetos ya modelados sobre ciertos muebles.
- `surface_height`: altura desde el suelo local; la primera pieza siempre tiene `pickup_surface=true` y es una mesa o escritorio con centro libre.
- `pickup_point=Vector3.ZERO`: desplazamiento XZ desde el centro de la mesa; la altura se toma por separado.
- `front=Vector3.FORWARD`: frente de acceso local −Z. La cama original tiene cabecera en −X; no se rotó su geometría para cambiar ese diseño.

El generador debe copiar esos campos al registro de estructura y añadir `box`/`rotation_y`. Para 90°/270° intercambia los tamaños X/Z de `box`; para 0°/180° no los intercambia. HouseLibrary honra ese giro explícito y no vuelve a inferirlo a partir del aspecto de la caja.

Primera superficie: 0,72 m de alto, excepto la mesa de preparación de cocina a 0,86 m mediante escala uniforme 1,19444. La cocina usa esa mesa, pileta, cocina/horno y heladera; no se inventó un `counter.glb` inexistente. El baño usa mesa auxiliar, vanitory, inodoro y ducha. Los cuatro temas de dormitorio incluyen `bed.glb` real.

## Compatibilidad y límites

La nueva ruta de HouseLibrary se activa exclusivamente cuando existe `asset_id`. La casa autoral sin ese campo conserva su mapeo, escalado, orientación y decoración anteriores. Los nombres visibles de muebles generados pueden cambiar libremente sin reinterpretar modelos.

La ruta generada no agrega tazas, lámparas ni otros props a la primera mesa. Los objetos decorativos que ya forman parte de un GLB permanecen allí. Por eso mesas con máquina de coser, vajilla o tablero se usan como segunda pieza y no como superficie de pickup.

No se editan `procedural_house.gd`, física, navegación, GLB ni Blender en este subtask. Los grifos/ollas sobre mesadas siguen siendo detalles por encima de su cuerpo físico, igual que en la ruta autoral; `visual_size` lo registra expresamente. La validación de disposición, circulación y cuatro colocaciones efectivas por habitación corresponde a la integración de Root.

Las medidas fuente están en `work/furniture09-source-bounds.json`, obtenidas por `work/furniture09_measure.py` a partir de nodos/accesores de los GLB actuales, con SHA256 de cada archivo.

## Verificación preparada

`game/tests/furniture09_blueprint_checks.gd` valida los 16 temas, cuatro registros, los cuatro giros, escala uniforme, altura visual, apoyo al suelo y correspondencia XZ con los modelos importados. La primera mesa recibe nueve rayos contra los triángulos reales de su zona central. También comprueba que etiquetas cambiadas no alteren el asset y que ejemplos del fallback autoral conserven su resultado.

```text
Godot --path game --script res://tests/furniture09_blueprint_checks.gd -- --output=<carpeta absoluta>
```

Las capturas del catálogo son escenas preparadas comunes; no prueban todavía la distribución completa de un seed del generador.

Resultado ejecutado: **2.490/2.490 PASS**, Godot 4.5.2 Compatibility nativo, salida 0 y stderr vacío. Evidencia `outputs/0.9-furniture-blueprint/`: 16 PNG y `furniture09-blueprint.json`; logs `work/furniture09-blueprint.log` y `.err`. Revisión visual puntual de `kitchen.png`, `bathroom.png` y `bedroom_blue.png`: piezas funcionales diferenciadas y cama real, con proporciones originales preservadas por escala uniforme. La iluminación corresponde al estudio del fixture, no al interior definitivo generado.
