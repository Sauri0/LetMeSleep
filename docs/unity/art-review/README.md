# Catálogo visual de producción — Let me sleep

Pedido de Branko, 2026-09-12. Se incorpora a la revisión de alfa y crece con cada entrega. Las láminas de referencia orientan la presentación; las fichas se rellenan con assets y capturas reales, sin crear variantes o mecánicas por los textos de los bocetos.

## Qué contiene cada ficha

- ID estable, nombre, responsable, etapa, ruta del prefab y fuente editable, commit y hash del contenido capturado.
- Modelo armado: frente, espalda, ambos perfiles, dos vistas tres cuartos y giro continuo 360°. Vista superior/inferior para alas, suelas, patas, techos y piezas donde aporten información.
- Despiece: cada componente cosmético o modular aislado, a escala conocida, más el conjunto montado. Dimensiones, pivote y conexión cuando importan para ajuste o agarre.
- Materiales: superficie final y una vista técnica de malla/UV cuando se investiga un defecto. Luz neutra reproducible y muestra bajo la iluminación real del mapa.
- Movimiento: clip completo en bucle o acción completa, nombre, duración, poses de contacto/extremos y detalle del defecto. Una pose quieta no valida una animación.
- Variantes existentes: cada pieza revisada individualmente y combinaciones de riesgo (pelo/gorro, barba/cuello, ojos/gafas, manga/mano/objeto, alas/accesorios). Giro interactivo permite revisar ángulos intermedios; no se pretende enumerar infinitos ángulos ni declarar todas las combinaciones revisadas por una muestra.
- Incidencias con ID: zona, vista/frame, reproducción, responsable y comparación antes/después. Conservar evidencia anterior; una nueva captura no borra el defecto sin verificarlo.

## Estados separados

`Inventariado` → `Captura pendiente` → `Capturado` → `Revisado con fallas` o `Revisado sin hallazgos` → `Aprobado por Branko`.

Existencia de archivo, compilación o auditor numérico no implica aprobación artística. Cada evidencia identifica el asset exacto. Las capturas de versiones anteriores se etiquetan históricas y no acreditan un asset recién cambiado.

## Responsables

| Área | Producción de fichas/evidencia | Revisión |
|---|---|---|
| Humano, mosquito, ropa, caras, rig, clips y agarres | Modelador 1; Worker 2 conecta animación real | QA + Director |
| Casa, patio, lobby, arquitectura, muebles y props | Modelador 2; Worker 2 aplica iluminación | QA + Director |
| Menú, ajustes, personalizador, HUD y estados online | Revisar interfaz visual | QA + Director |
| Acciones, contactos, cámara, alcance y uso de objetos | Worker 1; modelos del autor correspondiente | QA |
| Audio y música | Worker 2: muestra, evento, contexto y niveles | Director + prueba de escucha |
| Índice, versión, almacenamiento y publicación | Director | QA |

Un solo turno de Unity/Blender pesado. Los autores preparan recetas y fuentes en sus worktrees; Director coordina la generación e integra. No editar un prefab o escena compartida desde dos tareas.

## Prioridad de alfa

1. Humano en pijama, mosquito y herramienta existentes: silueta, cara, manos, contacto y clips actuales.
2. Casa y lobby: ensamblaje, puertas, escaleras, juntas, muebles y apoyos.
3. UI: menú y personalizador a 720p/1080p; HUD y estados online reales.
4. Audio: eventos esenciales, transiciones, retorno al menú y cierre sin reproducción residual.

Las variantes de beta se añaden cuando existen. No producir cientos de láminas vacías ni convertir el catálogo en un sustituto de corregir la entrega.

## Archivos y uso

Código del catálogo: `tools/art_review/build_catalog.py`. Inventario independiente: `N:/LetMeSleep/Validation/ArtCatalog/qa-inventory.json`. Índice navegable y medios: `N:/LetMeSleep/Validation/ArtCatalog/`.

Cada ejecución produce un índice local con búsqueda, filtros y estados. Las capturas parciales muestran explícitamente lo que falta. Las láminas comparativas y vídeos se adjuntan como evidencia real; no se generan ilustraciones para simular progreso del motor.
