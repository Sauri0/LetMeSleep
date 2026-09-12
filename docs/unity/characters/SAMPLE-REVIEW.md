# Primera pareja fuente — 0.9.4 alfa

Fecha: 12 de septiembre de 2026. Blender 5.2.1 LTS, build `9e2066aef7ef`, Windows. Fuente original reproducible en `art_source/unity/characters/build_characters.py`. Referencias de humano, mosquito y hoja de personajes inspeccionadas; hashes en manifest. No se copiaron armas, clases, interfaces ni accesorios de combate de las referencias.

## Resultado medido

| Dato | Humano | Mosquito |
|---|---:|---:|
| Triángulos LOD0 | 11.224 | 1.736 |
| Huesos, incluidos sockets | 64 | 33 |
| Renderers | 5 | 3 |
| Acciones fuente | 10 | 5 |
| Vértices sin peso / pesos incorrectos | 0 / 0 | 0 / 0 |
| Triángulos degenerados / coordenadas no finitas | 0 / 0 | 0 / 0 |
| Escala visual Unity acordada | 1 | 0,5 |

Altura humano con gorro: 1,928 m, cuerpo 1,72 m y cámara 1,53 m. Mosquito fuente: largo total 0,422 m y alas 0,481 m; Unity aproximadamente 0,211 m y 0,240 m. Colisión mosquito radio 0,055 m independiente de alas. Bounds exactos y materiales por renderer en las auditorías JSON.

Reimportación FBX independiente: pasan conservación de dimensiones (tolerancia 0,1 mm), triángulos, cantidad de huesos, sockets y presencia de todas las acciones. Esto acredita ida y vuelta Blender/FBX; no acredita Avatar ni materiales URP en Unity.

## Revisión visual realizada

Once PNG reales de Cycles CPU, dos hilos, 720×900, 24 muestras, bajo turno autorizado por Director. Frente, perfil, espalda y tres cuartos de ambos personajes; mano abierta/cerrada y contacto de palmada. Los archivos están en `art_source/unity/characters/review/` y se sellan con hashes SHA-256.

Correcciones durante esta revisión:

- Suelas: reemplazadas las superficies elipsoidales solapadas por una pantufla de secciones continuas con banda de suela integrada.
- Hombros: transición de manga reducida dentro del torso para eliminar el extremo plano expuesto al bajar los brazos.
- Abdomen: corregida la discontinuidad de orientación de secciones que torcía el extremo al cambiar la dirección de la curva.
- Manos: palma y dedos soldados; pesos suavizados con máximo cuatro influencias por vértice. La flexión positiva X local mueve el índice 7,9 cm hacia la palma en ambos lados. La macro muestra cierre hacia dentro; quedan facetas visibles propias del modelo y el agarre con herramienta debe probarse después.
- Palmada: alcance de dos huesos calculado para ambas manos. Centros de palmas a 0,050 m, espesor aproximado combinado 0,048 m. Captura de frame 12 verifica orientación enfrentada; el contacto del juego debe coincidir con su evento autoritativo.

Cabeza y gorro separados del cuerpo para ocultación en primera persona. Membranas y venas separadas para materiales de Presentation. No hay pelo en esta base. No se produjeron variantes.

## Entrega a integración

`integration.json` cruza los nombres de anchors de Presentation (contrato W2 `190fad6`) con huesos fuente únicos. Los anchors son datos de rig; el prefab `PresentationAnchors` se construye en Unity por el integrador. FBX y blend contienen geometría, rig, materiales y acciones, sin cámaras ni luces de revisión guardadas.

Archivos fuente: `human/LMS_Human_alpha.blend`, `human/LMS_Human_alpha.fbx`, `mosquito/LMS_Mosquito_alpha.blend`, `mosquito/LMS_Mosquito_alpha.fbx`. Reproducción: Blender background con `--threads 2 --python-exit-code 1 --python build_characters.py`, después `verify_fbx.py`; renderizar `render_review.py` sólo bajo turno del Director; ejecutar `python seal_manifest.py` al finalizar. Todos los scripts resuelven sus salidas junto a su propio archivo.

## Pendientes explícitos

- Importación real Unity: orientación, escala, materiales URP, Avatar Humanoid y máscara facial. Generic es la opción inicial verificable de fuente; no afirmar que el Avatar Humanoid ya está validado.
- Presentación local: cuerpo visible, interior de cabeza oculto, cámara libre, agarre de herramienta, pies y transiciones de palmada/caída.
- Clips fuente iniciales requieren integración y pulido de contacto en motor. Faltan Human Land/Hit/Faint/Recover y los estados ampliados de mosquito detallados en `integration.json`; no se sustituyeron por nombres equivalentes sin avisar.
- No hay LOD1/LOD2 ni herramienta en este lote de primera pareja. No se midió rendimiento, red o calidad visual en Unity.

El Director revisó human_front y mosquito_threequarter y aceptó la muestra para integrar alfa; indicó que esto no es aprobación artística de Branko. Señaló ojos humanos muy salientes y alas muy blancas/opacas. El perfil confirma el volumen de los ojos; queda como observación para ajuste tras revisión en motor. W2 debe comprobar transparencia sobre fondo claro/oscuro: la membrana tiene espesor con dos superficies, por lo que activar también Render Face Both puede acumular opacidad. Usar culling de caras posteriores en el material URP y verificar el resultado, sin asumir equivalencia visual con Cycles.

La muestra fuente está lista para integración. No declara completado todo el contenido de personajes de alfa ni las pruebas del juego.
