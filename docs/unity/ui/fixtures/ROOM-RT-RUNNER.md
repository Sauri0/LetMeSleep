# Runner nativo de sala: preparado, sin ejecutar

2026-09-13. Reemplaza la ejecución del runner de entrenamiento para este ticket. El runner anterior produjo cambios en tablas de fuente y opciones del editor; **no volver a ejecutar sus scripts/DLL para validar sala**. La restauración de esos diffs antiguos corresponde a Root, con su backup y turno exclusivo. Este cambio no toca fuentes originales, ProjectSettings, Bootstrap ni Core.

## Entrada y ejecución por Root

- DLL: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/bin/HiggsfieldRoomUIBatch.dll`.
- Proyecto de compilación externo: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/Batch.csproj`.
- Config: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/config.json`.
- JSON de catálogo: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/catalog-input.json`; sólo consume `entries[].mapId/displayName`. Exige cinco IDs únicos; no carga prefabs ni geometría.
- Fuentes C#: `HiggsfieldRoomUIBatch.cs`, `RoomMapNativeFixture.cs`, `FixtureIsolation.cs`. Compilarlas juntas, sin el runner de entrenamiento que comparte el nombre requerido por el loader.
- Referencia Newtonsoft existente: `N:/LetMeSleep/Repository/unity/Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll`, con `Private=false`; Unity ya la proporciona. No requiere instalar paquetes. Config, catálogo, estado y recibos usan `JsonConvert`, ya que `JsonUtility` no pobló los DTOs privados del ensamblado cargado externamente.
- Mismo loader Root: `LetMeSleep.Content.Editor.Higgsfield.HiggsfieldExternalFixture.Run`, clase externa `HiggsfieldMapChecks.Run(configPath, outputPath)`.
- Ejecutar `Run-RoomMapBatch.ps1` sólo con turno Unity/proyecto/GPU exclusivo. Usa monitor 1, gráficos activos, sin `-quit` ni `-nographics`. Worker UI no lo ejecutó.

```powershell
dotnet build N:/LetMeSleep/Validation/Higgsfield/RoomUI/Batch.csproj -o N:/LetMeSleep/Validation/Higgsfield/RoomUI/bin --nologo -v minimal --no-restore
pwsh -NoProfile -File N:/LetMeSleep/Worktrees/ui/docs/unity/ui/fixtures/Run-RoomMapBatch.ps1
```

Usar PowerShell que disponga de `ProcessStartInfo.ArgumentList` (PowerShell 7, como el lanzador anterior). Si `powershell` resuelve a Windows PowerShell 5.1, invocar con `pwsh`.

## Qué prueba

Sala simulada con anfitrión e invitado, UI real y spy. Parte en Casa con patio; recorre los cinco IDs desde las flechas, exige que la etiqueta espere al snapshot, comprueba rechazo/ack, Ready/Start bloqueados durante cambio de reglas, invitado, fase distinta de Waiting, pending, catálogo vacío y wrap. Usa eventos de puntero sintéticos y callbacks forzados para comprobar guardas; no es prueba de mouse físico ni online.

Valida texto completo de TMP y límites de botones en cada mapa a 1280×720 y 1920×1080. Renderiza `room-720-RT.png` y `room-1080-RT.png` con el nombre más largo del catálogo. El escalado es explícito del Canvas al RT, sin afirmar comportamiento automático de GameView. La lectura humana de ambas imágenes sigue siendo necesaria.

## Aislamiento y recibos

