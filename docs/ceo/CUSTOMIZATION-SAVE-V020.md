# Personalización guardada v0.2.0

Estado: P26/P27/P28 aceptados para el catálogo actual de colores. Este documento define el contrato y la evidencia local; no acredita red local, WAN ni una partida con otras personas.

La versión conserva el catálogo actual de colores. No agrega piezas, arte ni un catálogo nuevo.

El acceso de lobby comparte la fila existente de acciones con **RECORRER**; no agrega altura al panel de reglas. La prueba de UI comprueba sus rótulos en el canvas activo. Si el gate entrega `-customizationSaveEvidence <directorio bajo Validation/V020>`, la misma prueba captura esa fila a 1280×720 y 1920×1080.

## Catálogo pendiente

P28 guarda una sola combinación de los componentes disponibles hoy: tono de piel, color de pijama y color de mosquito. Siguen pendientes de producción e integración las piezas acordadas de cabello, rasgos, vello facial, ropa separable, accesorios, gafas, calzado, cuerpos, alas, expresiones, patrones y accesorios de mosquito. Este trabajo no afirma que la personalización completa esté lista.

## Estados que se conservarán

`appearance` es la apariencia publicada: el canal 2 existente la comunica a integrantes de la sala. `localAppearanceDraft` es la única combinación guardada localmente. Nunca se envía por red. `previewAppearance` alimenta la vista 3D y debe inicializarse desde el borrador local al cargar preferencias.

`AlfaApplication.Appearance.cs` y `TickAppearance` no se modifican en este ticket: continúan serializando únicamente `appearance`. La prueba local comprueba que previsualizar no cambia esa representación publicada; la confirmación de que un par remoto recibe sólo la apariencia aplicada requiere el gate nativo de red.

Un perfil schema 1 sin `localAppearanceDraft` se migra tomando `appearance`. Un perfil de schema no compatible continúa bloqueado por el almacenamiento existente y no se reescribe.

## Comportamiento esperado

- Cada cambio válido guarda el borrador local con la escritura atómica existente. Si falla, se recupera el estado local anterior y se muestra el fallo real.
- Al entrar a Personalizar se fija una base de sesión. **DESHACER CAMBIOS** vuelve y guarda esa base, aun si la persona ya pulsó **APLICAR**.
- **APLICAR** persiste en la misma escritura el borrador local y la apariencia publicada. Sólo este paso habilita la comunicación que ya realiza `TickAppearance`.
- Cerrar con **VOLVER** o Escape conserva el borrador local sin un diálogo de pérdida. Desde el lobby, **VOLVER** retorna a la misma sala.
- Las actualizaciones de `PresentLobby` no deben sacar a la persona de Personalizar mientras edita la misma sala en espera; el contexto se ancla al código de sala. Otra sala, una ronda iniciada o una salida siguen su transición normal.

## Evidencia local

`customization-native-03.xml` ejecutó las tres pruebas de `LetMeSleep.Tests.PlayMode.CustomizationPersistencePlayModeTests`: 3/3 PASS, Unity 6000.3.24f1. Cubren restauración tras reinicio, vista previa restaurada, migración schema 1 sin borrador, no publicación al previsualizar, aplicación, deshacer con base de sesión, fallos de escritura al guardar local y aplicar, schema no admitido, preservación de los campos de vídeo cuando la ruta de override está activa, y snapshots de lobby durante edición.

El recibo `CustomizationSaveNative03/customization-save-lobby-layout.txt` registra canvas 1280×720 y 1920×1080. Las dos capturas fueron inspeccionadas: **RECORRER** y **PERSONALIZAR** aparecen juntos y legibles; **TIEMPO** permanece en una línea. Es evidencia Canvas local, no una sala, par o prueba WAN.

El intento native-01 no ejercitó la coexistencia porque el fixture tenía `CanExplore=false`. native-02 sí mostró ambos controles y falló 2/3 por overflow de `LobbyExploreButton`; se corrigió sin ocultar los controles ni retirar la aserción. native-03 vuelve a ejecutar el filtro y pasa 3/3.

La transición real de una sala remota hacia juego/cierre sigue siendo un gate de red posterior. El transporte no cambió: `AlfaApplication.Appearance.cs` conserva `TickAppearance` y sólo lee `appearance` publicada.
