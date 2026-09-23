# v0.3.0 — pasos de compilación, paquete y publicación (integrador)

Receta mecánica sobre la rama integrada. Todo en PowerShell 7 salvo que diga
bash. Un solo Unity por proyecto y como máximo tres en la máquina: adquirí un
slot en `N:/LetMeSleep/Validation/V030/locks` antes de cada Unity y liberalo al
terminar (ver las reglas de la ola). Cada corrida escribe en un directorio nuevo.

Nada se publica sin estos cuatro gates en verde, con su evidencia en `$V`:
tests (paso 2), auditoría de afirmaciones (paso 4), preflight y build de
release (pasos 5–6) y **recorrido manual del exe de release** (paso 9).

```powershell
$wt   = '<worktree integrado>'            # p. ej. N:/LetMeSleep/Worktrees/v030-integration
$proj = "$wt/unity"
$unity = 'N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe'
$V    = 'N:/LetMeSleep/Validation/V030/Release'   # evidencia de esta entrega
```

## 1. Gates sin Unity

```powershell
python "$wt/work/sync_csproj.py" "$proj"
$fw = '-p:TargetFrameworkRootPath=N:/Unity/Editors/6000.3.24f1/Editor/Data/MonoBleedingEdge/lib/mono/xbuild-frameworks/'
foreach ($p in 'LetMeSleep.Tests.EditMode','LetMeSleep.Tests.PlayMode','Assembly-CSharp-Editor') {
  dotnet msbuild "$proj/$p.csproj" -nologo -v:minimal -t:Build -clp:ErrorsOnly $fw }   # sin errores
pwsh -File "$wt/work/package-v030-selftest.ps1" -OutputRoot "$V/PackageSelftest"      # "self-test PASSED"
```

## 2. Tests (sin `-quit`)

- EditMode: `-batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`.
  Incluye `LetMeSleep.Editor.WindowsAlfaBuildReleaseCheckTests` (regla de
  ensamblados de release).
- PlayMode: `-batchmode -force-d3d11 -projectPath $proj -runTests -testPlatform PlayMode -testResults <xml> -logFile <log> --lms-validation-data <dir>/CustomizationPersistencePlayModeTests-data`.
  El último segmento de la carpeta de datos **tiene** que empezar con
  `CustomizationPersistencePlayModeTests-` y estar bajo `Validation/V020` o
  `V030`; si no, esos cuatro tests fallan en SetUp.
- Gate: exit 0, `failed=0`. Omitidos aceptados (6), y ningún otro:
  - `MosquitoRagdollPlayModeProof.R4TransfersArticulatesContactsRestoresAndCleansUp`
    (`[Ignore]`, ragdoll R4 no conectado; `docs/unity/RAGDOLL-POSE-PROTOCOL-STATUS.md`);
  - cinco pruebas de evidencia que piden un argumento propio:
    `EquipmentHudVisualEvidenceTests` (`-equipmentHudReview <dir>`),
    `ModularCustomizationUiPlayModeTests.SyntheticModularCanvas…` (`-modularCustomizationUiEvidence <dir bajo V030>`),
    `HiggsfieldMapLoadingPlayModeTests` (`-higgsfieldGameReview <config.json>`),
    `HiggsfieldWaterPlayModeTests` y `HiggsfieldGpuWaterPlayModeTests` (`-higgsfieldReview <config.json>`).
    Se cubren con una segunda corrida filtrada que pase esos argumentos
    (plantilla: `N:/LetMeSleep/Validation/V030/QA/evidence-playmode-*`).

## 3. Árbol limpio y versión

Unity reescribe al abrir o testear las fuentes TMP dinámicas
(`UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset`,
`UI/Resources/AlfaUiFonts/LMSBarlowNarrow-Bold SDF.asset`) y
`Bootstrap/Customization.renderTexture`. Además, en una Library nueva la primera
importación de los FBX de personajes **invalida** los certificados faciales de
`LMS_Human*.prefab` y `LMS_Mosquito.prefab` (los deja sin `RigRevision` ni
`SourceSha256`). Por eso:

