# Receta de producción de personajes v0.2.0

Fecha: 2026-09-20. Este documento prepara el siguiente turno de producción;
no aprueba arte, importa assets ni inicia trabajos pagados. Las referencias que
mandan son PER-06, PER-07 y PER-08 de `DIRECCION-VISUAL.md`.

## Recuperación y contraste observado

| Paquete | Hecho verificado | Reutilizar | No reutilizar / no cumple |
|---|---|---|---|
| HUM-3D-005 | Meshy v7 Ultra: `char1` único, 27.170 triángulos, 24 huesos, 0 vértices sin peso y un clip Walk. Hay renders frente/dorso/3/4. | Patrón GLB/FBX y evidencia de rig ponderado. | La entrada fue `HUM-REF-001_seedream_v5_pro.png`, derivada de un boceto. Malla única, un material y 24 huesos sin cara/dedos: no es modular ni fuente artística siguiente. |
| HUM-3D-001/004 | Original Tripo y remesh Meshy 5 preservados. 004 siguió triangulado, sin rig y ~1,998 m pese a pedir 1,7 m. | Trazabilidad de fallos. | Apariencia, geometría y una nueva operación de remesh. |
| Mosquito LMS06 alfa | Tiene core, alas, ojos, cejas, boca, accesorios y rig `Root/Head/Proboscis/Wing.*`, patas y sockets. | Nombres, orden de partición, sockets y clips como fixture técnico. | Mallas, texturas, colores y silueta: es arte alfa anterior, no un paquete Higgsfield nuevo. |

Los renders observados de HUM-3D-005 conservan pijama, pantuflas, gorro rojo,
facetas y ojos grandes. Frente a PER-06/PER-08, la cabeza es más estrecha, ojos y
mandíbula tienen otra lectura y faltan piezas intercambiables. Las tres vistas
existen, pero no prueban clips, separación de ropa/cara ni catálogo. Es una
comparación, no aceptación ni declaración de que sea idéntico.

## Inventario y duplicados

Ver inventario con rutas y hashes en
`N:/LetMeSleep/Validation/Higgsfield/Personajes/v020-production-recipe/INVENTORY.json`.

* HUM-3D-005 ya fue enviado por 53,5 créditos cotizados: existe su recibo y no
  se reenvía. Su recibo no conserva ID backend; toda solicitud nueva debe hacerlo.
* HUM-3D-001/002/003/004 son antecedentes, incluidos rechazos; no son saldo ni
  órdenes presentes.
* No se localizó en `Validation/Higgsfield` un recibo Higgsfield nuevo de
  mosquito. Lo encontrado es LMS06 alfa y exportaciones: sólo fixture técnico.

## Fuentes exactas

| Clave | Archivo y SHA-256 | Región fuente sin redibujar | Destino |
|---|---|---|---|
| PER-06 | `C:/Users/brank/Desktop/bocetos/personajes/ChatGPT Image 12 sept 2026, 05_21_31 a.m. (1).png` — `0A55776231EC35BBE5CD7B4C370812D85AE18CCBFEE2EB9012EFA06494FEE975` | 1448x1086; humano central `x=580..870, y=76..741`. | Base humana y paneles de piezas. |
| PER-07 | `C:/Users/brank/Desktop/bocetos/personajes/ChatGPT Image 12 sept 2026, 05_21_31 a.m. (2).png` — `62CDE0CF78B3853E18E65172368E19B524E8925A2BFA2B662E7A9004F179F142` | 1448x1086; mosquito central `x=543..908, y=100..537`. | Cuerpo mosquito y piezas. |
| PER-08 | `C:/Users/brank/Desktop/bocetos/personajes/ChatGPT Image 12 sept 2026, 05_39_07 a.m. (4).png` — `80C921E7BF7DBCEF1EEF7BFF785851F86B1F188B98034BFEF8A7D75C23E68CCE` | 1672x941; humano `x=412..834, y=135..708`; mosquito `x=1022..1455, y=153..704`. | Revisión de personalización, no board entero para image-to-3D. |

PER-06/PER-08 fijan una única silueta humana completa canónica. C01 exige tres
bases compatibles, pero no define dos cuerpos adicionales. HUM-V020-B02 y B03
quedan listos pero bloqueados antes de cotizar: requieren un crop o boceto de
cuerpo entero que fije sus diferencias. Inventar proporciones en prompt rompería
la fidelidad solicitada. Esto no bloquea B01 ni el sistema modular.

## Tickets mínimos

Todos son para el próximo slot Blender asignado por CEO. Los outputs se guardan
bajo `Validation/Higgsfield/Personajes/v020-production-recipe/<ticket>/`; no
escriben `Assets`, fuentes Blender existentes ni arte alfa.

