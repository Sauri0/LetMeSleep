# Cierre compartido de Main

Main.request_exit(exit_code=0) concentra el cierre existente: detener música,
solicitar cierre de sala, esperar 0,25 s y limpiar servidor antes de salir.
La notificación de ventana y las pruebas de práctica/perfil usan la misma ruta.
El perfil ya no necesita reescribir el quit en una copia de Main.

Práctica mosquito/sleep: 25/25, renderer nativo, Dummy, no microphone,
exit 0 y stderr vacío; verbose sin Music orphan. La semilla inicial reproduce
la casa anterior, la revancha genera otra semilla. No se reprodujeron los
16 recursos previos y no se adjudica su causa completa a este cambio.
Main real conserva salida solicitada 7 en main09-exit7.json, stderr vacío.
Las factories instrumentadas compilan con --check-view-scopes sin errores.
