# Mapas Higgsfield — estado de producción

Branko aprobó veinte bocetos y cinco mapas. Encargado coordina hasta cierre verificado; humanos y alfa preservados. Arte nuevo generado por Higgsfield Scene Builder en Blender visible. Fuentes: N:/LetMeSleep/Artifacts/Higgsfield/Mapas.

## Estado 2026-09-13, después de terminar Puerto

- Isla hf-isla-del-laguito-v2: arte, agua, navegación y rutas nativas terminadas. Motor humano corregido para casts tangentes: geometría original48/48 y263portales PASS, pico.9653mm. No proxies persistidos. Evidencia Validation/Higgsfield/IslaV2-20260913/original-motor-full-01.
- Casa hf-casa-del-patio-v1:44/44 y17pasajes PASS tras regresión motor;16patrullas GameplayRuntime reales. Emisión restaurada y navegación instalada. Spawn_Human_03.001 tiene delta UnityY.2841115, fuente anterior conservada; sincronización de entrega pendiente. ContentHash019cca020c67d1e1d4d758556f76e3b2c553f715f21a0207f618ce3be01eede1.
- Campamento hf-campamento-pinar-v2: arte completo,1743caras/12objetos corregidos técnicamente, fuente antes de reparación preservada. NativeReview v2 muestra terreno/agua correctos y5/5suelo/cápsula libre. Rutas completas41/49 en primer lote,25portales y16Runtime PASS; puntos de aparición, trayectos frente a muebles y penetraciones de sendero/puente en investigación. No habilitar todavía.
- Yate: arte completo,213objetos/160meshes/73884tri. v1import parcial falló guard CPU20k del océano. Fuente intacta. Receta v2 explícita GPU Water_Ocean; vidrio transparente conservaalpha. Integración nativa de agua GPU y materiales pendiente.
- Puerto: arte completo,811objetos/730meshes/175746tri. Tres casas amuebladas, taller, faro con escalera, dos muelles y rutas. Ambas previews revisadas. FBX limpio5606716bytes/0warnings. Receta hf-puerto-del-faro-v1 explícita GPU Water_Ocean_Pueblo, amplitud.09m según propiedad fuente. Import/navegación/física pendientes.

## Créditos

Saldo observado1233.59 Ultra tras cerrar las cinco construcciones. Inicial2765.62; diferencia neta1532.03 no equivale a factura individual y puede incluir reservas/reconciliaciones. Veinte imágenes cotizadas60créditos. No nuevas generaciones pendientes. IDs completos en artifacts/recibos.

## Integración

Unity temporalmente Gameplay para Camp. Blender Puerto terminado e idle. Root integra agua GPU opt-in, importadoralpha y fuentes. Presentación prepara iluminación de los cinco; Mapas recetas estrictas. Catálogo y copia de escena bootstrap todavía no instalados.

Motor: nueva consulta humana conserva endpoints físicos, reduce radio de query y compensa distancia por normal; Isla y Casa originales PASS. Caso de escalón24cm también falla expectativa en motor anterior, documentado como baseline; no fingir10/10.

Agua: CPU mantiene límite20k. GPU conserva malla, crea materiales privados Unlit de paleta plana con shader referenciado, vínculo serializado explícito. Prueba C# offline56checks+10Python PASS; no certificar shader/movimiento sin ejecución nativa. Espumas CPU separadas. Batching disponible pero no activado.

## Cierre pendiente

Resolver Camp, importar y verificar Yate/Puerto (incluido shader/agua). Navegación schema1 obligatoria; import-recipe.json no es navegación. Instalar catálogo de cinco y copia bootstrap, comprobar selección/carga/retorno e iluminación. Entregar fuentes, deltas y evidencia. Online sigue mapa alfa; no afirmar cinco mapas online, WAN o FPS sin validación. Director retoma al finalizar la actualización.
