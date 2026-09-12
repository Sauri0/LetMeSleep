# Calidad obligatoria: UI, objetos y mapas

Branko amplió el requisito gráfico de personajes a la interfaz y al entorno el 2026-09-12: «nivel así de UI» y «nivel así de cosas y mapa». Esto se suma a CHARACTER-QUALITY-BAR.md y gobierna la recuperación de alfa.

Referencias originales conservadas en N:/LetMeSleep/References/UIQuality-20260912 y EnvironmentQuality-20260912: seis PNG por área, rutas originales y SHA-256 en sus manifest.json. Las imágenes fijan acabado y composición. Las etiquetas dentro de ellas no amplían modos, inventario, armas, progresión ni sistemas del plan.

## UI

- Logo propio Let me sleep con identidad cómica, legible; tipografía de título con carácter y texto secundario claro.
- Paneles nocturnos con profundidad, bordes y espacios intencionales; componentes consistentes y selección visible. Iconos grandes y reconocibles a 720p, no pequeños glifos difíciles de leer.
- Menú con jerarquía y fondo compuesto junto a Content/Presentation. Mantener protagonismo del juego sin tapar personaje ni acciones.
- Personalizador con visor proporcionado, miniaturas reales de las opciones existentes y paletas claras. Categorías, giro, zoom, guardar y volver evidentes.
- Lobby y online: código, copiar/invitar, participantes, listo y ajustes de anfitrión fáciles de entender. Respetar sorteo de roles.
- Ajustes agrupados por función; HUD breve que explique objetivo y acciones actuales; estados de carga/error/pausa/resultado consistentes.
- Transiciones breves y suaves; ratón, teclado, foco, Escape y escalado comprobados con capturas y uso real, no sólo jerarquía de objetos.

Responsable UI; M2/W2 producen y presentan el fondo. Director conserva callbacks/Online/Bootstrap y coordina integración.

## Objetos y mapas

- Objetos de silueta clara con partes funcionales: asas, patas, tapas, herrajes y uniones proporcionadas. Biseles/facetas elegidos para leer volumen, materiales distinguibles y apoyos exactos.
- Muebles con estructura y textil de volumen apropiado: colchones, almohadas y cojines deben leerse como tela rellena; puertas, cajones y estantes con grosor y ensambles.
- Habitaciones con función y composición: grandes formas primero, después elementos domésticos colocados por uso. Preservar circulación, visión y alturas jugables.
- Arquitectura completa en interiores/exteriores: marcos, puertas, zócalos, techos, escaleras, ventanas y fachada coherentes sin superficies coplanares que parpadeen.
- Patio y exterior con suelo, vegetación y rocas de escala variada, caminos y límites claros; profundidad y agrupaciones naturales. Evitar llenar el terreno con copias uniformes para simular detalle.
- Luz cálida localizada, relleno ambiental frío, contacto y materiales legibles. Evitar techos negros, focos quemados y brillos que oculten formas.

M2 rehace un conjunto representativo completo y lo propaga a casa, patio y lobby de alfa. W2 coordina materiales y luz. Los mapas posteriores mantienen el orden de entregas; no añadirlos ahora para sustituir el pulido de la casa.

## Criterio de cierre

Comparar referencias y resultado real en vistas equivalentes. Revisar cada objeto aislado y colocado, el entorno al recorrerlo y la UI en sus estados funcionales a 720p/1080p. Registrar brechas abiertas. Aprobar una compilación, una cantidad de objetos o un render aislado no demuestra que se alcanzó el acabado solicitado.
