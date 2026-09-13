# Mapas Higgsfield — producción autorizada

Branko aprobó los cinco conceptos y pidió ejecutarlos. Veinte bocetos Higgsfield terminados y revisados en `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/Bocetos/INDICE.md`.

## Estado actualizado — 2026-09-13, ejecución autorizada

Actualización 03:14 ART: CASA importada `hf-casa-del-patio-v1` con 3664 renderers, 3313 colliders, 29 materiales y los 5 spawns humanos con suelo/clearance. Fuente .blend final verificada; export original GLB bloqueó Blender, recuperado desde guardado sin perder autoría. Usar `HF_MAP_02_casa_UNITY.fbx` y `_UNITY.glb`; GLB estático sólo escena activa exportó en 2.6s. Chat CASA interrumpido exitosamente tras entrega para impedir reintentos. Render nativo en `02-casa/UnityFinal/`; emisiones autoradas pendientes de incorporar por extensión del contrato (Mapas está trabajando).

Blender visible ahora ejecuta CAMPAMENTO con Free mode: chat `e82b2f90-30b1-4aa6-9fd0-790e9977a202`, escena `HF_MAP_03_campamento`, primer envío aceptado. Saldo antes y durante =1971.73, no afirmar factura definitiva hasta cierre. Restan yate/puerto sin iniciar. No reenviar solicitud campamento ni cambiar escena mientras esté busy.

Gameplay tiene Unity CPU central para CASA (21 spawns, escalera y navegación); root se abstiene de abrir Unity. Isla: 47/48 casos PASS, cabaña sí accesible con ruta correcta. Único fallo vigente al muelle en contacto tangente con Path_South_Arrival_COLLIDABLE; candidato local de collider derivado preparado por Gameplay, todavía sin prueba nativa ni aplicación. Mirador y navegación aérea interior pendientes. No afirmar isla cerrada.

UI: selector de mapas inyectable integrado72895f1. Catálogo Bootstrap integrado17110f5, carga/iluminación por ID e inyección desde catálogo modificadas por root, todavía sin asset de catálogo instalado. Presentación prepara builder de catálogo/escena nueva; no modifica alfa. Water PlayMode isla PASS permanece válido. Auditor ahora usa materiales efectivos por objeto y exporta emisión Principled, para conservar colores de instancias y ventanas/faroles.

Los veinte bocetos están aprobados. Isla construida e importada como **hf-isla-del-laguito-v2**; CASA está en su primera creación Scene Builder (chat `0d0d7c32-fc34-4666-b717-4a6ecb54d99d`). Campamento, yate y puerto siguen preparados, todavía sin generación 3D. No confundir la aprobación de bocetos con entrega de cinco mapas jugables.

Isla final: 595 objetos fuente / 558 renderers Unity / 327 colliders; 5 spawns humanos y 16 mosquitos. Importación nativa y prueba PlayMode del agua PASS (1/1). Evidencia `01-isla/UnityFinal/`. El FBX original de la corrección quedó incompatible con Unity; usar exclusivamente `HF_MAP_01_isla_UNITY.fbx`, exportado por Blender estándar conservando geometría y materiales. No volver a editar binarios FBX.

Gameplay tiene asignado temporalmente Unity CPU batch, sin render, para terminar navegación y recorridos de Isla. Root no lanza Unity simultáneamente. Root conserva Blender visible, actualmente CASA. Presentación prepara un catálogo nuevo de Bootstrap sin editar carga/UI/Core ni abrir editores. Mapas disponible para recetas posteriores.

Saldo observado 1890.76 cr durante CASA, plan Ultra. El catálogo Scene Builder identifica su backend predeterminado `higgsfield/efficient-orchestrator` como `is_free_llm=true`; el selector Free mode omite el modelo y usa ese predeterminado. Validar consumo/calidad al usarlo en el próximo mapa. No asumir que esto vuelve gratuitas imágenes o generaciones 3D adicionales. Evitar seguimientos pagos para correcciones técnicas que resolvemos localmente.

Pendiente: finalizar CASA y los otros tres escenarios, comprobar importación/circulación/iluminación/agua, registrar mapas completos en carga de juego y entregar cierre verificable. Humanos y alfa anterior conservados.

## Historial: Isla del Laguito

Scene Builder real dentro de Blender visible, conversación `9d05439c-e4cd-4609-b212-60c345f8e4e8`. Primera entrega: BLEND/FBX/GLB completos, 471 mallas, 126177 triángulos, cabaña amueblada, senderos, mirador, muelles, bosque y agua con formas de onda. Fuente original preservada en `01-isla/SourceRevision01`.

Importación nativa Unity6000.3.24f1 completada con receta de revisión `hf-isla-del-laguito-review-01`: 471 renderers, 321 colisiones y tres componentes agua/espuma. Capa real del proyecto: Default (WorldStatic no existe). Ejes verificados por spawns Unity=(BlenderX,BlenderZ,BlenderY). Agua llega como MeshFilter1537/6561 vértices, dentro del límite de animaciónCPU; materiales lago3/océano4 conservados por nombre.

Captura y datos nativos: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/01-isla/UnityReview/`. Dos puntos humanos tienen suelo y espacio para cápsula; sus orígenes estaban aproximadamente2.5cm bajo el sendero. Esta prueba no certifica recorridos completos ni agua en ejecución.

Corrección activa FOLLOWUP-01: completar5humanos/16mosquitos, separar copas/troncos, sellar huecos entre tablones sin tapar aberturas, verificar contacto escalón y renovar exportaciones. No iniciar otra escena de Blender mientras la conversación esté ocupada.

## Resto

Casa del Patio, Campamento Pinar, Yate a la Deriva y Puerto del Faro tienen cuatro referencias y PROMPT.txt/attachments.json preparados por mapa. Todavía no fueron construidos. Instrucciones incorporan5humanos/16mosquitos, troncos separados, paredes cerradas y propiedades de colisión explícitas.

## Apoyo técnico asignado

- Modelador Terreno y Mapas: recetas verificadas por FBX/GLB/audit, sin abrir editores ni consumir créditos. Importador y primera receta integrados.
- Worker Presentación y Audio: API BindHiggsfield para iluminación específica por mapa y retorno al lobby, sin modificar arte/presets anteriores.
- Worker Código / Gameplay: fixture de navegación/cápsulas/motor sobre geometría importada, sin alterar reglas ni ejecutar nativos por su cuenta.

Root conserva turno Blender/Unity y coordinación. Personajes y alfa anterior siguen conservados. No declarar cinco mapas terminados por los bocetos, por el importador o por el primer render.

## Integración pendiente

Receta SpatialData no es plan de navegación: GameplayBotNavigation requiere schema_version1/map_id/zones/portals. No conectar receta como navegación al iniciar ronda. Validar capacidad, suelo/rutas/escaleras, agua en ejecución, límites, iluminación y composición. El catálogo y carga de partidas actual todavía usan house-patio-v1; las revisiones no están registradas como mapas finales.

Saldo observado tras primera isla y20imágenes:2514.46, desde2765.62;60cr fue cotización de imágenes. Diferencia de saldo no equivale a factura individual de SceneBuilder. Registro actualizado en Artifacts/Mapas/CREDITOS.json; vigilar cada mapa, no repetir solicitudes inciertas.
