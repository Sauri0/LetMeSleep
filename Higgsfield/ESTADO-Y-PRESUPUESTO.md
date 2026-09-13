# Higgsfield para Let Me Sleep

Verificado el 12/13 de septiembre de 2026, después del upgrade. Las fechas de beneficios se transcriben como aparecen en la web; no se verificó su hora exacta de vencimiento.

## Estado técnico

- Blender 5.2.1 LTS: `N:\Blender\blender.exe`.
- Complemento oficial Higgsfield 1.5.52 instalado y habilitado. Requiere Blender 5.1 o superior.
- Inicio de sesión del complemento completado. Su SDK confirmó `plan: ultra` y `credits: 3000`.
- Conexión local verificada contra la escena inicial: Camera, Cube, Light. No se abrió ni modificó ningún archivo del alfa.
- MCP `higgsfield_blender_local` registrado en Codex, usando el servidor `blmcp` incluido en el complemento y el Python de Blender. Handshake y listado de 26 herramientas correctos. La ejecución local de una consulta a la escena también funcionó.
- Blender debe estar abierto para que la conexión local responda. Coordinar siempre el slot con Director antes de abrir Blender, exportar o renderizar.
- El Bridge remoto del complemento indicó `running`. El cliente OAuth remoto de Codex falló por discrepancia de issuer (`clerk.higgsfield.ai` frente a `higgsfield.ai/_clerk`); esa entrada quedó deshabilitada, sin reducir las validaciones de autenticación. Para este equipo usamos la conexión local.
- Preferencias originales respaldadas en `C:\Users\brank\AppData\Roaming\Blender Foundation\Blender\5.2\config\userpref.before-higgsfield-20260912-224244.blend`.
- La ventana de verificación PID20824 fue cerrada limpiamente, con escena inicial sin cambios, para devolver el slot al Director.

## Plan y saldo verificados

- Plan: **Ultra**.
- Saldo disponible: **3.000 de 3.000 créditos**. Gasto de esta instalación/auditoría: **0**. Generaciones enviadas: **0**.
- Renovación mostrada: **12 de octubre de 2026**. Factura próxima mostrada: **USD 129**.
- Recarga automática desactivada; saldo de recargas automáticas: 0.
- La web conservaba algunas etiquetas de Plus/1.000 créditos y concurrencia 6 videos/8 imágenes después del upgrade. El nombre Ultra, el saldo 3.000/3.000 y el SDK coinciden. No usar esas etiquetas residuales para garantizar límites de concurrencia.
- La tarjeta comercial de Ultra anuncia generaciones pagas paralelas ilimitadas; su tabla comparativa todavía indica 8 videos/8 imágenes. Hay inconsistencia de presentación, por lo que no se promete una concurrencia efectiva ilimitada. Para cuidar el presupuesto se trabajará inicialmente con un recurso por vez.
- No hay una garantía de cantidad de assets finales: depende del costo de la configuración elegida, los intentos y la reparación posterior.

## Beneficios ilimitados activos observados

La web cuenta 11 registros de beneficios y repite algunos. Se agrupan aquí por modelo; no son 11 modelos distintos.

| Modelo | Beneficio observado | Uso recomendado para el juego |
|---|---|---|
| Nano Banana 2 | 2K, 30 días, hasta 13/10/2026; también hay registros de 7 días | Referencias, variantes de pijamas y muebles, vistas de objetos |
| Nano Banana Pro | 2K, 7 días, hasta 20/09/2026 | Diseños que necesiten más precisión y consistencia |
| FLUX.2 Pro | 365 Unlimited, 1K, activo y autorrenovable | Exploración visual y referencias |
| GPT Image | 365 Unlimited, activo y autorrenovable; versión no especificada en esa fila | Ilustraciones, iconos y ajustes visuales |
| Seedream 4.5 | 365 Unlimited, activo y autorrenovable | Diseño de ambientes y materiales de referencia |
| Kling O1 Image | 365 Unlimited, activo y autorrenovable | Variantes y edición de referencias |
| Nano Banana | 365 Unlimited, activo y autorrenovable | Ediciones de imágenes de referencia |
| Seedream 5.0 Lite | 365 Unlimited, activo y autorrenovable | Exploración de bajo costo marginal dentro del beneficio |

La FAQ de precios explica que “365 Unlimited” da un año sobre los modelos incluidos, condicionado a mantener una suscripción elegible; termina cuando finaliza el plan tras cancelar o bajar a uno que no lo incluya. La cuenta muestra estos beneficios como autorrenovables. Revisar parámetros permitidos y vigencia antes de usarlos. Las generaciones ilimitadas usan la cola estándar y las pagas la prioritaria. El contador separado de generaciones gratuitas mostraba 0.

Los ilimitados web no se trasladan automáticamente al MCP, CLI, Supercomputer ni Blender. La documentación general los limita al uso manual en higgsfield.ai. El esquema del conector contempla algunas promociones especiales de MCP: sólo utilizarlas si el servicio confirma expresamente elegibilidad y costo cero. Nunca automatizar la web para eludir esta distinción.

## Modelos y costos consultados mediante el SDK autenticado

Son estimaciones de solo lectura obtenidas con `jobs.estimate_cost`, sin subir archivos ni crear trabajos. No son precios universales: volver a cotizar con los parámetros exactos antes de generar.

