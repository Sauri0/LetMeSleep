# Casa de Let me sleep · arte 0.7

Los modelos de esta etapa son originales del proyecto. No se descargaron modelos, texturas ni materiales de terceros. Se conservan el generador, las escenas Blender editables y los GLB explícitos que carga Godot; el juego no necesita Blender instalado.

`build_house07.py` reutiliza las operaciones de modelado de `build_house.py`. Construye carpintería con perfiles, piezas biseladas, cerámica torneada, tela y accesorios de uso doméstico. Las 32 piezas nuevas y sus triángulos, límites y tamaños figuran en `house/v07/manifest.json`. Las escenas editables están en esa misma carpeta; los archivos de juego en `game/assets/art/house/`.

Para regenerar desde la raíz del repositorio:

```powershell
& './work/tools/blender-4.5.3/blender-4.5.3-windows-x64/blender.exe' --background --python './art_source/environments/build_house07.py'
```

Blender trabaja en metros con Z vertical y exporta glTF con Y vertical. La hoja de puerta mantiene el origen en la bisagra a nivel del suelo: ancho nominal X de 0 a 1, panel Y de 0.14 a 2.45 y espesor Z de ±0.035. El marco comparte ese origen y deja libre el umbral. `DoorView` aplica el ancho y la transformación de `DoorCatalog`; los herrajes sobresalen visualmente de la hoja sin cambiar su volumen de colisión.

Los muebles se adaptan a los AABB de `MapCatalog`. La ducha cerrada ocupa la misma huella de 1.60 × 0.65 m del armario anterior y una altura explícita de 2.15 m. Las ventanas son aberturas visuales con vidrio: la pared exterior sigue siendo sólida en la simulación. Las barandas usan una sola descripción compartida, `HouseBarriers`, tanto para sus mallas como para la física nativa, las consultas de Arena y las rutas.

`HouseDetails` coloca los elementos por función de las 16 habitaciones. Las superficies y los apoyos físicos conservan las posiciones de la simulación. Los paños visuales de suelo se separan para mantener local la selección de luces de Compatibility; sus volúmenes conjuntos conservan el suelo original. No se aumenta el límite de ocho focos por malla.

Hay cuatro focos con sombras como máximo y un único `ReflectionProbe` estático acotado al baño. El reflejo es una aproximación del ambiente; no es un espejo plano ni una captura continua del jugador. SSR y SSAO permanecen desactivados en Compatibility. El difusor de las lámparas usa emisión cálida moderada, sin depender de bloom.

Límites verificados contra la documentación oficial de Godot4.5: [ocho focos por malla en Compatibility](https://docs.godotengine.org/en/4.5/classes/class_spotlight3d.html), [capacidades de cada renderer](https://docs.godotengine.org/en/4.5/tutorials/rendering/renderers.html) y [actualización estática del ReflectionProbe](https://docs.godotengine.org/en/4.5/classes/class_reflectionprobe.html).

La prueba `game/tests/house07_checks.gd` comprueba muebles, ducha, ventanas, huecos y postes de las barandas, las dos escaleras y la correspondencia entre rayos nativos y Arena. `--screens` genera las 16 cámaras fijas y una vista adicional con humano y muebles; `--motion` añade el recorrido corto del baño. Las puertas se cierran sólo en estas comparaciones interiores para conservar el encuadre de la versión anterior, que no tenía hojas.
