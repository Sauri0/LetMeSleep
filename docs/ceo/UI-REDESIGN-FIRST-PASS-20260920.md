# UI — primera integración visual, no cierre de diseño

Se integra una primera mejora de menú, HUD y componentes comunes: navegación
abierta sobre la escena, jerarquía de acciones, acento cálido, profundidad de
botones, superficies redondeadas y bandeja horizontal de equipo. Entrada y foco
usan tiempo independiente de pausa y la preferencia de movimiento reducido.
No cambia la lógica de salas, inventario, cargas ni personalización.

Fuentes: AlfaUiController.cs, AlfaUiFactory.cs, AlfaUiTheme.cs. No se incorpora
el WIP ajeno de fuente Atkinson ni se aprueba el arte alfa visible en la escena.

## Evidencia y retrabajos

Carpeta `N:/LetMeSleep/Validation/V020/UiRedesignReview01`:

- Run-20260920-174454-576: 1/2, equipo desborda; CEO detecta además SALIR y
  ESTAMINA invisibles y panel normal excesivo. Primera versión rechazada.
- Run-20260920-175045-042: 1/2, ESTAMINA aún desborda. Segunda rechazada.
- Run-20260920-175446-331: 2/2, pero CEO detecta superposición de InteractionPrompt
  con inventario y letra huérfana en REUTILIZABLE. Tercera no aceptada visualmente.
- Run-20260920-180449-325: 2/2, cero omitidos/errores. CEO inspeccionó los ocho PNG:
  menú/HUD reales y carga/intercambio sintéticos a 1280×720 y 1920×1080.
  Texto visible, recursos completos y paneles separados en esos estados.
- Functional-20260920-180754-230: cinco pruebas funcionales de personalización
  pasan; sexta omitida porque faltó argumento explícito de evidencia. El gate
  completo no se declara PASS. Se conserva el intento.
- ModularVisual-20260920-180849-655: sólo el caso omitido, ahora con argumento,
  pasa 1/1. CEO inspeccionó ambos PNG; datos y personaje son sintéticos, el visor
  vacío es parte del fixture y no una demostración de catálogo terminado.

Builds CPU del worker sin errores/advertencias. Cuatro revisiones de diseño;
costos/tokens del worker desconocidos, no estimados a partir del tiempo.

## Pendiente de entrega

Esto acepta una primera integración, no cumple por sí solo el pedido de una UI
mucho más elaborada. Falta extender la composición a sala, ajustes, selección y
resultados; revisar interacción/foco/transiciones en movimiento; y mostrar el
catálogo final con personajes nuevos. Los gates estáticos 16:9 no certifican
otras relaciones de aspecto, WAN, FPS ni satisfacción visual del usuario.
