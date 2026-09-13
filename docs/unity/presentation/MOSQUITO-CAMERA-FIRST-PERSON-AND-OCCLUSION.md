# Mosquito: cámara cerca de pared y primera persona

Cambio autorizado tras auditoría de fuente. La instrucción posterior de Branko **anula** cualquier mínimo de zoom: GameplayRuntime/input/red conservan0..2.5 y cero es primera persona intencional. No se interactuó con ejecutable del usuario ni se ejecutó Unity.

## Implementación

- MosquitoFollowCamera Late1250 después de ActorVisualBinding1100, VisualAttention1200 y corrección de picadura eventual1220. Lee pivot/huesos del frame final.
- Binding local exclusivo desde GameplayVisualPresenter: renderers del CharacterView local, huesos Thorax/Head/Abdomen01/Abdomen02 y anchor AimForward. No se instala sobre actores remotos. Unbind antes de destruir visuales/cambiar contexto.
- Bounds corporal cacheado al bind desde vértices reales con influencia de esos huesos y sus bindposes. Cada frame transforma8esquinas por hueso; aplica escala autor una sola vez. Alas/venas no inflan el volumen corporal. Es envolvente conservadora, no malla exacta. Si no existe skin legible/core, fallback a bounds reales de renderers, nunca a un radio R4 inventado.
- Margen de entrada=max(padding de cámara, esfera que contiene near plane según near/FOV/aspect). Salida añade max(padding,15% radio de bounds). Evita parpadeos de visibilidad en frontera. Valores derivan tamaño y lente reales; no distancia mágica de ocultación.
- Primera persona: al acercar zoom dentro del radio corporal+margen, interpolación suave CameraTarget→AimForward. Cero deja distancia0 al pivot frontal resuelto. El barrido actor→pivot también valida este movimiento, así no pone el ojo detrás de una pared. Orientación/FOV/near existentes se conservan; no toca input ni mira autoritativa.
- Oculta sólo renderer.forceRenderingOff propios durante transición1P o entrada en bounds+margen; guarda valor previo al ocultar. Alejar/exitband/cambioactor/disable/unbind/destroy restaura cada flag, sin tocar renderer.enabled, materiales, colliders, GameObjects ni otros actores. Un renderer que ya estaba forzado invisible sigue invisible al restaurar. Se rechazan bindings ajenos/duplicados.
- Dos sweeps antiWORLD y reducción inmediata de distancia permanecen. **Nunca se impone mínimo resuelto que atraviese pared**. Telemetría disponible DesiredDistance, EffectiveRequestedDistance, ResolvedDistance, IsFirstPerson, IsLocalVisualHidden, BodyWorldBounds y márgenes.
- MainMenuLivingScene elimina únicamente Light.shadowResolution, setter Built-in incompatible con URP. Quedan parámetros de luz y sombras idénticos; URP resuelve tier con pipeline. No se filtran warnings LMS_BITE.

## Pruebas entregadas

Se conservan pruebas existentes de pared/suelo/techo y puerta fina con control negativo. Nuevas pruebas PlayMode:

1. Entrada, banda de histéresis, salida, valores previos forceOff/enabled, rechazo de renderer ajeno, collider intacto, cambio actor/disable/unbind/destrucción del componente.
2. Skin sintética con vértice de ala extremo: core bounds lo excluye, responde al hueso y escala.5 exactamente una vez.
3. Pared cercana reduce distancia por debajo de tamaño corporal y oculta propio sin atravesar world; después zoom0 llega a anchor frontal fuera de bounds y sigue oculto; al alejar reaparece.

Código runtime y tests compilan offline contra Unity6000.3.24f1/NUnit, cero errores/advertencias. Proyecto local `work/surface-r3-native/observer-package/CameraOcclusionTests.csproj`. **No se ejecutaron tests PlayMode aquí**.

## Receta Director

FreshEditor/compilingfalse en turno concedido: ejecutar MosquitoFollowCameraPlayModeTests completos. Luego R4 real personalizado en entrenamiento: rueda.85→0→2.5, vuelo libre, pared detrás, esquina/puerta, perchsuelo/pared/techo, entrada/salida1P con mirar arriba/abajo. Verificar espacio visible, ausencia de abdomen opaco/cortesworld y restauración al volver humano/menú/partida; comprobar otro mosquito sigue visible. Registrar bounds/desired/resolved/hidden durante transiciones. Si renderer.bounds fallback produce ocultación prematura, comprobar4corebones/readability, no estrechar umbral a ciegas. No afirmar calidad/FPS sin ese run.

Límite: forceRenderingOff afecta ese renderer local en todas las cámaras del mismo cliente mientras se oculta; no otros clones/otros clientes. UIpreview usa otra instancia. No se introduce un shader transparente ni cambios globales de visibilidad. AntiWORLD mantiene límites anteriores de buffers/depenetración; este cambio no pretende cerrar toda patología de geometría.