| Ticket | Proveedor y fuente | Entregable / montaje | Revisión antes de continuar |
|---|---|---|---|
| HUM-V020-B01 | SDK Higgsfield existente, `meshy_v7_image_to_3d`; crop sin redibujar de PER-06 central, con SHA del crop y prompt registrados; pose T, rig 1,7 m, texture/PBR/remesh activos. | GLB/FBX/blend bruto preservado; separar piezas sólo después. Nuevo contrato humano, no arte alfa. | Frente/dorso/3/4 vs PER-06; 0 sin peso; pies/origen/escala medidos; cabello, ojos, gorro, camiseta, pantalón y pantuflas separables. Rechazar como modular si queda malla única. |
| HUM-V020-B02 / B03 | Mismo SDK y parámetros, cada uno con crop+SHA de un boceto completo propio. | Dos bases independientes con contrato de B01. | Bloqueados por fuente visual faltante; no derivar de HUM-3D-005 ni deformar B01 para fingir variedad. |
| HUM-V020-MOD-H | SDK por pieza aislada desde paneles PER-06, crop+SHA por job. No enviar una hoja completa: HUM-3D-003 no produjo módulos. | Biblioteca sobre B01: cara, cabello, vello, prendas, sombrero, gafas y calzado; mesh por slot y color por parte. | La pieza se activa sola sin hueco/intersección/escala incorrecta; frente/perfil/espalda y clip A34. |
| MOS-V020-B01 | SDK `meshy_v7_image_to_3d`; crop sin redibujar del mosquito central PER-07. Cotizar de nuevo: no hay recibo nuevo reutilizable. | Bruto preservado; rig nuevo que conserva nombres/sockets LMS06, sin reutilizar su arte. | Silueta, ojos separados, probóscide, abdomen segmentado, seis patas y alas vs PER-07; hover, posado, picadura y recuperación. |
| MOS-V020-MOD | SDK por pieza aislada desde PER-07: alas, expresiones, abdomen/patrones y accesorios. | Piezas y paletas sobre MOS-V020-B01, nunca mallas LMS06. | Alas/patas sin atravesar cuerpo; `Socket.Mouth` estable; cambiar pieza no mueve Root ni rompe clip. |

## Cantidades C01–C12

| Decisión | Objetivo | Lote |
|---|---:|---|
| C01 | 3 bases humanas | B01 ahora; B02/B03 bloqueadas por fuente. |
| C02 | 8 peinados incluido calvo | 7 meshes + ausencia. |
| C03 | 6 ojos, 6 cejas, 6 bocas | 18 piezas faciales. |
| C04 | 6 vellos incluido ninguno | 5 meshes + ausencia. |
| C05 | pijama + 4 conjuntos | 5 outfits; superior/inferior sólo si compatibles. |
| C06 | 6 accesorios de cabeza | gorro nocturno + 4 meshes + ausencia. |
| C07 | 4 gafas y 4 calzados | 3 gafas + ausencia; 4 pares contando pantuflas. |
| C08 | cuerpos mosquito sin cantidad fijada | MOS-V020-B01 se cotiza y revisa ahora; cuerpos adicionales esperan una cantidad explícita. |
| C09 | 4 alas y 6 expresiones | 4 pares; seis combinaciones ojos/cejas. |
| C10 | 5 patrones abdomen | cinco materiales/overlays, no cinco cuerpos. |
| C11 | 6 accesorios mosquito incluido ninguno | cinco meshes + ausencia; diseños pendientes. |
| C12 | 4 emotes comunes | saludar, señalar, reír, celebrar en ambos rigs. |

Primero cotizar y aceptar B01 humano y mosquito; después contrato modular; luego
cantidades. La falta de C08 no bloquea el primer mosquito. Las cantidades no
autorizan inventar diseños fuera de referencias.

## Herramientas nuevas: tickets sin envío

Referencias observadas: `C:/Users/brank/Desktop/bocetos/objetos/ChatGPT Image 12 sept 2026, 04_23_20 a.m. (1).png`
muestra un aerosol facetado rojo y el "bug spray" verde; `.../objetos/ChatGPT
Image 12 sept 2026, 05_21_31 a.m. (3).png` repite el bug spray dentro del panel
de herramientas. No localicé una pantufla arrojable ni raqueta eléctrica en esos
bocetos. El aerosol puede seguir sus volúmenes, color por partes y lectura
low-poly; pantufla y raqueta requieren una referencia de diseño antes de producir
arte fiel, aunque sus prefabs técnicos se pueden preparar.

