# Mapas Higgsfield — estado de producción

Branko aprobó veinte bocetos y cinco mapas. Encargado coordina hasta cierre verificado. Arte nuevo generado con Higgsfield Scene Builder en Blender visible; alfa y humanos conservados. Fuentes: N:/LetMeSleep/Artifacts/Higgsfield/Mapas. Índice: ENTREGA.html (61 archivos,93 enlaces,20 bocetos y10 vistas; hashes verificados).

## Estado 2026-09-13, integración final en curso

- Isla hf-isla-del-laguito-v2: arte, agua, navegación y rutas nativas terminadas. Geometría original48/48 y263portales PASS después de corregir query tangente del motor humano; pico.9653mm. Sin proxies. Assets instalados en commit1c055f7.
- Casa hf-casa-del-patio-v1:44/44 y17pasajes PASS,16patrullas GameplayRuntime reales. Emisión y navegación instaladas. ContentHash019cca020c67d1e1d4d758556f76e3b2c553f715f21a0207f618ce3be01eede1. Spawn03 UnityY.2841115; copia exclusiva UnityAdjustedSource refleja ese marcador y conserva originales/exports anteriores. Assets1c055f7.
- Campamento hf-campamento-pinar-v2: arte corregido de winding; agua15objetos PASS. Collider cerrado derivado exactamente de Paths, Spawn05 medido y navegación aplicados por Unity API. ContentHashfc9399387e8a9d4bcae5f4a70e3291233170f54d92383d63a9a8b5cb648ad168. Regresión48/49 estricta,25/25portales y16/16Runtime PASS. Única excepción aceptada por coordinación: pico transitorio2.326175mm frente al contrato2mm; rutas completan y penetración final0. NO cambiar contrato ni afirmar49/49 PASS. Delta/proveniencia en Data/camp-technical-revision-v1.json y docs CAMP-V2-SEMANTIC-AND-PHYSICS.md. Assets1c055f7.
- Yate hf-yate-a-la-deriva-v3: fuente exclusiva NormalsV3 conserva arte y corrige32000caras del océano. NativeReview5/5 suelo libre; agua GPU y espumaCPU PASS2/2 (21385vertices originales intactos y movimiento de píxeles). Navegación en validación:49casos iniciales37/49; baúl y tapa bloquean escalera SwimToAft, autorizada reubicación conjunta medida en cubierta, sin cambiar mallas/materiales. Ajustar rutas y grafo de aire entre niveles. No cerrar aún.
- Puerto hf-puerto-del-faro-v1:811objetos/730meshes/175746tri,458colliders, cinco spawns humanos con piso y cápsula libre. Imagen nativa revisada; agua GPU3721vertices y espumaCPU PASS. Mapas prepara regiones/portales/rutas offline, Gameplay revisa y ejecuta navegación. No cerrar aún.

## Integración de juego y equipo

UI de entrenamiento pasó pruebas uGUI/TMP con IDs sintéticos y renders720/1080 revisados. Nuevo selector de mapas de sala integrado e51c7d2, pendiente prueba nativa específica. Core allowlist1899a65 acepta sólo alfa y cinco IDs finales, conserva autoridad/protocolo y reinicio de ready;115checks CPU PASS. Root conecta Bootstrap, conserva MapId al cambiar cantidad humana y protege clientes sin catálogo. No afirmar WAN.

Cinco skyboxes creados por API en una ejecución dedicada. Config de catálogo final: artifacts/Mapas/UnityPackage/catalog-input.json; falta navegación Yate/Puerto para crearlo. Después instalar copia Assets/Scenes/LetMeSleepHiggsfield.unity, fixture nativa de lighting y diez sesiones locales de entrenamiento (5mapas×2roles). Tests existen; aún no ejecutados contra catálogo.

Turno Unity: Gameplay para Yate/Puerto. Mapas apoya propuesta Puerto sin Unity. UI prepara runner de sala con aislamiento de fuentes/settings; Online entregó allowlist y quedó terminado. Root mantiene integración y fuentes. No nueva generación Higgsfield.

## Créditos y extensión

Saldo observado1233.59 Ultra tras cinco construcciones. Inicial2765.62; diferencia neta1532.03 puede incluir reservas/reconciliaciones, no factura individual. Veinte imágenes cotizadas60créditos. Sin nuevos cargos de generación.

Dependencias del addon habían desaparecido de site-packages tras procesos headless previos. Restauradas49wheels compatibles de su propio paquete, sin descarga ni cambio de versión. Fresh Python importa blmcp/httpx/pydantic/aiortc/skia/yaml y conecta a Puerto en Blender. Recibo addon-dependencies-restore-receipt.json. No reiniciar Blender ni usar headless con recursos de usuario compartidos; aislar recursos en futuras ejecuciones.

## Pendientes de cierre

Completar Yate/Puerto, catálogo/escena y carga real. Preservar y restaurar cambios de fixture en fuente Atkinson y EditorSettings (eran limpios antes del UI batch; no publicar tablas temporales). No confundir esos cambios con WIP previo de Humanos, AGENTS, QualitySettings o asmdef. Commit selectivo de contenido final y documentación; entregar fuentes ajustadas y evidencia. Director retoma al finalizar, no por aprobación de los bocetos.
