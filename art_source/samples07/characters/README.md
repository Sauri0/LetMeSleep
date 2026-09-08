# Muestras reales de personajes A/B · 0.7

Dos propuestas de **proporción y silueta de una misma familia**, preparadas para comparar y elegir. Son cuatro modelos 3D originales editables; no imágenes conceptuales. Los GLB y `.blend` de producción no se sustituyeron.

**Decisión recibida al cerrar la muestra:** se eligieron el humano A compacto y el mosquito B alargado como dirección de proporciones. Esto no aprueba todavía la iluminación, todas las uniones ni su integración física. Las láminas y el clip conservan el rótulo de prueba previa a esa decisión.

| Modelo | Tamaño de autoría a escala del juego, X/Y/Z (m) | Vértices visibles por defecto | Triángulos visibles | Superficies / materiales distintos | Huesos |
|---|---|---:|---:|---:|---:|
| A humano | 0.811 / 1.817 / 0.562 | 24 804 | 49 404 | 20 / 12 | 32 |
| B humano | 0.811 / 1.940 / 0.540 | 24 457 | 48 710 | 20 / 12 | 32 |
| A mosquito | 0.174 / 0.114 / 0.175 | 7 277 | 14 236 | 15 / 7 | 21 |
| B mosquito | 0.219 / 0.121 / 0.204 | 7 277 | 14 236 | 15 / 7 | 21 |

Estas medidas salen de los vértices de las piezas seleccionadas en la pose de autoría, con escala humano 1 y mosquito 0.35. No describen cápsulas de colisión ni el tamaño del encuadre. El ancho máximo humano es el de los brazos; no refleja por sí solo el cambio del torso. `model.json` contiene las medidas de cada pieza, sus materiales, sus controles y los puntos de cada hueso. El conteo comprende sólo la combinación por defecto, no todos los cosméticos simultáneos.

## Diferencias geométricas exactas

- **A, curva compacta:** máscara central del torso con anchura ×1.13 y profundidad ×1.10; cara/cabeza con anchura ×1.08 y altura ×0.94 sobre el cuello; nariz ligeramente más corta. El volumen del gorro sobre 1.735 m reduce su altura un 25% y acorta el extremo lateral. Abdomen de mosquito más ancho (×1.15), más alto (×1.08) y corto (×0.84); alas más cortas (×0.87) y anchas (×1.20).
- **B, alargada desgarbada:** torso central de anchura ×0.89 y profundidad ×0.94; cara de anchura ×0.90 y altura ×1.08; proyección de la nariz ×1.17. El tramo superior del gorro aumenta su altura un 30% y alarga el extremo lateral. Abdomen de mosquito más fino (×0.80/0.87) y largo (×1.28); alas más largas (×1.13) y estrechas (×0.75).
- Las máscaras son suaves y no cambian los nombres, puntos ni relaciones del esqueleto compartido. No son dos estilos artísticos ajenos entre sí, ni simples recoloreados.
- Correcciones comunes de la muestra: camisa/pantalón solapados en la cintura; ribete de cuello plano; puño de manga menos ancho; gorro con volumen superior continuo y cola fina para eliminar su gran intersección anterior; mayor densidad de superficie en cara. Siguen visibles la banda de pelo y algunas transiciones de piezas, documentadas como refinamiento posterior.

## Piezas editables y faciales

Cada humano contiene 23 mallas de variantes, con 8 seleccionadas por defecto. Cada mosquito contiene 17, con 7 seleccionadas. El despiece de Godot desplaza esas mallas reales: cabeza, cara, pelo/antenas, gorro/accesorio, cuerpo, atuendo, pantuflas/patas y alas. El traje humano es una superficie continua deformable, por eso no se representa falsamente como piezas independientes de brazo y pierna.

Siete categorías compatibles con Tu pinta: color, cara, pelo/antenas, atuendo, accesorio, acento y calzado/patas. Se conservan tres caras por especie, tres estilos de pelo/antena, tres atuendos y tres calzados; humano añade gorro de noche como accesorio 3. El material de la muestra comparativa es siempre primario teal (índice 1), acento mostaza (4), atuendo 0 y cara humana 1/mosquito 0. El defecto anterior de perfiles no se reinterpreta ni se escriben preferencias desde estas pruebas.