| Ticket | Estado y fuente | Entregable del próximo turno | Gate de integración |
|---|---|---|---|
| TOOL-V020-SLIPPER | Sin modelo nuevo confirmado; existe `art_source/characters/tools/slipper.blend`, pero es antecedente no aprobado como arte final. | Prefab `ToolId=slipper` con malla nueva de producción, `Grip`, `Impact` y medida real; pendiente fuente artística. | Eje local `Grip→Impact` sobre `+Z`; se monta en `ToolSocket_R`; la pantufla de pie cosmética no se reutiliza como pickup arrojable. |
| TOOL-V020-RACKET | Sin modelo nuevo confirmado; `racket.blend` histórico tampoco es aceptación artística. | Prefab `ToolId=electric_racket` con malla nueva de producción, `Grip`, `Impact` y radio/cara activa medidos. | `Grip→Impact` sobre `+Z`, `ToolSocket_R`, prueba de pulso/alcance contra contrato sin inferirlo de la malla. |
| TOOL-V020-AEROSOL | No hay modelo nuevo; la referencia visual es el aerosol/bug spray de los dos bocetos de objetos. | Prefab `ToolId=aerosol` con malla nueva, boquilla `Impact` hacia `+Z`, `Grip` y volumen de nube separados. | `Grip→Impact` sobre `+Z`, `ToolSocket_R`, la nube sale de `Impact`; revisar el alcance de aerosol junto a la malla de producción. |

El único modelo de herramienta alfa confirmado disponible para reutilización
técnica es el flyswatter. Su orientación histórica no se copia: la revisión
`DELIVERY-REVIEW-TOOL-PICKUPS-FF5F290.md` registró un eje físico contradictorio.
Cada ticket nuevo debe exponer `ToolView.ToolId`, `Grip`, `Impact`, `HeadRadius`
y `GripToImpact`, y entregar captura de pickup/suelo/mano más la medición `+Z`.
No se crea generación Higgsfield ni prefab en este encargo.

## Cotización e idempotencia

1. Crear un crop del archivo y región de la tabla, sin nueva ilustración. Antes
   de cotizar, registrar ruta, dimensiones, SHA-256, PER y límites de crop.
2. Para las dos bases iniciales, usar
   `N:/LetMeSleep/Validation/Higgsfield/Personajes/v020-production-recipe/quote_v020_bases.py`.
   Sólo llama `estimate_cost`: no lanza Blender, no modifica escena, no sube una
   referencia ni tiene código de submit. Humano cotiza rig a 1,7 m; mosquito
   cotiza geometría/textura sin rig, pose, animación ni altura humana. Usar
   `Higgsfield/tools/generate_human_meshy_ultra.py` sólo como patrón de SDK:
   está fijado a HUM-REF-001/HUM-3D-005 y no debe ejecutarse sin reemplazar
   referencia, ticket y recibo.
   Si MCP local no está escuchando, la alternativa directa es
   `N:/Blender/blender.exe --background --python N:/LetMeSleep/Validation/Higgsfield/Personajes/v020-production-recipe/quote_v020_bases_direct.py`.
   Requiere el slot CEO; carga preferencias/extensión existentes sin factory
   startup, no abre/guarda fuentes y sanitiza tanto la salida como fallos.
   En background debe cargar primero el catálogo 3D cacheado; el helper registra
   una etapa y categoría sanitizadas (`required_job_type_absent`,
   `persisted_token_absent`, `workspace_absent` o `remote_estimate_failed` con
   código HTTP si existe), nunca el cuerpo de autenticación o backend.
3. Ejecutar `estimate_cost` sin `--submit`; guardar proveedor, job type, params,
   cotización y hora. Los 53,5 históricos no son presupuesto nuevo.
4. Antes de enviar, crear atómicamente recibo con `status: dispatching`, SHA de
   input, hash params y `idempotency_key`. Si existe recibo, consultar backend por
   `job_id`; no reenviar a ciegas. Confirmar `work.tasks` vacío.
5. Al responder SDK, registrar `job_id`, coste real si existe, URI y paths; sólo
   marcar imported tras preservar GLB/import bruto.
6. Hacer revisión visual/técnica de la fila del ticket. Un FAIL conserva recibo,
   original y renders; sólo PASS documentado habilita la pieza siguiente.

No se envió ni aprobó ticket nuevo. B02/B03 siguen bloqueados por sus dos
bocetos de cuerpo entero; C08 sólo limita cuerpos mosquito posteriores.

## Actualización CEO — sesión necesaria para cotizar

El intento directo de cotización confirmó el catálogo Meshy, pero la sesión
persistida de la extensión está ausente. Recibo sanitizado093714: etapa
persisted_session, categoría persisted_token_absent; no se llamó estimate_cost,
no hubo upload, submit ni importación. El CEO pidió al usuario iniciar sesión
en la extensión y continúa los frentes de código. No se reutilizarán modelos
alfa ni placeholders como arte entregable de las herramientas nuevas.
La ausencia de diseños completos para B02/B03 no bloquea preparar B01 ni el
primer cuerpo mosquito. Tampoco se interpreta una cantidad no fijada en C08
como veto a producir el cuerpo canónico de PER07.