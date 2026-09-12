# Registro opcional de prueba con amigos

El usuario confirmó que podrá probar con un amigo otro día. Preparar la candidata descargable y conservar la prueba entre redes distintas pendiente; no atribuirle una partida que aún no realizó.

El player de desarrollo admite `--lms-playtest-context <context.json> --lms-playtest-log <events.jsonl>`. Se juega desde los menús normales, sin automatizar roles, resultados ni conexión. Sin ambos argumentos no se activa el registro; un player no Development lo ignora. El registro no cambia la ruta de guardado, preferencias, identidad EOS ni caché.

Director prepara una vez un contexto compartido para ambos extremos, fuera del repositorio:

```powershell
$folder = 'N:/LetMeSleep/Validation/FriendsTest-01'
New-Item -ItemType Directory -Path $folder -ErrorAction Stop | Out-Null
$salt = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
@{ runId=[guid]::NewGuid().ToString('D'); saltBase64=[Convert]::ToBase64String($salt) } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'context.json') -Encoding utf8
```

Ambos usan el mismo contexto y distinto archivo de salida nuevo. No sobrescribe registros existentes (`CreateNew`). No incluir configuración EOS en ese contexto. El contexto no contiene cuentas ni código de sala, pero se conserva fuera del repositorio para limitar correlaciones.

Los eventos registran versión/buildGUID/protocolo, UTC y secuencia; identidad, código y peers pasan por HMAC-SHA256 con la clave del contexto. No se guardan nombres, PUID, DeviceID, código reutilizable, IP, puertos ni credenciales. Hay eventos de membership, fase, inicio/fin de ronda, ruta literal informada por EOS, salida solicitada y cierre del registro. Máximo4096 eventos; no captura de imágenes ni escritura por frame. Errores de archivo deshabilitan el registro y permiten seguir jugando.

El cierre `ShutdownRequested` NO acredita salida del proceso ni audio detenido. El observador externo debe registrar PID/exitcode/cierre de audio. `RoundEnded` refleja el estado recibido por esta instancia; hay que cruzarlo con la otra. Estos JSONL son evidencia auxiliar, no el recibo completo ni una certificación WAN. Faltan por separado hash del paquete/EXE/commit, confirmación de equipos/redes distintos, recorrido físico, acciones y errores negativos, dos rondas concordantes y conformidad de los participantes según `EOS-WAN-PROTOCOL-0.9.4-ALFA.md`.

Validación pendiente nativa: sin argumentos no crea archivos; contexto inválido o salida existente no altera juego; crear/cancelar/entrar/salir genera eventos sanitizados; dos rondas no se fusionan; escritura denegada no rompe cierre; salida del proceso observada por fuera. La compilación offline por sí sola no verifica estos casos.