```powershell
# a) Re-certificar y fijar versión/escenas (idempotente; con los FBX actuales reproduce los prefabs commiteados)
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.PrepareV030 -logFile "$V/prepare.log"
Select-String "$V/prepare.log" -Pattern 'LMS_FACIAL_IMPORT_BOUND|LMS_PREPARED'   # 4 BOUND + PREPARED 0.3.0
# b) Restaurar lo que Unity reescribió sin cambiar contenido (guardar copia antes si hace falta)
git -C $wt status --short
git -C $wt restore -- "unity/Assets/LetMeSleep/UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset" `
  "unity/Assets/LetMeSleep/UI/Resources/AlfaUiFonts/LMSBarlowNarrow-Bold SDF.asset" `
  unity/Assets/LetMeSleep/Bootstrap/Customization.renderTexture
git -C $wt status --short    # vacío, sin untracked: BuildCandidate aborta con cualquier cambio
```

`ProjectSettings.asset` ya está en `bundleVersion: 0.3.0` (sale en el menú como
`0.3.0 · WINDOWS`). Si `PrepareV030` deja un diff real en prefabs o settings,
revisarlo y commitearlo antes de compilar; no compilar con el árbol sucio.

## 4. Auditoría de afirmaciones (obligatoria, antes de compilar)

`BuildV030` copia `docs/player/PRUEBA-V0.3.0.md` **dentro del ZIP** como
`GUIA-DE-PRUEBA.md`, y `README.md` y `docs/v030/RELEASE-NOTES-0.3.0.md` se
publican con la release. Los tres tienen que describir sólo lo que está en el
commit que se compila. Para cada novedad, comprobá su evidencia en el HEAD que
vas a compilar y dejá el resultado en `$V/claims.md`:

```powershell
foreach ($c in 'ea68ccce','67d3d980','81e638bb','24f96cc5','c61e9da8','de59a50e','523ce9ce','227afc76','9fed0196',
               'b4019617','28b73c55','d107bff8','58ab8f04','5f693794','e500dedd','2ea670fb','ff146338','2fba601f',
               '04d2dbfb','a0278dc8','588a6cab') {
  git -C $wt merge-base --is-ancestor $c HEAD; "$c $LASTEXITCODE" }        # todos 0
```

| Afirmación (guía, README, notas) | Evidencia en el commit |
|---|---|
| Interfaz nueva según bocetos (menú, entrenamiento, jugar online con pestañas, sala, personalización, HUD, pausa, resultados, ajustes) | `ea68ccce`, `67d3d980`, `81e638bb`, `24f96cc5` |
| Personajes rediseñados (pijama, pantuflas, gorro rojo, mosquito nuevo) | `c61e9da8`, `de59a50e`, `523ce9ce` |
| Personalización rediseñada (pestañas HUMANO/MOSQUITO, girar arrastrando, FRENTE/ESPALDA/LADO, ALEATORIO, DESHACER) | `81e638bb` (la vista previa en sí ya existía en 0.2.0) |
| Mapas, menú y sala con nueva ambientación (luz cálida, halos, fuego, ventanas de noche) | `227afc76`, `9fed0196`, `b4019617`, `28b73c55` |
| Sala que se resincroniza al cortarse un enlace | `d107bff8`, `58ab8f04`, `5f693794` |
| Amigos que entran durante Resultados | `e500dedd` |
| La ronda termina bien si se va un equipo entero | `2ea670fb`, `ff146338`, `2fba601f`, `04d2dbfb` |
| Avisos de sala correctos al reconectar (sala llena, cerrada) | `a0278dc8` |
| 0.3.0 no se mezcla con 0.2.0 | `588a6cab` (protocolo `lms-unity-030-1`) |
| "Build de release" (sólo en las notas) | recibo `profile=release`, `releaseProblems=[]` (paso 6), paquete (paso 8) y `walkthrough.json` con `pass=true` (paso 9); si falta algo, borrar el punto |

**No** están en este commit y por eso los tres documentos lo dicen como
limitación, no como novedad:

- animaciones nuevas: `claude/v030-anim` no aporta commits (está en
  `046ac0a3`); las cuatro marchas del humano (`3e3c57fb`, `1aff7ca4`) ya
  estaban en la 0.2.0 (build `61f8b71a`);
- decoración nueva de los mapas: la librería de props `ed59d65f` sólo está en
  `claude/v030-maps` y son FBX en `art_source`, sin colocar en las escenas.

Si la integración suma alguno de estos (u otro cambio visible), agregá la línea
a los tres documentos **con su commit en esta tabla** y commiteá antes del paso 6.
Si alguna evidencia de la tabla no está en HEAD, borrá esa afirmación de los
tres documentos. `README.md` recién va a la rama por defecto cuando la release
exista (sus enlaces apuntan a `releases/tag/v0.3.0`).

