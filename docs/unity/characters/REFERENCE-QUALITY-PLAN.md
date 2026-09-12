# Calidad de personaje exigida por los ocho bocetos

Requisito explícito de Branko: los modelos del catálogo fueron rechazados por insuficientes. Los gates técnicos no son aceptación del diseño. M1 abrió los ocho PNG `ref-01` a `ref-08` de `N:/LetMeSleep/References/CharacterQuality-20260912` mediante `view_image`; identidad en el manifest de esa carpeta. Se toma la forma y calidad de construcción, manteniendo el humano base en pijama/pantuflas/gorro y sin añadir variantes beta ni mecánicas de las hojas.

| Brecha visible | Delta de fuente humano | Evidencia exigida |
| --- | --- | --- |
| Cabeza ovalada grande, cuello oculto, nariz poco legible | Secciones craneofaciales con planos frontales, sien, pómulo biselado, mandíbula/mentón y nariz integrada; cabeza de unos 34×31 cm y cuello visible | Frente/perfil/espalda/tres cuartos junto a ref-04/ref-08, con el mismo tamaño aparente |
| Ojos superpuestos y orejas redondas | Ojos grandes parcialmente encastrados, borde superior de órbita, oreja con borde y concha hundida, ceja prismática | Primeros planos frente/perfil, Blink real y expresiones |
| Boca rígida | Jaw adicional conservando 64 huesos previos; mentón/labio articulados, cavidad y labio superior; Hit/Fall/Faint/Recover y cejas de Swat | Secuencia temporal y detalle de boca, sin penetración labio/cara |
| Collar como cordón; mangas/pantalones cilíndricos | Collar plegado con espesor de 5 mm y ribete fino, bolsillo con cara/espesor, costura/botones discretos, puños y bucles escogidos en codo/rodilla | Idle/Crouch/Clap/Swat, acercamiento hombro/cadera y tela en perfil |
| Palmas redondas, dedos largos, pantuflas como botas | Palma de secciones planas, dedos 15 % más cortos/gruesos; suela biselada baja y empeine con abertura, tobillo visible | Mano abierta/cerrada/agarre, palmada, pantufla perfil y apoyo durante caminar |

Se preservan Root, CameraEye a 1,53 m en bind, cápsula, Grip y los 15 IDs/nombres de animación. W2 confirmó compatibilidad de Jaw/65 huesos mientras sigan CharacterView, HeadRenderers y anclajes. El nuevo helper `author_human_geometry.py` contiene geometría propia. No se ha ejecutado Blender ni se han actualizado `.blend`/FBX para esta revisión mientras Director ocupa el editor PID 23792.

## Mosquito candidato silhouette7

M1 abrió Mosquito_Idle_35/90 de `N:/LetMeSleep/Validation/ArtCatalog/characters-silhouette7`: patas y articulaciones separadas, abdomen afinado, probóscide roja y facetas de ala son mejoras visibles. Aún no cumple el diseño de los bocetos: cabeza/órbitas demasiado esféricas, postura horizontal, patas cortas respecto del abdomen y trompa poco descendente. No está aprobado.

El contrato Mouth.Y local=0, con Root a 0,057 m del soporte, mantiene la punta 57 mm sobre ese plano. Algunas referencias la extienden cerca de las patas. Esa diferencia se debe exponer y coordinar con Director/W1 antes de cambiar el anclaje; no se ocultará desplazando la cámara ni el runtime. Se terminará primero el candidato humano y luego se revisará el siguiente delta de mosquito contra evidencia.

## Orden de validación

1. Al recibir turno, generar fuentes; medir huesos/anclajes, Jaw/dedos/Blink, deformaciones, piso/loops, fidelidad FBX y apoyos de mosquito conservado. Corregir fallos antes de sellar.
2. Importar en Unity y ejecutar builder/idempotencia en el editor del Director.
3. Capturar frente, perfil, espalda, tres cuartos y movimiento real. Comprobar apertura de pantufla, collar y planos faciales con detalle suficiente. Para alas, fondo con bandas/cuadrícula que permita percibir transmisión.
4. Revisar comparativamente con los ocho bocetos. Mantener diferencias pendientes visibles; no etiquetar calidad artística como PASS porque los números pasen.