| Operación | Configuración cotizada | Créditos |
|---|---|---:|
| Tripo Text to 3D | Textura y geometría estándar | 5 |
| Hunyuan 3D v3.1 Text to 3D | Standard, sin PBR | 7 |
| Hunyuan 3D v3.1 Text to 3D | Pro y PBR | 16 |
| Tripo H3.1 Image to 3D | Texturizado, PBR, calidad estándar | 9 |
| Tripo H3.1 Multiview to 3D | Texturizado, PBR, calidad estándar | 9 |
| Hunyuan3D v3 Image to 3D | Normal, 500.000 caras, sin PBR | 11 |
| Meshy 6 Text to 3D | Estimación base | 25 |
| Meshy 7 Image to 3D | Sólo geometría, Ultra mode desactivado | 25 |
| Meshy 7 Image to 3D | Textura, sin rig ni animación, Ultra mode desactivado | 38 |
| Meshy 7 Image to 3D | Textura, rig y animación, Ultra mode desactivado | 47,5 |
| Meshy 5 Remesh | Estimación base | 6 |
| Meshy 5 Retexture | Estimación base | 9,5 |
| 3D Body | Reconstrucción de forma/pose humana desde imagen | 1 |
| Kimodo | Estimación base sin clip específico | 0,1 |

Kimodo devuelve una secuencia de movimiento NPZ; requiere adaptación al esqueleto. La cifra base 0,1 no prueba el costo ni calidad de un clip final concreto. 3D Body reconstruye forma/pose humana: no equivale a un personaje acabado con ropa y rig de producción.

También aparecen 3D Objects, 3D Rigging, Image to 3D y Multi-Image to 3D, pero sin esquema de estimación disponible. Su precio queda **sin verificar**, nunca se trata como gratuito.

No confundir el plan Ultra con el parámetro `ultra_mode` de Meshy 7: este último no se incluyó en las cotizaciones y puede cambiar el precio.

## Cómo aprovecharlo en el juego

- **Humano:** conservar y corregir la fuente existente (pijama, pantuflas, gorro, manos, ojos y locomoción). La IA de imágenes puede ayudar a explorar ropa; un modelo nuevo requiere revisar topología, articulaciones, pesos y animación.
- **Mosquito:** conservar la fuente existente. Las alas, patas, ojos y física necesitan un rig adecuado al insecto; no dar por válido un autorig humano.
- **Mapas:** construir living, casa, patio y lobby mediante módulos y scripts de Blender/Unity. Generar sólo objetos distintivos que falten. Una escena de 3D Jutsu no añade automáticamente colisiones, navegación ni lógica del nivel.
- **Elementos:** priorizar Tripo/Hunyuan como candidatos económicos después de evaluar una muestra; reservar Meshy para casos donde la calidad lo justifique. Calidad real todavía no comparada mediante generación.
- **Materiales:** Retexture puede ayudar, pero revisar UV, mapas y el resultado en Unity. Una imagen de textura no equivale automáticamente a un material PBR sin costuras.
- **Animación:** considerar Kimodo y rigging como auxiliares; validar contacto de pies, deformaciones, bucles y retargeting. Videos de Seedance/Kling no son archivos de animación de huesos.
- **UI y promoción:** modelos de imagen para iconos e ilustraciones; video/audio para tráiler o presentación cuando hagan falta. Acceso al modelo no significa uso gratuito. No asignar créditos a promoción mientras falten recursos esenciales.
- **Supercomputer:** además de las generaciones, los pedidos de texto pueden consumir créditos según el LLM y la complejidad. Para planificar y escribir scripts usaremos esta tarea de Codex y Blender local, conservando créditos de Higgsfield para operaciones generativas justificadas.

## Política de gasto propuesta

La preparación técnica y la auditoría están autorizadas; no se han encargado nuevos assets. Director confirmó que los defectos actuales del alfa se reparan primero en las fuentes existentes. El siguiente presupuesto es una propuesta de máximos para producción futura, no una orden de consumirlo.

| Destino | Máximo propuesto |
|---|---:|
| Personajes y sus piezas especiales | 900 |
| Muebles y otros elementos | 750 |
| Objetos distintivos de escenarios | 450 |
| Pruebas y correcciones generativas | 300 |
| Reserva sin comprometer | 600 |
| Total | 3.000 |

1. Mantener una lista priorizada de recursos faltantes y sus fuentes existentes.
2. Diseñar antes de generar 3D. Aprovechar referencias ya disponibles y beneficios web dentro de sus condiciones.
3. Cotizar parámetros exactos; trabajar de a una variante. Primera prueba de un proveedor: dentro de un límite de 100 créditos, incluido en el apartado de pruebas.
4. Tras dos intentos fallidos de un mismo recurso, diagnosticar y corregir el enfoque; no encadenar regeneraciones automáticas.
5. Construir variantes mediante geometría, materiales y accesorios en Blender; reutilizar mallas y rigs.
6. Registrar costo estimado, costo real, recurso, modelo y archivo resultante. Comparar saldo antes/después de cada lote.
7. No activar recargas, adquirir extras ni cambiar el plan como parte de la producción.
8. Para el alfa actual, mantener el gasto en cero hasta que el Director identifique una necesidad que no resuelvan las fuentes propias.

## Fuentes

- Cuenta autenticada: https://higgsfield.ai/me/settings/subscription
- Precios y FAQ: https://higgsfield.ai/pricing
- SDK del complemento oficial: catálogo 3D y estimador consultados en vivo.
- Complemento: https://higgsfield.ai/plugins/blender
- Créditos: https://higgsfield.ai/creator-hub/help-center/credits/how-credits-work
- MCP: https://higgsfield.ai/creator-hub/help-center/integrations/what-is-higgsfield-mcp
- 3D Jutsu: https://higgsfield.ai/blog/higgsfield-3d-jutsu
- Importación Unity: https://docs.unity3d.com/6000.0/Documentation/Manual/3D-formats.html
- Uso comercial: https://higgsfield.ai/terms-of-use-agreement (sección 4.4; revisar aparte licencias de recursos de terceros).