## 5. Preflight de ensamblados de release (sin compilar el player)

```powershell
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.VerifyReleaseAssemblies `
  -logFile "$V/release-assemblies.log" --lms-release-assemblies-report "$V/release-assemblies.json"
Select-String "$V/release-assemblies.log" -Pattern '^LMS_RELEASE_ASSEMBLIES'
pwsh -File "$wt/work/package-v030-selftest.ps1" -OutputRoot "$V/PackageSelftest" -ReleaseAssembliesReport "$V/release-assemblies.json"
```

Esperado: exit 0, `problems=0 attributes=True` y
`developmentOnlyInDevelopment=Unity.Pipeline.dll,Unity.Pipeline.IlInterpreter.dll,UnityPipeline.…`
(7 nombres); el self-test con el conjunto previsto termina en `PASSED`.
`Unity.Pipeline.Attributes.dll` **tiene** que estar en el conjunto de release:
com.unity.pipeline lo deja sin restricciones a propósito y todo player Mono no
Development lo lleva; `BuildV030` y `package-v030.ps1` lo aceptan y siguen
rechazando `Unity.Pipeline.dll`, `Unity.Pipeline.IlInterpreter.dll` y
`UnityPipeline.*.dll`. Referencia de esta rama: 40 ensamblados de release sin
problemas, y el conjunto Development previsto coincide 46/46 con la carpeta
`Managed` del player 0.2.0 (`Validation/V030/QA/release-assemblies-03`).

## 6. Build de release

```powershell
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.BuildV030 -logFile "$V/build.log"
$out = (Select-String "$V/build.log" -Pattern '^LMS_ALFA_BUILD Succeeded (.+)$').Matches[0].Groups[1].Value
Get-Content "$out/build-receipt.json"
```

Esperado: `result=Succeeded`, `errors=0`, `version=0.3.0`, `profile=release`,
`developmentBuild=false`, `releaseProblems=[]`, `sourceDirty=false`,
`sourceCommit` = `git rev-parse HEAD`. Sale en `N:/LetMeSleep/Artifacts/0.3.0-<UTC>` (el log dice además `LMS_BUILD_PROFILE release`).
El build usa `BuildOptions.None`: sin Development Build, profiler ni
PlayerConnection, sin depuración de scripts, sin IP en `boot.config`. Si detecta
contenido de desarrollo lanza excepción y lo anota en `releaseProblems`. Las
credenciales EOS se inyectan sólo durante el build y se borran de StreamingAssets
al terminar. `git status` debe seguir vacío.

## 7. Smoke (build de diagnóstico del mismo commit)

La sonda `--lms-probe-output` no existe en el build de release. El smoke
automático corre sobre un build Development del mismo commit; **no** reemplaza
el recorrido del paso 9.

```powershell
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.BuildV030Diagnostics -logFile "$V/build-diag.log"
$diag = (Select-String "$V/build-diag.log" -Pattern '^LMS_ALFA_BUILD Succeeded (.+)$').Matches[0].Groups[1].Value
pwsh -File "$wt/work/smoke-v030.ps1" -BuildDirectory $diag -ReleaseBuildDirectory $out -OutputRoot "$V/Smoke01"          # casa/sangre
pwsh -File "$wt/work/smoke-v030.ps1" -BuildDirectory $diag -ReleaseBuildDirectory $out -OutputRoot "$V/SmokeMatrix01" -Matrix  # 5 mapas x 3 modos
```

Abre una ventana visible (las corridas ocultas o con `-force-d3d11` daban PNG
negros). Mirá `menu.png`, `human.png` y `mosquito.png` de cada caso. El build de
diagnóstico **nunca** se empaqueta ni se publica.

## 8. Paquete

```powershell
pwsh -File "$wt/work/package-v030.ps1" -BuildDirectory $out | Tee-Object "$V/package.json"
$pk = Split-Path ((Get-Content -Raw "$V/package.json" | ConvertFrom-Json).package)
```

Sale en `N:/LetMeSleep/Artifacts/v0.3.0/packages/<UTC>-<guid8>/`:
`Let-me-sleep-0.3.0-Windows.zip`, `.zip.sha256.txt`, `Let-me-sleep-series-2.json`,
la carpeta desempaquetada y `package.json` (interno). El script rechaza builds
que no sean de release, sucios o de otra versión, y cualquier contenido de
desarrollo. El ZIP no lleva `build-receipt.json`, `*_BurstDebugInformation_DoNotShip`,
`*_BackUpThisFolder_ButDontShipItWithYourGame` ni `.pdb`. `BUILD.json` lleva
`version 0.3.0`, `releaseSeries 1`, `sourceCommit` y el SHA-256 de cada archivo.

## 9. Recorrido manual obligatorio del exe de release

Es el primer player no Development del proyecto y la sonda no existe en él, así
que nada automático prueba el binario que se sube. Antes de publicar, una
persona juega **el ZIP del paso 8** (no la carpeta del build) con:

```powershell
pwsh -File "$wt/work/walkthrough-v030.ps1" -PackageDirectory $pk -OutputRoot "$V/Walkthrough01"
```

El script verifica el ZIP contra su `.sha256.txt`, lo extrae en
`$V/Walkthrough01/game`, compara cada archivo con `BUILD.json`, revisa
`boot.config` (sin `player-connection*`), `.pdb` y `*_DoNotShip`, y abre
`Let-me-sleep.exe` con `-logFile $V/Walkthrough01/player.log`. Después guía los
pasos y captura la ventana del juego en cada uno (sólo si el juego está al
frente; tras Enter hay 5 s para volver al juego):

1. `01-menu.png`: el menú dice `0.3.0` y no aparece "Development Build".
2. `02-training-human.png`: ENTRENAMIENTO → INICIAR en la tarjeta de humano;
   moverse, correr, saltar y golpear.
3. `03-training-mosquito.png`: Esc → SALIR DEL ENTRENAMIENTO → SALIR;
   ENTRENAMIENTO → INICIAR en la tarjeta de mosquito; volar, subir, bajar y
   posarse.
4. `04-room-created.png`: salir del entrenamiento igual que antes; JUGAR →
   CREAR SALA; la sala EOS muestra su código.
5. `05-room-left.png`: Esc → SALIR DE LA SALA → SALIR; vuelve al menú sin
   avisos de error.
6. Cerrar el juego desde el menú: SALIR → SALIR.

En cada paso el operador confirma (s/n) lo que vio. `walkthrough.json` queda con
`pass=true` sólo si el paquete verificó, las cinco capturas existen y pesan
≥ 100 KB, las seis confirmaciones son "s", el juego salió con código 0 y
`player.log` no tiene excepciones ni marcas `LMS_*FAILED/MISSING/INVALID/UNSUPPORTED`.
Mirá las cinco PNG: un `pass` no prueba el aspecto. Si falla, no se publica:
se corrige, se vuelve a compilar (paso 6) y se repite el recorrido. Esto no
verifica online entre redes distintas ni con dos identidades reales.

## 10. Publicación (sólo con autorización explícita del usuario)

Requisitos: pasos 2, 4, 5, 6, 7, 8 y 9 en verde con su evidencia en `$V`
(`claims.md`, `release-assemblies.json`, recibo, `smoke.json`, `package.json`,
`Walkthrough01/walkthrough.json` con `pass=true` y sus PNG revisadas) y el
**mismo** `sourceCommit` en el recibo, `BUILD.json` y `walkthrough.json`.

Tag `v0.3.0` en ese `sourceCommit` y release **prerelease** en
`Sauri0/LetMeSleep` con tres assets: el ZIP, su `.sha256.txt` y
`Let-me-sleep-series-2.json`. Notas: `docs/v030/RELEASE-NOTES-0.3.0.md`
(su comentario HTML repite qué punto depende de qué evidencia). Sin launcher.

```powershell
git -C $wt tag v0.3.0 <sourceCommit>; git -C $wt push origin v0.3.0
gh release create v0.3.0 "$pk/Let-me-sleep-0.3.0-Windows.zip" "$pk/Let-me-sleep-0.3.0-Windows.zip.sha256.txt" "$pk/Let-me-sleep-series-2.json" `
  --repo Sauri0/LetMeSleep --verify-tag --draft --prerelease --title 'Let me sleep v0.3.0 — versión de prueba' `
  --notes-file "$wt/docs/v030/RELEASE-NOTES-0.3.0.md"
gh release download v0.3.0 --repo Sauri0/LetMeSleep --dir "$V/verify"   # comparar Get-FileHash con los locales
gh release edit v0.3.0 --repo Sauri0/LetMeSleep --draft=false
```

Después: README ya apunta a esta release y a `docs/player/PRUEBA-V0.3.0.md`;
registrar la entrega en `docs/ceo/STATE.md` y `docs/ceo/runs.jsonl`.
