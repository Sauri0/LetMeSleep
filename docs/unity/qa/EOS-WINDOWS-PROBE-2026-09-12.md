# Estado del probe EOS Windows — 2026-09-12

Dictamen: **evidencia parcial; U094-12 y U094-13 permanecen BLOCK**.

Entorno informado: Unity `6000.3.24f1`, player Windows x64 de desarrollo. Los logs y recibos con material sensible permanecen fuera del repositorio. Este documento no contiene PUID, DeviceID, código de sala, IP, puerto, token ni credencial.

| Observación | Resultado | Alcance acreditado |
|---|---|---|
| Player inicial, con y sin gráficos | Crash nativo previo al juego en `GfxPluginNativeRender-x64.UnityPluginLoad`. | Defecto de empaquetado reproducido; no acredita lógica de juego ni EOS. |
| Diagnóstico retirando el helper gráfico de overlay no usado | El host arrancó; autenticación EOS y callbacks reales de crear/destruir lobby completaron. | `host_lifecycle` parcial. No acredita que la política automática produzca el mismo artefacto. |
| Guest con cache separado bajo la misma cuenta del sistema | `SameDeviceIdentity`; `networkType=NotEstablished`; cero mensajes; `wanVerified=false`. | El negativo de identidad duplicada fue detectado correctamente. No es un par EOS local. |
| `NativePluginPolicy` con exclusión mediante `PluginImporter.SetIncludeInBuildDelegate` | Código preparado, rebuild limpio todavía pendiente. | Ningún PASS de build hasta verificar el artefacto reconstruido. |

El recibo sanitizado del guest corresponde al run `probe-20260912-3` y permanece en el área privada de evidencia. Su resultado y contadores coinciden con esta clasificación.

## Para avanzar

1. Ejecutar un rebuild limpio con `NativePluginPolicy`, confirmar que el helper no está en el artefacto y que el player inicia sin crash.
2. Repetir host create/destroy y conservar recibo ligado al hash del ejecutable reconstruido.
3. Usar una segunda identidad EOS real en otro perfil/equipo; no borrar ni regenerar DeviceID para fabricar el resultado.
4. Completar create/join, callback P2P, intercambio cruzado y cierre. Clasificarlo `local_eos_pair=true`, `wan=false` salvo que también use dos redes físicas.
5. Ejecutar el protocolo WAN con dos equipos/redes, dos rondas y ruta `Direct` o `Relay` informada por EOS. Hasta entonces no afirmar relay ni WAN.
