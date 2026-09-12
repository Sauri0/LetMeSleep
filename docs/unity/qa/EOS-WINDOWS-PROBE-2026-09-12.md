# Estado del probe EOS Windows — 2026-09-12

Dictamen: **evidencia parcial; U094-12 y U094-13 permanecen BLOCK**.

Entorno informado: Unity `6000.3.24f1`, player Windows x64 de desarrollo. Los logs y recibos con material sensible permanecen fuera del repositorio. Este documento no contiene PUID, DeviceID, código de sala, IP, puerto, token ni credencial.

| Observación | Resultado | Alcance acreditado |
|---|---|---|
| Player inicial, con y sin gráficos | Crash nativo previo al juego en `GfxPluginNativeRender-x64.UnityPluginLoad`. | Defecto de empaquetado reproducido; no acredita lógica de juego ni EOS. |
| Diagnóstico retirando el helper gráfico de overlay no usado | El host arrancó; autenticación EOS y callbacks reales de crear/destruir lobby completaron. | `host_lifecycle` parcial. No acredita que la política automática produzca el mismo artefacto. |
| Guest con cache separado bajo la misma cuenta del sistema | `SameDeviceIdentity`; `networkType=NotEstablished`; cero mensajes; `wanVerified=false`. | El negativo de identidad duplicada fue detectado correctamente. No es un par EOS local. |
| `NativePluginPolicy` con exclusión mediante `PluginImporter.SetIncludeInBuildDelegate` | Rebuild limpio `eos-probe-clean`: `Succeeded:0`; helper ausente; player inició y el host autenticó/creó lobby. | PASS sólo de empaquetado, arranque y ciclo host. El build partió de `814265c` con fuentes Online sin commit; no acredita una fuente íntegramente reproducible, transporte ni WAN. |

El recibo sanitizado del guest corresponde al run `probe-20260912-3` y permanece en el área privada de evidencia. Su resultado y contadores coinciden con esta clasificación.

## Para avanzar

1. Repetir el build limpio desde un commit sin cambios locales y guardar commit, SHA-256 del ejecutable y manifiesto. La auditoría de la corrida actual está en `BUILD-TRACEABILITY-REVIEW-2026-09-12.md`.
2. Usar una segunda identidad EOS real en otro perfil/equipo; no borrar ni regenerar DeviceID para fabricar el resultado.
3. Completar create/join, callback P2P, intercambio cruzado y cierre. Clasificarlo `local_eos_pair=true`, `wan=false` salvo que también use dos redes físicas.
4. Ejecutar el protocolo WAN con dos equipos/redes, dos rondas y ruta `Direct` o `Relay` informada por EOS. Hasta entonces no afirmar relay ni WAN.