- Copias privadas en memoria de fuentes, materiales, atlas, fallbacks y variantes de peso, con protección contra ciclos. El crecimiento dinámico ocurre en las copias. No usa `CreateAsset` ni `SaveAssets`.
- TMP Settings también se clona y su singleton se sustituye temporalmente, para que la fuente por defecto y fallbacks globales sean privados. La reflexión sobre `s_Instance` y `m_FontWeightTable` está acotada a este batch y al TMP instalado; si faltan los campos, aborta antes de crear UI. Nunca modificar los Settings originales mediante setters globales.
- Al finalizar: compara serialización de fuentes/materiales/TMP Settings originales y SHA-256 de sus archivos/atlas. Restaura el singleton y destruye sólo objetos privados no persistentes.
- Opciones de Play Mode guardadas en SessionState, restauradas en salida normal, `beforeAssemblyReload` y `quitting`. El recibo incluye valores originales/restaurados y `optionsRestored`. Una recarga inesperada marca la ejecución interrumpida y exige un batch nuevo; no pretende reanudar el fixture tras perder su ensamblado externo.
- `batch.json`: catálogo/hash/nombres, fuentes y hashes originales, resultados por resolución, flags de aislamiento y opciones.
- `post-exit.json`: el lanzador comprueba de nuevo los archivos después de que Unity haya salido, y compara el hash completo de EditorSettings anterior/posterior. También guarda `editor-settings-before.json` antes de iniciar el proceso. Si hay diffs, falla y los preserva; no los revierte ni los publica.
- Un cierre forzado/crash puede impedir callbacks. SessionState no sobrevive a otro proceso: el recibo externo y los hashes permiten a Root diagnosticar/restaurar con su backup. Un timeout deja el PID y requiere intervención, sin segundo lanzamiento automático.

## Evidencia disponible

Compilación externa: cero errores/advertencias. `CheckFixtureOptions.py --output N:/LetMeSleep/Validation/UI-RoomFixtureOptions-20260913` pasa 40 comprobaciones con dobles de API y guard real extraído: todas las combinaciones enabled/opciones, restauración anterior a reload, pérdida simulada de suscripciones estáticas, quitting e idempotencia.

**Pendiente:** ejecución Unity de sala, aislamiento nativo de fuentes, recarga real del dominio y revisión de capturas. La compilación y los dobles no certifican esos comportamientos. No se reutiliza el PASS visual anterior de entrenamiento.

## Corrección de JSON externo

El intento de Root `N:/LetMeSleep/Validation/Higgsfield/RoomUI/run-20260913-084525-ed5821fc` falló en preflight: JsonUtility dejó vacío el catálogo externo aunque el JSON tenía cinco entradas. Se reemplazó por Newtonsoft tanto la lectura Config/Catalog como la escritura de batch/receipt/Status. Se conserva la exigencia de cinco IDs únicos; no hay IDs hardcodeados.

`CheckRoomJson.py` pasa cinco pruebas offline con las declaraciones reales de DTOs privados anidados: Config con overrides/defaults, cinco entradas reales, comparación de IDs/nombres contra otro lector JSON, recibo batch completo y recibo/Status del fixture. Compilación corregida sin errores/advertencias. Reejecución nativa pendiente por Root.

## Recorte detectado en miembros y cantidad seleccionada

El intento `run-20260913-084949-54eee275` llegó a UI y falló texto completo en `Member1` a ambas resoluciones. Root confirmó aislamiento de fuentes/opciones; las capturas muestran recorte real incluso con nombres cortos del fixture. La comprobación usa `TMP_Text.isTextOverflowing` después de `ForceMeshUpdate`, no compara longitud de markup.

Corrección local: retirar el margen vertical adicional de 10+10 del texto de miembros (Fill ya reservaba 8+8), y reducir el padding horizontal de etiquetas de cantidad de 16+18 a 6+6 para que quepan «✓ 1» y «✓ AUTO». Mantiene fuentes, dimensiones de paneles y controles. La verificación completa permanece para los nombres cortos, estados y textos estáticos de este fixture; no afirma que cualquier nombre de usuario arbitrariamente largo deba caber sin elipsis. Se añaden detalles de overflow con texto parseado, rect, margen e índice TMP para diagnósticos posteriores. Compilación offline verificada; repetición nativa pendiente.

Root confirmó checks720/1080, hashes intactos y nombres/mapa completos en `run-20260913-085650-48bee7e0`. La revisión visual detectó que el glifo ✓ se representaba como un cuadrado. Se reemplazó el marcador de cantidad por ASCII `[1]` / `[AUTO]`, sin modificar fuentes ni instalar paquetes. El fixture comprueba ambos marcadores, la ausencia de marcador en números no seleccionados y texto completo también con AUTO seleccionado. Revisión nativa de este último ajuste pendiente por Root.
