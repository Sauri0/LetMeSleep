# Native6: humano corregido, mosquito rechazado

Alcance exclusivo: encuadre de visor tras 9359f1f, integrado por Director como 69e8394. Evidencia producida por Director en `N:/LetMeSleep/Validation/TeamRecovery/ui-native6`. Sin nativos propios ni cambios de runtime durante esta revisión.

Se revisaron los 28 registros de proyección de ambos receipts, hashes de las 28 imágenes y 11 PNG representativos: humano default1080, side720, back1080, near720, far1080; mosquito default1080, front720, side720, back1080, near720, far1080. Los hashes confirman humano default=front=reset y mosquito default=reset para cada resolución.

## Humano

Corregido el defecto de cabeza fuera en vista centrada. Cabe completo en default/frente/perfil/espalda/reset; far aleja como corresponde. 16 295 vértices comprobados por estado, fuera=0 en los estados de cuerpo completo a ambas resoluciones. Default ocupa aproximadamente y=0.047–0.922 del viewport; cámara y=0.922, frente al y=0.054 erróneo de native5. Zoom cercano recorta de forma deliberada: 3692 vértices fuera, no se usa para aprobar cuerpo completo.

## Mosquito: discrepancia bloqueante

Los receipts dicen fuera=0 para 4142 vértices en todos los estados de cuerpo completo. Sin embargo, los PNG contradicen esa conclusión:

- Default1080 corta alas por arriba/derecha, probóscide a izquierda y patas por abajo.
- Frente720 corta alas y patas.
- Perfil720 corta probóscide, alas, abdomen y patas.
- Espalda1080 corta alas y patas.
- Far1080 sí muestra la silueta completa. Near720 está más recortado, como corresponde al zoom.

No cerrar framing del mosquito por el resultado numérico. El chequeo de CapturePreviewBounds6.cs usa BakeMesh(false)+TransformPoint, el mismo camino geométrico que el runtime, por lo que no es independiente de un posible error de escala de esa conversión.

Pista pendiente de probe nativo: el prefab mosquito tiene VisualRoot.localScale=(0.5,0.5,0.5); el tamaño aparente difiere aproximadamente por factor dos. Comparar BakeMesh(false), BakeMesh(true), lossyScale/rootBone y bounds renderizados antes de decidir corrección. Esto es hipótesis, no causa probada. No alterar escala de modelos ni multiplicar distancia por dos como parche.

Director notificado de la discrepancia con nombres exactos. Arte, iluminación final, input físico, persistencia, giro continuo y otras poses quedan fuera de esta revisión.
