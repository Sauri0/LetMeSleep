# Fix de normales de parpadeo humano

Defecto medido por Director en Unity6000.3.24f1, FBX `9e85748b...`: cerrar los ojos cambia normales de127 vértices fuera de la región ocular;95 giran más de1°. Máximo mentón116.565°, cachete51.252°, con posición sin cambio. El efecto aparece en los cuatro niveles. Las30 muestras CPU BakeMesh no muestran desplazamiento de mentón/cachete ni fuera del soporte de morph.

Ejemplo vértice1, altura fuente1.356m: normal base `(0,0,1)`, delta de cada ojo `(0,1,-1)`. Con los dos ojos activos termina en dirección `(0,2,-1)` antes de normalizar. Los datos FBX sólo contienen156 índices de posición y normal por forma, todos de párpados; el cambio ajeno aparece en las normales calculadas al importar.

El postprocessor nuevo se limita a las rutas `LMS_Human_alpha.fbx` y `LMS_HumanMenu.fbx` dentro de Content/Characters/Models y a su renderer HumanHead. Para cada frame Blink calcula soporte topológico: vértices móviles más los vértices de sus triángulos incidentes. Esto conserva las normales de las anclas inmóviles del párpado; no propaga soporte al resto de la cabeza. Fuera de ese soporte pone a cero únicamente los deltas de normal y tangente. No cambia vertices, deltas de posición, normales base, materiales, skin, bind, orden/nombres/frameWeights de formas, pesos del renderer ni bytes FBX.

Se eligió filtrado topológico frente a normales estáticas globales o una caja ocular amplia: mantiene la iluminación animada válida del párpado y excluye también componentes inmóviles cercanos al ojo. No cambia BlinkWeightPolicy, VisualAttentionRig ni CharacterContentBuilder.

Archivos nuevos en el assembly Editor de personajes: HumanEyelidNormalsPostprocessor.cs y EyelidMorphSupport.cs, ambos con .meta. No instalación en runtime/player. Orden de postprocess1000, versión1. Falla explícitamente si una de las dos fuentes deja de traer HumanHead o las ocho formas.

Validación offline: compilación con DLLs Unity6000.3.24f1,0errores/0advertencias. Seis checks del helper puro: anclas adyacentes conservadas, cara desconectada excluida, sin propagación transitiva, input inmutable, deltas válidos conservados exactamente, índices inválidos rechazados.

Pendiente bajo slot Director: integrar, reimportar ambas fuentes usando Unity API y repetir scripts externos HumanFaceImportDifferential.cs y HumanFaceNormalDifferential.cs. Deben conservarse formas/muestras y deltas de posición; normales de mentón/cachete deben permanecer estables. El source SHA seguirá igual porque no se reexporta FBX. Registrar también base+menú, no sólo el prefab base.

Revisión gráfica pendiente: misma luz/cámara, Idle0 con cabeza quieta y girada, cierres0/.25/1. Confirmar estabilidad de mentón/cachete y que el párpado mantiene superficie/iluminación natural. El defecto objetivo de normales no excluye que aún exista clipping o un párpado demasiado abultado.
