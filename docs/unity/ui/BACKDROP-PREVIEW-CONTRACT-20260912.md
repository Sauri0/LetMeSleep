# Contrato UI / fondo de menú / visor

2026-09-12. Acordado por mensajes con Worker Presentación y Audio; Director conserva hookup en Bootstrap. Este contrato no declara integrada la ocultación del decorativo ni aprobado el encuadre.

## API UI disponible en este delta

- `AlfaUiController.CurrentScreen`: lectura del contexto actual.
- `event Action<AlfaUiScreen> ScreenChanged`: notifica un cambio de pantalla después de activar/desactivar vistas, ajustar el visor y asignar foco. No se emite si se presenta otra vez la misma pantalla. Una excepción del suscriptor se registra con Debug.LogException.
- Confirmación/modal conserva CurrentScreen y no emite otro ScreenChanged. Así Personalización → Confirmar descarte → Seguir editando mantiene el contexto Customization.

Al conectar un observador al controlador real, suscribirse y sincronizar una vez con CurrentScreen: el evento no reproduce la inicialización anterior a la suscripción. Desuscribirse al destruir el adaptador; no crear otro controlador UI.

```csharp
// handleScreen es el callback del adaptador real de Presentation/Director.
ui.ScreenChanged += handleScreen;
handleScreen(ui.CurrentScreen);
// Al destruir o desconectar ese adaptador:
// ui.ScreenChanged -= handleScreen;
```

## Responsabilidades de Presentation / Director

1. Identificar explícitamente los decorativos del diorama de menú. Cuando CurrentScreen es Customization, suspender únicamente su presentación visual y restaurar el estado previo al salir. No buscar todos los objetos Human/Mosquito ni desactivar el root completo de una sala.
2. Mantener mundo, iluminación, cámara del fondo y decoración doméstica. No oscurecer la pantalla para encubrir la rendija entre paneles.
3. No tocar cámara, stage, RenderTexture ni `CharacterPreviewOrbit.CurrentInstance` desde ese manejador: pertenecen al visor. No desactivar personajes de lobby/partida ni alterar colisión/escala física.
4. Si el contexto de escena cambia estando Customization abierto, reconstruir el conjunto de decorativos autorizados y sincronizar CurrentScreen. UI no conoce la identidad de los actores de Presentation.
5. Al volver, restaurar la visibilidad previa; no encender un decorativo que ya estaba oculto por otra condición.

Presentación revisó el ownership real: AlfaApplication.LoadMap crea menuCharacters = MenuCharacterDisplay con HumanMenuStage/MosquitoMenuStage; es un root decorativo con colliders desactivados. Director puede controlar únicamente ese root, sincronizando al suscribirse y después de LoadMap. No hace falta un componente nuevo en Presentation. El evento por sí solo no oculta ningún objeto. El hookup y la identidad de los decorativos siguen pendientes de integración central.

## Visor y encuadre por especie

Worker UI conserva AspectRatioFitter, ángulos Frente/Perfil/Espalda/Centrar y límites actuales calculados por bounds. En esta composición no se modifica CharacterPreviewOrbit ni se ajusta distancia contra los modelos viejos de native2.

Worker Presentación acordó evaluar tres cuartos/escala visual para mosquito al llegar el modelo nuevo, sin cambiar escala física. La luz/fondo del visor debe separar patas y membrana. La captura inicial debe registrar modelo, orientación, distancia y textura; después giro completo y extremos de zoom. El ajuste final se decide sobre esa muestra, no sobre reference8/reference9 ausentes en native2.

## Receta de comprobación del hookup

Con editor del Director: menú → Personalizar → humano/mosquito → cambio no guardado → Escape → Seguir editando → Escape → Salir. Capturar la separación entre paneles en cada contexto. Esperado: sin mano/personaje decorativo residual durante la personalización y sus confirmaciones, visor siempre visible, decorativos anteriores restaurados sólo al volver. Repetir entrada/salida y cambio de escena; no aceptar una máscara negra como solución.