Las **seis caras** tienen los diez controles reales: `BlinkL`, `BlinkR`, `GazeX`, `GazeY`, `BrowUp`, `BrowDown`, `MouthOpen`, `MouthSmile`, `MouthPress`, `CheekLift`. Están en Blender como shape keys y en GLB como morph targets. El driver común interpola reposo, alerta, esfuerzo e impacto a partir de estado ya público; el parpadeo se desfasa entre individuos. Las imágenes técnicas inhiben parpadeos y movimiento facial; el clip separado los habilita. El panel muestra el rango funcional, no una actuación final ni una partida.

Implementado: geometría A/B, variantes intercambiables, esqueleto deformable, diez canales en cada cara, driver y LOD facial, fuentes y exportación explícita. **Pendiente tras elección:** pulido final de expresión/uniones, retarget de la silueta elegida a las ocho superficies de defensa y validación de contacto/cámara en movimiento, coste final con 16 actores y aprobación visual. La muestra no modifica HumanPose ni las reglas físicas para hacer coincidir una silueta todavía sin elegir.

## Vistas y luz

`front`, `side`, `back`: proyección ortográfica. `three-quarter`: perspectiva de 36°. Cámara, pose, color y luz idénticos entre A y B por especie. `exploded`: mallas reales desplazadas, proyección ortográfica. En la lámina dividida el mosquito se amplía **×4 respecto a su escala del juego**; en `escala` el mosquito junto al humano usa 0.35 real y el panel derecho mantiene la ampliación rotulada. Los faciales usan acercamiento de cámara y conservan su rótulo de ampliación.

**Estudio técnico Compatibility:** Spot cálido con sombra, energía 1.85, rango 8 m y ángulo 55°, ambient 0.28 y relleno direccional 0.32; atlas de sombra 2048, bias 0.20, normal bias 2.0. El bias se corrigió después de una prueba comparativa que aisló las bandas diagonales como auto-sombra. No se han retocado las imágenes. Las vistas `world` y `world-detail` las captura el fixture de la casa con sus luces intactas: sirven para estudiar la integración, incluyendo la sobreexposición/sombra dura todavía observada. La energía numérica del estudio no equivale a la iluminación de la casa por posición, atenuación y número de luces.

## Fuentes y reproducción

- Modelos: `art_source/samples07/characters/{A,B}/{human,mosquito}/*.blend`.
- Exportaciones y contratos: `game/assets/art/samples07/characters/{A,B}/{human,mosquito}/*.glb`, `rig_contract.json`, `model.json`.
- Generador staged: `generate_samples.py`; depende de `art_source/export_presets/characters_pipeline.py` y `art_source/characters/shared/facial_geometry.py`. Usa Blender 4.5.3 LTS; `--variant A|B|both --species human|mosquito|both`. Sólo escribe las carpetas staged.
- Driver de muestra: `game/assets/art/samples07/sample_preview.gd`. Depende de `character_skin.gd`, `facial_expression.gd`, `cloth.gdshader`, `human_pose.gd` y `cosmetics.gd`. El shader procedural de tela es de Godot, **no está embebido dentro del GLB**. No hay imágenes/texturas externas compradas.
- Vistas nativas: Godot 4.5.2 con renderer Compatibility, `--path game --script res://tests/samples07_character_views.gd -- --variant=A --output=RUTA_ABSOLUTA`; repetir con B.
- Panel/clip facial: `--script res://tests/v07_facial_preview.gd -- --sample=A --output=RUTA_ABSOLUTA`. Añadir `--animate-blinks` para la secuencia; `--write-movie RUTA_ABSOLUTA/facial.png --fixed-fps 30` antes de `--` captura fotogramas nativos.
- Validación read-only de los cuatro GLB: `python art_source/samples07/characters/validate_samples.py`. Comprueba huesos, las seis caras por par de especies, todos los canales y datos finitos exportados.

Los archivos conservan el sufijo histórico `lms06` porque comparten nombres de rig; las rutas A/B evitan cualquier reemplazo del arte de producción. Abrir el `.blend` permite inspeccionar y modificar las piezas, materiales, pesos y shape keys; no hace falta reproducir un render para que existan esas piezas.
