# Carga y cámaras de cinco mapas — 20/09/2026

La prueba nativa `HiggsfieldMapLoadingPlayModeTests` pasó: diez sesiones de
entrenamiento Sangre, cinco mapas por dos roles, iniciadas mediante la aplicación
real. Cada sesión comprobó identidad/hash del mapa, navegación vinculada, tres
actores finitos y eliminación del runtime al regresar al menú.

Evidencia: `N:/LetMeSleep/Validation/V020/FiveMapGameReview01/results.xml`,
`unity.log`, `input.json` y `captures/`. Unity 6000.3.24f1, Direct3D 11,
1280×720, datos de preferencias aislados. Fuentes J32 en c47983d; cambios
de física aún sin aceptar presentes en el árbol de trabajo. No es una build
publicable ni evidencia asociada a un commit limpio.

CEO inspeccionó las diez capturas de la cámara de juego. No aparecen materiales
magenta ni una cámara dentro del suelo en estos puntos iniciales. Isla y Yate
son diurnos; Casa y Campamento nocturnos; Puerto tiene iluminación de atardecer.
En Casa y Campamento, el mosquito pierde contraste frente a árboles y cielo
oscuro. Esta observación queda pendiente de revisión en movimiento y del arte
nuevo, todavía no integrado. Los humanos y mosquitos visibles son los recursos
actuales: estas imágenes no acreditan fidelidad a los bocetos finales.

| Mapa | Humano | Mosquito |
|---|---|---|
| Isla del Laguito v2 | Carga y cámara inicial comprobadas | Carga y cámara inicial comprobadas |
| Casa del Patio v1 | Entrada y luz del porche visibles | Silueta poco contrastada en la vista inicial |
| Campamento Pinar v2 | Caminos y suelo visibles | Silueta oscura frente a árboles |
| Yate a la Deriva v3 | Cubierta, escalera y mar visibles | Cubierta, escalera y mar visibles |
| Puerto del Faro v1 | Plaza y accesos visibles | Plaza, faro y horizonte visibles |

La captura usa `Camera.Render`, por lo que no incluye el Canvas superpuesto.
No prueba HUD, recorridos completos, transiciones de cámara sobre paredes o
techo, audio, FPS ni online. La ejecución avisó que aún faltan los prefabs de
pantufla, raqueta eléctrica y aerosol; no se ocultó esa carencia con sustitutos.

## Muestra: cámara humana en Yate

![Yate, cámara humana](N:/LetMeSleep/Validation/V020/FiveMapGameReview01/captures/hf-yate-a-la-deriva-v3-Human.png)

## Muestra: cámara mosquito en Casa

![Casa, cámara mosquito](N:/LetMeSleep/Validation/V020/FiveMapGameReview01/captures/hf-casa-del-patio-v1-Mosquito.png)
