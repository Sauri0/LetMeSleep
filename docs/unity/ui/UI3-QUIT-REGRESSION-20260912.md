# UI3 — regresión SALIR y corrección acotada

Base recibida: UI1e795ed + Bootstrap6c021be, central2ee6e54 informado por Director. Se abrieron con view_image los ocho PNG `N:/LetMeSleep/Validation/TeamRecovery/ui-native3` (menú, humano, mosquito, settings a720/1080).

Regresión confirmada: SALIR no aparece en ambos PNG de menú; quedan icono y CERRAR EL JUEGO. La fuente conserva el texto/callback pero asigna 66 unidades al FeatureButton; sus insets de título (7+32) dejan27 para fuente22 con overflow Ellipsis. Las otras acciones de dos líneas usan72 y dejan33, visibles en la misma captura. Corrección: únicamente MainQuitButton pasa de66 a72; mantiene tratamiento QuietButton, ConfirmQuit, icono y subtítulo. La explicación por altura se valida definitivamente con recaptura.

Compilación estática Unity6000.3.24f1:0 errores/0 advertencias; diff --check sin errores. No Unity propio ni cambios a Bootstrap/Online/otros controles. Pedir menú720/1080, normal y foco SALIR, y confirmar que el pie no desborda antes de cerrar regresión.

Resto de UI3: paneles del personalizador y pie alineados; rendija sin mano residual en las muestras; Director informa menuCharacters false en Customization y true al volver. Settings conserva texto/valores/checks y columnas de video alineadas. No certifico mouse/teclado/Tab/Escape/persistencia por PNG. Mosquito sigue frontal/pequeño con vacío; modelos son anteriores, no humano9. Fondo y faroles magenta de menu1080 son seguimiento de Presentación. Aceptación artística global pendiente.
