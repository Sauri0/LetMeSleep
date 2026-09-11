# Entregas 0.9.1 y 0.9.2

Objetivo explícito vigente: lanzar 0.9.1 y luego 0.9.2 con todas las correcciones. Ninguna entrega se declara lista sin su paquete probado. Este reparto ordena entregas, no elimina pendientes.

## 0.9.1 — correcciones base verificadas

- Superficies: techo, uniones, esquinas, dinteles y materiales de suelo.
- Casa v2: circulación real alrededor de escaleras, rellanos y dimensiones, presupuesto de iluminación por tramo.
- Movimiento: giro al mirar abajo + continuidad de marcha/presentación, preservando respuesta y defensa.
- Personalización: selección reconocible, navegación, giro/zoom y comparación de variantes existentes.
- Regresiones de ambos roles, práctica, puertas, ataques, tareas y conexiones. App0.9.1/protocolo10; no mezclar clientes con rc1.
- Revisión visual y medición documentada; build Windows nuevo, código/ZIP en GitHub, rc1 preservado.

## 0.9.2 — completar calidad y pendientes

- Distribución doméstica por uso/planta/vecindad, servicios de tamaño razonable y equipamiento esencial; amueblado interior lógico sin bloquear rutas.
- Modelados/rasgos de ambas especies diferenciados, perfiles, ropa/accesorios y expresiones sin interpenetraciones; revisar patrón de pijama en cuello.
- Acabado de entorno/materiales y luces sobre la casa final, sombras/rendimiento con escenas comparables.
- Corregir los hallazgos restantes de QA, incluyendo movimiento/contacto si la primera fase detecta problemas no resueltos; no trasladar defectos que vuelvan injugable la0.9.1.
- Repetir checks afectados sobre el conjunto, exportar/probar/publicar paquete0.9.2 con manifiesto y alcance real.

Los implementadores pueden preparar ambos tramos en paralelo, pero Director integra versiones en orden. Geometría distinta requiere identidad de generador distinta; no reutilizar house-v2 para otra geometría tras publicar0.9.1. No exportar dos revisiones distintas con la misma versión.

WAN real entre casas sigue pendiente por decisión de Branko hasta después del pulido. Las pruebas locales o de creación de lobby no se reportarán como certificación WAN. FPS por defecto ilimitados;1080p60 en1660Ti es objetivo de rendimiento, no medición ya conseguida.

## Recuperación del reinicio del 11 de septiembre

Se verificó ausencia de procesosGodot/Blender/juego. Los índices Git de worktreesUI yQA estaban corruptos; se respaldaron junto a sus índices y se reconstruyeron con read-treeHEAD, conservando workingfiles. gitfsck--connectivity-only no detectó errores. Director integró commitsQA c67771e y0572155 después de verificar los hashes locales; dos intentosimportWorker1 habían fallado antes de llegar a pruebas y sus resultados no cuentan como éxito. Nuevos logs llevarán nombres distintos.
## Observación nueva: flexión de dedos

Branko reportó dedos que parecen doblarse hacia el dorso desde primera y tercera
persona. Worker1 investiga la pose y ejes runtime; Modelador2 la anatomía, rig y
pesos de los modelos. Revisar ambas manos de frente, palma, dorso y perfil en
reposo, cierre, palmada y agarre. La corrección debe cerrar hacia la palma y
mantener oposición del pulgar, sin esconder el defecto con la cámara. Se suma
al cierre de calidad de modelos/animaciones de0.9.2; cualquier arreglo runtime
pequeño y validado puede incorporarse antes de publicar0.9.1 sin mezclar las
mallas faciales aún en desarrollo.
