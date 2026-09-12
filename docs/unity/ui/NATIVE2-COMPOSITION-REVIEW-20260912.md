# UI nativa 2 — revisión del autor y siguiente composición

Fecha: 2026-09-12. Fuente UI `43b2eb6`, integración UI `2c5c820`, HEAD central `45bc248`, Editor 20212 con -noaudio informados por Director. Revisadas con view_image las ocho capturas nativas en `N:/LetMeSleep/Validation/TeamRecovery/ui-native2`: menú, humano, mosquito y ajustes a 1920×1080 y 1280×720. No se abrió Unity ni se cambió runtime para esta revisión. Se recibieron ambos informes independientes: cierran defectos estáticos de componentes en las muestras y mantienen composición global e interacción pendientes. Los modelos capturados son los anteriores; no representan reference8/reference9. El fondo es el lobby anterior: el rebuild de living falló por índices de malla y el fix d1d5af8 estaba pendiente según Director.

## Lo que sí demuestra esta tanda

- Mostaza aparece completo en ambos tamaños, con marca de selección; el humano muestra ropa amarilla. Director indica selección por invocación programática de onClick y posterior Deshacer, sin guardar. Eso no certifica clic físico, foco ni teclado. Esto cierra el recorte visible de ese estado; no demuestra persistencia ni recorrido por teclado.
- Personas, camiseta, mosquito y símbolos de ajustes son reconocibles. Los contornos ya no muestran los grandes rectángulos desplazados del lote anterior.
- Los sliders comparten inicio de pista, tienen tiradores separados y muestran porcentajes/multiplicadores. Pantalla completa muestra una tilde. Las imágenes no prueban que cada control actualice únicamente su valor ni que Aplicar persista.
- La indicación de arrastre/rueda es visible; no prueba el gesto. El rol seleccionado incorpora `>` y la otra pestaña queda neutra.

## Brecha respecto de las referencias y próximo alcance propuesto

| Área | Evidencia actual | Próximo ajuste delimitado |
|---|---|---|
| Marca/menú | El mosquito del logo sigue aislado en la esquina derecha de un bloque tipográfico angosto. Dos líneas ámbar separan contenidos consecutivos; riel lleno de superficies equivalentes. | Integrar símbolo y lettering en un único conjunto; eliminar un separador; compactar el encabezado y reservar la mayor masa/contraste para JUGAR ONLINE. Mantener cinco acciones y el espacio de la escena. |
| Jerarquía de acciones | SALIR rojo ocupa casi la misma caja/peso que JUGAR ONLINE. En personalizador, cuatro ángulos compiten como grandes acciones y Guardar/Deshacer/Volver forman tres franjas casi iguales. | Salir con tratamiento auxiliar y confirmación existente; pie de personalizador con Guardar dominante y Deshacer/Volver secundarios. Mantener hit targets y foco distinguible; no reducirlos para ganar espacio. |
| Alineación del personalizador | Opciones empieza 30 px antes y termina 30 px después del visor a 1080, por alturas 960/900. Cabecera de visor y título de opciones no establecen una línea común. | Unificar bordes superior/inferior y línea de títulos; anclar pie de acciones a una misma banda. Conservar celdas y nombres completos. |
| Superficies | Grandes rectángulos de azul uniforme producen apariencia de formulario. En mosquito, la mitad inferior de opciones queda vacía mientras la escala del modelo sigue pequeña. | Separación más contenida entre escenario y controles, superficies diferenciadas por función y un pie estable. No rellenar con funciones o paletas inventadas. Evitar añadir otra capa de borde/sombra como sustituto de composición. |
| Visor/mundo | Un fragmento de la mano del humano de fondo se asoma entre paneles. Mosquito frontal ocupa una fracción pequeña del cuadrado; cambiar el ancho del marco no resolvió la altura vacía. | Coordinar fondo y cámara con Presentación. Probar vista tres cuartos para leer abdomen/alas y encuadre por especie dentro de UI, manteniendo giro/zoom y sin alterar prefabs/escala física. Esperar modelos nuevos para decidir distancia final. |
| Ajustes | Legibilidad mejorada, pero controles y video tienen amplio vacío bajo las filas; el panel exterior deja otro vacío antes del pie. Resolución/Calidad todavía inician flechas en ejes distintos. | Alinear columnas de video y ajustar alturas de tarjetas a su contenido con pie común. Preservar agrupaciones y capacidades reales; no sumar opciones para ocupar espacio. |
| Texto/foco | Secundarios se leen mejor, pero valores de sliders/ayuda siguen pequeños a 720. Ninguna imagen acredita Tab/Escape/restauración. | Medir jerarquía final a 720 y revisar foco/contraste de modo separado; mantener abiertos los hallazgos funcionales de RECOVERY-REVIEW. |

Las referencias muestran acabado, volumen doméstico, intención tipográfica y equilibrio con escenario. Resolver truncados y compilar no alcanza ese nivel. La nueva tanda mejora los componentes, pero no recibe aprobación artística.

## Dependencias

Revisión independiente de ambas tareas incorporada; el delta posterior está descrito en COMPOSITION-DELTA-20260912.md. Director conserva editor/proceso y produce las siguientes capturas. Presentación coordina fondo/encuadre/luz con los nuevos modelos; Worker UI mantiene cambios en Runtime/Assets. Lobby, HUD y modales aún necesitan capturas equivalentes por compartir componentes.
