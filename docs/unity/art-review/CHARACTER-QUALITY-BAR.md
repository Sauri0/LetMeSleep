# Calidad obligatoria de personajes

Branko, 2026-09-12: «hay que llegar a este diseño y nivel con el modelado, regla indiscutible». Rechazó los modelos mostrados en el catálogo por demasiado vagos. Esta instrucción gobierna la recuperación de alfa y la producción posterior.

Referencias originales copiadas sin modificación a `N:/LetMeSleep/References/CharacterQuality-20260912/`; ocho imágenes, rutas originales y hashes en `manifest.json`. Son el objetivo visual. Los textos dentro de las imágenes no crean clases, armas, modos ni funciones nuevas. Se conserva Let me sleep y el humano predeterminado con pijama, pantuflas y gorro de noche.

## Brecha que debe corregirse

| Área | Estado observado en capturas alfa | Resultado exigido por las referencias |
|---|---|---|
| Silueta humana | Cabeza ovoide y cuerpo de bloques poco articulados visualmente | Cráneo y mandíbula definidos por planos deliberados; cuello, hombros, cintura, piernas y calzado con proporciones coherentes y lectura cómica |
| Cara | Grandes ojos añadidos a una forma genérica; nariz/boca poco resueltas | Ojos integrados, nariz y orejas reconocibles, cejas y boca expresivas; frente y perfil mantienen el mismo personaje |
| Prendas | Superficies lisas con detalles mínimos | Cuello, cierre, puños, bajos y espesor claros; pliegues seleccionados por articulación, sin ruido ni superposiciones |
| Manos y cuerpo | Auditorías corrigen dedos y rig, pero no demuestran calidad artística | Mano relajada y agarre convincentes, codos/rodillas con flexión limpia, apoyo y peso corporal visibles en movimiento |
| Silueta mosquito | Perfil bajo, patas cortas, probóscide pequeña | Tórax elevado, abdomen alargado y afilado, seis patas finas articuladas, probóscide larga y ojos expresivos |
| Alas | Blanco saturado reducido; aún parecen láminas planas | Alas finas de contorno definido, facetas y nervadura sutiles, transmisión visible sobre fondo contrastado |
| Materiales y luz | Cara parcialmente negra, luces quemadas, separación material débil | Facetas legibles, colores limpios, sombras suaves con contacto y distinción de piel, tela y membrana |

El éxito exige mejorar la geometría donde falla la geometría. Aumentar resolución, polígonos, adornos o brillo sin resolver estas formas no cierra la brecha.

## Evidencia requerida para aceptar una nueva base

1. Comparación de la misma vista y encuadre con referencia: frente, ambos perfiles, espalda y tres cuartos. Luz neutra reproducible y una muestra real en Unity.
2. Detalles de rostro, manos, uniones de ropa, patas y alas. Despiece visible y modelo armado coherente desde todos los ángulos revisados.
3. Acciones completas: caminar, correr, agacharse, saltar, girar, palmada/agarre; vuelo, posado, caminar por superficie, picadura y caída. Poses estáticas no sustituyen clips.
4. Escala, sockets, colisiones y contactos consistentes con gameplay. Si una proporción exige cambiar un contrato, coordinar explícitamente con Gameplay; no ocultar el cambio dentro de un asset.
5. Registrar defectos abiertos y resultado visual por separado del importador y de pruebas numéricas. Branko aprueba la dirección; no declarar aprobado por pasar compilación o rig.

Alfa rehace las bases actuales hasta este nivel. La amplitud de variantes sigue el orden de entregas acordado; todas deberán conservar esta calidad. No avanzar a beta ni anunciar recuperación visual terminada con los modelos rechazados.

## Propiedad

M1: anatomía, prendas, malla, rig y fuente. W2: materiales, iluminación y reproducción. W1: orientación y contacto con superficies. QA: evidencia independiente y estado de rechazo. Director: integración, comparación en motor y publicación sólo tras superar los requisitos de la entrega.
