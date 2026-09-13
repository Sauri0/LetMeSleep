# Mapas Higgsfield — estado de producción

Branko aprobó los veinte bocetos y los cinco mapas. Encargado de Higgfield coordina esta actualización; Humanos y alfa anterior quedan conservados. Fuentes y evidencia: N:/LetMeSleep/Artifacts/Higgsfield/Mapas.

## Estado 2026-09-13 03:48 ART

- Isla del Laguito: arte final importado como hf-isla-del-laguito-v2, 558 renderers y327 colliders. Agua PlayMode PASS1/1. Navegación schema1 instalada. Física47/48 PASS; muelle sur todavía falla por contacto tangente en el sendero. Primer candidato local permitió avanzar de Z-24.429 a-27.385, pero reaparece en el collider original restante; no se persistió candidato. Gameplay investiga soporte completo del mismo sendero. No declarar isla cerrada.
- Casa del Patio: arte final importado como hf-casa-del-patio-v1,3664 renderers,3313 colliders y29 materiales. Física28/28 PASS, incluyendo21 spawns, accesos, cocina y escalera en ambos sentidos. Delta autorizado Spawn_Human_03.001 en UnityY.2841115; fuente conserva posición anterior y debe documentarse/sincronizarse. Emisión de ventanas/faroles restaurada mediante reparación nativa; captura UnityFinal renovada. ContentHash53882423821aae0f458b5d7bb8afa1b0ba259dcae3199e72e1202cc4dc1462e6. Navegación semántica/runtime real pendiente; stress anterior de adaptador a velocidad máxima no equivale a BotController del juego.
- Campamento Pinar: Free mode produjo una base insuficiente de10495 triángulos, conservada en SourceFreeDraft. Refinamiento Astra autorizado y aceptado, chat82b3ac8f-56e3-421b-8c50-1702865a4d51. Escena HF_MAP_03_campamento en Blender visible, mismo diseño y cuatro referencias aprobadas. No reenviar ni cambiar escena mientras busy. No importado todavía.
- Yate a la Deriva y Puerto del Faro: cuatro referencias y prompts preparados por mapa, generación3D todavía pendiente.

## Presupuesto

Veinte imágenes terminadas, cotización60 créditos. Saldo inicial2765.62, observado durante refinamiento Campamento1751.11, Ultra. Free mode mantuvo saldo1971.73 durante su ejecución, pero su base no alcanzó calidad final. Las diferencias pueden incluir reservas; no son facturas por conversación. Sin nuevas imágenes, videos ni modelados pagados fuera de Scene Builder. Registrar IDs y no duplicar solicitudes inciertas.

## Integración y turnos

Unity CPU temporalmente asignado a Gameplay para sendero Isla y navegación CASA, sin render. Root conserva Blender y coordinación. Mapas prepara soporte explícito de morphs Wave1/Wave2 y batching estático, sin abrir editores. Presentación prepara catálogo/instalador sobre copia nueva de bootstrap, sin alterar alfa. UI tiene selector Training por catálogo integrado; catálogo final aún no instalado.

Carga por ID, iluminación reversible, importador de materiales efectivos/emisión y agua runtime ya integrados. SpatialData requiere navegación schema1; import-recipe.json NO es navegación. No habilitar mapas incompletos. Online mantiene mapa alfa; no afirmar selección de cinco mapas online ni WAN/FPS sin validación.

## Fuentes válidas

Usar HF_MAP_01_isla_UNITY.fbx y HF_MAP_02_casa_UNITY.fbx. El primer FBX Isla alterado por Scene Builder es incompatible y se conserva sólo como evidencia. CASA GLB estático recuperado desde fuente guardada como HF_MAP_02_casa_UNITY.glb, sin perder geometría. No parchear binarios; exportador Blender estándar restaura correctamente materiales por instancia y exporta sólo la escena asignada.

## Cierre pendiente

Terminar Campamento, Yate y Puerto; resolver colisiones/navegación; importar y verificar sus materiales, iluminación y agua. Construir catálogo de cinco mapas y escena de revisión del juego, probar selección/carga/retorno. Entregar fuentes y evidencia real; Director retoma tras cierre verificado, no por aprobación de bocetos.
