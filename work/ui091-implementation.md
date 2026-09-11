# Interfaz visual 0.9.1 — personalización

Base: `59af8ad`. Propiedad limitada a `ui.gd`, `avatar_preview.gd`, la prueba
`ui091_customization_checks.gd` y esta evidencia. El catálogo y la geometría
pertenecen a Modelador 2.

## Contrato coordinado

Modelador 2 confirmó que este tramo conserva todos los IDs, etiquetas, conteos y
categorías de `cosmetics.gd`. La interfaz no cambia el perfil guardado, la carga
de preferencias, los mensajes online ni las opciones disponibles.

## Cambios

- La miniatura de cada categoría representa ahora su ID guardado, y el tooltip
  nombra la opción activa. Antes todas las categorías mostraban el ID 0.
- El resumen inferior incluye rol, categoría y opción para evitar nombres
  aislados ambiguos.
- Frente, Perfil y Espalda muestran un único estado activo. Una rotación libre
  por ratón o flechas despeja el preset; Restablecer recupera la vista completa.
- Hay botones visibles de acercar/alejar y controles de teclado en la vista 3D:
  flechas para orbitar, `+`/`-` para zoom e `Inicio`/`0` para restaurar. Flechas
  y zoom admiten repetición al mantener la tecla.
- Al abrir una categoría con teclado, el foco pasa a la opción guardada. `Esc`
  conserva el regreso de un nivel.
- La comparación neutral existente para ojos, cejas, boca y vello facial se
  preserva sin duplicar la corrección ya integrada.

## Validación

| Comprobación | Resultado |
|---|---:|
| Importación limpia del worktree | exit 0 |
| Parse de `ui091_customization_checks.gd` | exit 0 |
| `ui091_customization_checks.gd`, headless | 20/20 |
| `ui091_customization_checks.gd`, 1280×720 nativo | 22/22 |
| `ui091_customization_checks.gd`, 1920×1080 nativo | 22/22 |
| `customization08_checks.gd` | 137/137 |
| `ui_navigation_test.gd`, renderer nativo | 163/163 |

La prueba nueva entrega una flecha mediante `Input.parse_input_event` con foco
real en el preview; la llamada directa se usa sólo para cubrir la repetición del
evento sostenido. También verifica selección, miniaturas, tooltips, zoom,
neutral facial, cambio de rol, `Esc` y restauración de preferencias.

Una corrida headless de `ui_navigation_test.gd` no se toma como resultado: el
driver sin pantalla no ofrece portapapeles. La misma prueba pasó 163/163 con el
renderer Windows nativo. El SHA-256 de `preferences.cfg` después de las pruebas
coincidió con su backup (`CF909BC4DC2C78E92CC4…`). No quedaron procesos Godot.

## Evidencia visual

- `work/ui091-captures/human-eyes-front-1280x720.png`
- `work/ui091-captures/mosquito-eyes-front-1920x1080.png`

Ambas capturas conservan visibles las categorías, tres tarjetas de opciones,
selección de rol, selección facial, ángulo, zoom y resumen guardado. No se
observan solapamientos ni recortes a 720p o 1080p. Las mallas mostradas son las
de la base `59af8ad`; Modelador 2 reemplazará los rasgos dentro de los mismos IDs.

La revisión integrada posterior debe cubrir las tres variantes de ojos, cejas y
boca de ambos roles en Frente y Perfil, y pelo, antenas y accesorios en Frente,
Perfil y Espalda. La prueba WAN sigue pendiente y queda fuera de este tramo.
