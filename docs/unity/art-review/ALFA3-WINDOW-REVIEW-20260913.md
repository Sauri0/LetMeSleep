# Alfa.3 — observación del ejecutable en el monitor principal

Build revisada: `N:/LetMeSleep/Artifacts/alfa-20260913-000715`, fuente `e9d15e7e23f30e32c5dff9930b145e4eecb570bb`. Sigue siendo candidata, no cierre de alfa ni autorización para beta.

## Captura y alcance

Evidencia en `N:/LetMeSleep/Validation/alfa3-window-review/`. FFmpeg Windows Graphics Capture capturó sólo la ventana del player, sin otras ventanas superpuestas. `menu-wgc.mp4` pertenece al monitor vertical: conservar como diagnóstico del encuadre incorrecto, no usar para evaluar el objetivo visual. El usuario indicó que todas las pruebas deben ir al principal.

La segunda invocación agregó `-monitor 1`; `menu-primary.mp4` contiene 24 segundos horizontales. El usuario navegó durante la grabación: aproximadamente 0–15.5 s son Ajustes, 15.5–19.5 s menú principal, 19.5–24 s selección de entrenamiento. No es una toma continua de dos ciclos del menú ni evidencia de gameplay.

El revisor de animaciones inspeccionó fotogramas ordenados y un detalle a 15 Hz: alas alternan posiciones elevadas/abiertas, cambia orientación del mosquito, el humano gira cabeza y mueve brazo/matamoscas y torso. Refuta que los modelos sigan completamente estáticos. No demuestra todavía fluidez percibida a velocidad normal, seguimiento preciso de pupilas, contacto causal del golpe ni aprobación artística. Informe independiente: `N:/LetMeSleep/Validation/TeamRecovery/animation/ALFA3-PRIMARY-MOVEMENT-REVIEW.md`.

## Audio y salida observados

Dos instancias, PID36992 y PID8040, tuvieron diez muestras CoreAudio cada una con sesión Active, sin mute y picos mayores que cero. Las instantáneas posteriores muestran procesos, ventanas y sesiones de audio vacíos. Recibos `audio-before/after.json` y `primary-audio-before/after.json`; logs `player.log` y `player-primary.log` contienen secuencia de apagado del Input System.

Director no solicitó el cierre de esas instancias. La observación acredita ausencia de residuos en esos instantes; no acredita la ruta automatizada del botón Salir, todos los caminos de salida ni calidad auditiva de la mezcla. Las capturas de video no contienen audio.

## Defectos nuevos y responsables

1. **Cámara del mosquito demasiado próxima al cuerpo junto a pared.** Observación de ventana durante entrenamiento: abdomen ocupa gran parte de la pantalla. Presentación confirmó que la protección contra paredes puede reducir distancia hasta cero y no hay tratamiento de intersección con la malla propia. Se autorizó corregir orden de evaluación, zoom solicitado y oclusión local preservando colisiones del mundo. Falta integrar y verificar con geometría real.
2. **Picadura: `LMS_BITE_VISUAL_OFFSET` de 0.1530 y 0.1727 m.** El log mide corrección previa a alinear, no error residual. Gameplay identificó representación orientada con yaw libre durante adhesión y giro posterior de Head por atención visual. Se autorizó corregir orientación/contacto sin modificar autoridad ni relajar umbral, conservando pupilas y parpadeo.
3. **Resolución persistida como índice de catálogo.** UI confirmó riesgo de restaurar dimensiones distintas cuando cambia el monitor/catálogo. Se delegó guardar dimensiones estables con migración de preferencias existentes y evitar aplicar de nuevo resolución al cambiar sólo audio. No atribuir toda la selección del monitor vertical a este defecto sin prueba.
4. **Advertencia URP al volver al menú.** `Light.shadowResolution` sólo funciona en Built-In; Presentación corregirá la configuración compatible sin cambiar deliberadamente la estética de la luz.

No publicar esta candidata por el solo hecho de que el paquete y el smoke anterior sean válidos. Integrar y probar los defectos nuevos. Instalación pública, prueba entre redes independientes con el amigo y aceptación visual siguen pendientes.
