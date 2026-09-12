# Auditoría EOS acotada — 2026-09-12

## Alcance y dictamen

Base revisada: `c888d96adc54c005eb241e7a48c00eb4d48850cb`, rama `codex/unity-online-specialist`.

Esta entrega revisa el flujo crear/código/unir/estado/cierre del dueño y compila el módulo Online completo con `DEVELOPMENT_BUILD`. La verificación fue externa y sólo CPU: no abrió Unity, no cargó el SDK nativo, no inició conexiones ni modifica el portal de Epic.

U094-12 continúa **BLOCK** hasta ejecutar el mismo candidato con dos identidades EOS reales distintas. U094-13 continúa **BLOCK** hasta la prueba posterior entre dos equipos y dos redes físicas diferentes. La evidencia parcial anterior acredita autenticación y create/destroy del host, además del rechazo `SameDeviceIdentity`; no acredita un par local ni WAN.

## Hallazgos y correcciones

| Flujo | Hallazgo | Resultado de esta entrega |
|---|---|---|
| Crear | El host fija protocolo, capacidad 16, join por ID, RTC apagado y migración deshabilitada. Un callback tardío exitoso destruye el lobby creado. | Sin cambio. Revisión estática. |
| Código | Normaliza espacios/guiones, exige diez caracteres del alfabeto acotado y nunca contiene IP, puerto, PUID ni credenciales. | Sin cambio. Revisión estática. |
| Unir | Se llamaba `JoinLobbyById` antes de validar protocolo, capacidad, dueño y configuración. Un candidato incompatible podía adquirir membresía antes del rechazo posterior. | Corregido: `CreateLobbySearch` + `SetLobbyId` + `Find` + `CopySearchResultByIndex`; se valida el `LobbyDetails` antes de `JoinLobby`. |
| Errores de código | Código malformado/sobredimensionado, inexistente o vencido, sala propia, protocolo distinto, sala llena y configuración insegura necesitaban salidas distinguibles y reintentables. | Quedan como `InvalidCode`, `LobbyNotFound`/`Search_*`, `SameDeviceIdentity`, `IncompatibleVersion`, `LobbyFull` y `UnsafeLobbyConfiguration`. No existe fallback LAN. |
| Cancelación | Create/search/join usa una generación. Los callbacks viejos no cambian el intento actual; un join tardío exitoso abandona la membresía. | Conservado y extendido al nuevo search/join; cada handle se libera. Falta ejecución con EOS real. |
| Estado de ronda | El cliente llegaba a enviar a presentación un snapshot bien decodificado pero perteneciente a otro epoch/round. | Corregido: se descarta antes de tocar timeout o réplica. |
| Recibo del probe | Un callback de cierre reemplazaba `networkType`, por lo que podía borrar la ruta que EOS había informado. | Corregido: conserva la ruta establecida y registra el motivo de cierre aparte. El recibo actual aún no satisface por sí solo el esquema WAN completo. |
| Cierre | Invitado usa leave; dueño usa destroy; salir el dueño se interpreta como sala cerrada y no hay migración. | Revisión estática coherente. Falta confirmación cruzada con dos identidades. |

## Evidencia CPU reproducible

Ejecutar desde la raíz del worktree:

```powershell
& 'docs/unity/online/validation/Run-Validation.ps1'
```

El script crea un directorio de evidencia nuevo en `N:/LetMeSleep/Validation`, compila todos los `.cs` de Online con `DEVELOPMENT_BUILD`, ejecuta diez casos de la política pre-join y guarda hashes de fuente. El resultado esperado es:

```text
Compilación correcta.
0 Advertencia(s)
0 Errores
ONLINE_POLICY_CHECKS checks=10 failures=0 native_sdk_loaded=false
```

Esto acredita compilación y decisiones puras de política. No acredita Unity Test Runner, callbacks reales, transporte, dos identidades ni WAN.

## Plan ejecutable con dos identidades

### Etapa A — equipo disponible ahora

El equipo actual ya demostró que dos procesos y dos cachés bajo la misma identidad del sistema producen `SameDeviceIdentity`. Esa configuración sirve sólo como negativo y no debe repetirse borrando o regenerando DeviceID para fabricar dos usuarios.

Con un único equipo/DeviceID disponible no existe una corrida positiva válida. Para destrabar U094-12 se necesita un segundo equipo Windows con otra identidad EOS persistente. Ambos deben usar un único build limpio del mismo commit, versión y protocolo. Dirección reserva el turno de Unity/build y entrega EXE/ZIP con SHA-256; los procesos se ejecutan con audio deshabilitado cuando el launcher de QA lo permita.

### Etapa B — par EOS local, todavía sin WAN

1. Preparar un `run_id` aleatorio y un salt exclusivo fuera del repositorio. Confirmar que EXE/ZIP, commit, versión `0.9.4-alfa` y protocolo coinciden en ambos extremos.
2. Arrancar host y guest desde proceso cerrado, con perfiles/cachés persistentes separados. Registrar únicamente hashes salados; los `local_hash` deben ser distintos y cruzar con el remoto del otro extremo.
3. Host crea sala. Guest pega el código con espacios exteriores, encuentra el lobby, valida detalles, entra y obtiene callback P2P con ruta EOS literal.
4. Ejecutar los negativos: código vacío, alterado, sobredimensionado, vencido, propio y de protocolo diferente; cancelar durante search/join. Cada intento debe liberar recursos y permitir reintento.
5. Verificar remitente autenticado: el guest intenta cambiar reglas y enviar estado de host; el host rechaza ambos.
6. Jugar dos rondas, volver al mismo lobby sin estado heredado, hacer salir al guest y comprobar que el host conserva la sala.
7. Reconectar guest; cerrar desde host en lobby y durante una ronda. Guest vuelve al menú, no se convierte en host y ambos procesos terminan con código 0.
8. Emitir dos recibos sanitizados con el esquema de `EOS-WAN-PROTOCOL-0.9.4-ALFA.md`, marcando `local_eos_pair=true` y `wan=false`.

### Etapa C — WAN posterior entre casas

Repetir la Etapa B sin cambiar el build, con dos personas/equipos y dos redes físicas distintas. Se requieren dos rondas concordantes, hashes cruzados de build/identidad/lobby/ventana UTC, ruta informada por EOS, salida de guest, cierre del dueño y conformidad breve de equipo/red diferentes. Sólo entonces los recibos pueden marcar `wan=true` y U094-13 puede evaluarse como PASS.

Nunca guardar en recibos o repositorio el código reutilizable, PUID/DeviceID, IP, puerto, nombre de red, token, `ClientId`, `ClientSecret`, rutas de perfil o variables EOS.

## Pendientes reales

- Integrar en el probe el recibo completo de build, hashes salados, dos rondas, cierres y attestations; el JSON actual es sólo diagnóstico de conectividad.
- Ejecutar create/find/join/P2P/leave/destroy con dos identidades y capturar ambos extremos.
- Ejecutar la corrida WAN entre casas una vez que exista un candidato limpio y estable.
- Mantener publicación, pagos y configuración comercial de Epic/Steam fuera de este frente hasta que el juego esté terminado y el usuario decida plataforma.
