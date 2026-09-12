# Delta de composición tras native2 — 2026-09-12

Base UI: 43b2eb6; evidencia nativa de central UI2c5c820 / HEAD45bc248 / Editor20212-noaudio. Se incorporaron ambos informes independientes: `Validation/TeamRecovery/visual/UI-NATIVE2-REVIEW-20260912.md` y seguimiento de Revisor Funcional. UN1/UN2/UN3/UN6 y carencias estáticas de UN5/UN7 están corregidos en las muestras; no se reabren sin regresión. FUI-V4 y composición global permanecen abiertos.

## Cambio delimitado

- Marca: dos líneas de lettering con escala diferenciada y mosquito junto a SLEEP, en un único bloque. Retirado divisor interno redundante y eyebrow; un solo divisor separa marca de navegación.
- Menú: riel de 880 unidades; JUGAR ONLINE gana altura de 88 y conserva acento azul. SALIR pasa a tratamiento auxiliar oscuro de 66; la confirmación destructiva y callback siguen vigentes. Las cinco acciones conservan sus nombres.
- Personalizador: visor y opciones comparten altura de 900 y padding de 20. GUARDAR ocupa el pie primario de 72; Deshacer/Volver quedan debajo, en una fila secundaria de 66. Ángulos de 66 con menos masa/contraste y tipografía de 20. No se reduce el hit target para hacerlos secundarios. Las paletas de tres columnas, los nombres, los pictogramas y los callbacks se conservan.
- Ajustes: contenedor de 840, tarjetas de Audio/Controles/Video ajustadas a filas; fondo exterior Night800 separa grupos Night700. Resolución/Calidad comparten columna de etiqueta y flechas de 66. Acciones auxiliares reciben el mismo tratamiento secundario.
- Contrato de fondo: evento aditivo `ScreenChanged` junto a `CurrentScreen`, publicado después de activar pantalla/visor. Coordinado con Presentación: sólo decorativos del diorama de menú se ocultan durante Customization; Director conecta Bootstrap. No se añade scrim negro ni se manipulan personajes ajenos desde UI.

## Verificación y entrega

Compilación estática contra Unity6000.3.24f1: 0 errores / 0 advertencias. Revisión de diff: dos fuentes runtime UI y documentación; APIs de acciones y rutas Online/Bootstrap intactas. No se abrió motor/render ni se ejecutaron pruebas pesadas. Este delta requiere recaptura y no acredita el resultado artístico.

Recapturar menú con foco en Jugar/Salir, personalizador humano/Mostaza y mosquito con pie completo, Ajustes y sus campos largos, todo a 720/1080. Comprobar que las jerarquías nuevas no recortan texto y que primarias/auxiliares/foco se distinguen. Revisor Funcional conserva pruebas pendientes de input, Tab/Escape/restauración, aislamiento de sliders, guardado y persistencia.

Pendientes por dueño: Director/Presentación conecta BACKDROP-PREVIEW-CONTRACT; geometría vieja/fondo lobby anterior de native2 no acreditan modelos nuevos ni living. UI/Presentación decide encuadre de mosquito nuevo con tres cuartos y luz legible cuando llegue evidencia. La composición de esta entrega no corrige por sí sola esas dependencias.
