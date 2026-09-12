# Delta UI sobre evidencia nativa — 2026-09-12

Base: runtime heredado 5f4a381; capturas recibidas del Director con integración informada bf63666+b7a1186. Revisé con view_image los ocho PNG originales de `N:/LetMeSleep/Validation/TeamRecovery/ui-native`, y los informes de Revisar diseño visual/Revisor Funcional. Esta entrega no es aprobación visual: modifica fuentes/assets y necesita recaptura.

## Correcciones implementadas

- UN1 / FUI-V1: paletas en retícula de tres columnas, celdas de 216×66, texto de 20 sin autoencogimiento. Mostaza y los otros nombres conservan su texto; se aprovecha el espacio vertical. El orden de creación de opciones y callbacks permanece; hay que probar navegación de la retícula en motor.
- UN2: 13 pictogramas RGBA propios, con personas/grupo, camiseta, mosquito, altavoz, pantalla, mando, objetivo, ajustes y acciones. PNG de 128×128 antialias, regenerables con Pillow mediante `tools/build_ui_icons.py`. AlfaUiIcon carga sprites en Images y conserva la API y fallback anterior para iconos no sustituidos. Se evita volver a la ruta MaskableGraphic retirada por 0cdcbd3. Placas menos contrastadas e icono interior mayor.
- UN3: sombra reducida a 2–3 unidades, sin desplazamiento lateral; orden Outline → Shadow. El helper PlainShadow distingue el componente exacto porque Outline hereda Shadow. Evita modificar accidentalmente el borde al pedir la sombra. Cambio compartido: volver a observar lobby/HUD/modales además de estas cuatro vistas.
- UN4 / FUI-V4: visor deja de absorber todo el ancho sobrante; panel de 900 y opciones de 720, retícula de colores con más altura útil. Se conserva AspectRatioFitter y encuadre por bounds. La escala específica del mosquito/modelos nuevos requiere evidencia y coordinación con Presentación; no se modifica el prefab.
- UN5: ayuda visible ARRASTRÁ PARA GIRAR · RUEDA PARA ZOOM en cabecera del visor; ángulos y acciones del personalizador a 66 unidades (44 px a 720p, sujeto a tamaño real del layout).
- UN6: etiquetas de slider en columna de 190; pistas alineadas, tirador de 18×28 sin doble altura por anclas estiradas, valor porcentual/multiplicador visible. Valores se actualizan tanto por interacción como por PresentSettings con SetValueWithoutNotify.
- UN7: casillas usan tilde real, no sólo relleno; rol activo incluye prefijo `>` y el rol inactivo no toma azul de selección. Se mantiene diferencia entre dato seleccionado y foco pendiente de prueba.
- UN8 parcial: subtítulos de acciones y ayuda/versión de menú pasan a 16 unidades. Fondo y materiales siguen a cargo de Presentación/Elementos/Mapas.

## Verificación realizada y límites

`dotnet build unity/Library/UiNativeCheck/UiContractCheck.csproj --no-restore --nologo --verbosity minimal`: 0 errores, 0 advertencias. Compilación estática contra assemblies instalados de Unity 6000.3.24f1; no compila/importa el proyecto completo. PNG inspeccionados en lámina local con view_image; dimensiones/alpha/GUIDs y regeneración determinista comprobadas. `git diff --check` sin errores.

No inicié Unity, Blender, render 3D ni PlayMode. No toqué Online/Bootstrap ni creé procesos persistentes. Las capturas recibidas documentan defectos de la base, NO el resultado de esta corrección.

Pendientes reales: importación de texturas y recaptura 720/1080; nombres completos y selección de cada color; interacción de sliders/casillas y guardado/deshacer; foco con Tab/flechas y Escape/restauración; shader/tinte y legibilidad de iconos en runtime; encuadre del mosquito nuevo; estados de lobby y overlays afectados por estilo compartido. La restauración de foco del modal y binding Tab identificados en RECOVERY-REVIEW siguen abiertos y no se modifican en este delta visual.

Pedir la siguiente tanda con los mismos encuadres, el nombre Mostaza seleccionado y Settings con valores/casillas en ambos estados. Director y revisores deben mantener la aceptación gráfica pendiente hasta verla.
