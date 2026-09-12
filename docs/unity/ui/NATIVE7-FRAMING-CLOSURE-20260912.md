# Native7: cierre puntual de regresión de encuadre

Evidencia del Director tras reiniciar Editor 32520, HEAD comunicado 3dce610 con integración 34d5dc0 del fix UI 43944c1. Carpeta: `N:/LetMeSleep/Validation/TeamRecovery/ui-native7`.

Resultado: cerrar únicamente la regresión de framing de ambos personajes en las vistas muestreadas a 1280×720 y 1920×1080. Sin más cambios de runtime.

## Evidencia revisada

- Los dos receipts contienen 28 comprobaciones de proyección con BakeMesh(true): 24 estados de cuerpo completo con fuera=0. Cada humano proyecta 16 295 vértices y cada mosquito 4142.
- Se abrieron las 14 imágenes distintas que cubren default/frente/perfil/espalda/reset de ambas especies y resoluciones. Hashes verifican humano default=front=reset y mosquito default=reset a cada resolución, cubriendo las otras seis imágenes equivalentes sin depender solo del receipt.
- Humano: gorro, cabeza, manos y pantuflas dentro del visor; el cambio de perfil/espalda conserva encuadre completo.
- Mosquito: alas, probóscide, abdomen y extremos de patas dentro del visor en default de tres cuartos, frente, perfil, espalda y reset. Los PNG ahora concuerdan con la proyección; desaparece el recorte de native6.
- Zoom far: cuatro registros con fuera=0, incluidos en los 24 anteriores. Zoom near: 3716 vértices humanos y 918 de mosquito fuera, recorte deliberado excluido del criterio full-body. Esta revisión no añade aprobación visual de los ocho PNG de zoom, que no se abrieron.

## Límites

Cierre de regresión estática de encuadre para estos modelos y poses. No aprueba input físico, arrastre continuo, persistencia, animaciones completas, arte, iluminación/calidad global, rendimiento ni online. La producción nativa fue del Director; Worker UI hizo lectura de capturas/receipts y documentación. Native5 y native6 permanecen rechazados en sus informes históricos.
