# v0.3.0 — pasos de compilación, paquete y publicación (integrador)

Receta mecánica sobre la rama integrada. Todo en PowerShell 7 salvo que diga
bash. Un solo Unity por proyecto y como máximo tres en la máquina: adquirí un
slot en `N:/LetMeSleep/Validation/V030/locks` antes de cada Unity y liberalo al
terminar (ver las reglas de la ola). Cada corrida escribe en un directorio nuevo.

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
```

## 2. Tests (sin `-quit`)

- EditMode: `-batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`.
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

## 4. Build de release

```powershell
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.BuildV030 -logFile "$V/build.log"
$out = (Select-String "$V/build.log" -Pattern '^LMS_ALFA_BUILD Succeeded release (.+)$').Matches[0].Groups[1].Value
Get-Content "$out/build-receipt.json"
```

Esperado: `result=Succeeded`, `errors=0`, `version=0.3.0`, `profile=release`,
`developmentBuild=false`, `releaseProblems=[]`, `sourceDirty=false`,
`sourceCommit` = `git rev-parse HEAD`. Sale en `N:/LetMeSleep/Artifacts/0.3.0-<UTC>`.
El build usa `BuildOptions.None`: sin Development Build, profiler ni
PlayerConnection, sin depuración de scripts, sin IP en `boot.config`. Si detecta
contenido de desarrollo lanza excepción y lo anota en `releaseProblems`. Las
credenciales EOS se inyectan sólo durante el build y se borran de StreamingAssets
al terminar. `git status` debe seguir vacío.

## 5. Smoke (build de diagnóstico del mismo commit)

La sonda `--lms-probe-output` no existe en el build de release. Para el smoke:

```powershell
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.BuildV030Diagnostics -logFile "$V/build-diag.log"
$diag = (Select-String "$V/build-diag.log" -Pattern '^LMS_ALFA_BUILD Succeeded development (.+)$').Matches[0].Groups[1].Value
pwsh -File "$wt/work/smoke-v030.ps1" -BuildDirectory $diag -ReleaseBuildDirectory $out -OutputRoot "$V/Smoke01"          # casa/sangre
pwsh -File "$wt/work/smoke-v030.ps1" -BuildDirectory $diag -ReleaseBuildDirectory $out -OutputRoot "$V/SmokeMatrix01" -Matrix  # 5 mapas x 3 modos
```

Abre una ventana visible (las corridas ocultas o con `-force-d3d11` daban PNG
negros). Mirá `menu.png`, `human.png` y `mosquito.png` de cada caso. El build de
diagnóstico **nunca** se empaqueta ni se publica.

Además, abrí una vez el `Let-me-sleep.exe` de release: el menú dice `0.3.0` y no
aparece la marca "Development Build".

## 6. Paquete

```powershell
pwsh -File "$wt/work/package-v030.ps1" -BuildDirectory $out | Tee-Object "$V/package.json"
```

Sale en `N:/LetMeSleep/Artifacts/v0.3.0/packages/<UTC>-<guid8>/`:
`Let-me-sleep-0.3.0-Windows.zip`, `.zip.sha256.txt`, `Let-me-sleep-series-2.json`,
la carpeta desempaquetada y `package.json` (interno). El script rechaza builds
que no sean de release, sucios o de otra versión, y cualquier contenido de
desarrollo. El ZIP no lleva `build-receipt.json`, `*_BurstDebugInformation_DoNotShip`,
`*_BackUpThisFolder_ButDontShipItWithYourGame` ni `.pdb`. `BUILD.json` lleva
`version 0.3.0`, `releaseSeries 1`, `sourceCommit` y el SHA-256 de cada archivo.
Autotest del script con un build sintético:
`N:/LetMeSleep/Validation/V030/QA/package-selftest/run-selftest.ps1`.

## 7. Publicación (sólo con autorización explícita del usuario)

Tag `v0.3.0` en el `sourceCommit` del recibo y release **prerelease** en
`Sauri0/LetMeSleep` con tres assets: el ZIP, su `.sha256.txt` y
`Let-me-sleep-series-2.json`. Notas: `docs/v030/RELEASE-NOTES-0.3.0.md`
(revisar primero su comentario para el integrador). Sin launcher.

```powershell
$pk = '<carpeta del paquete>'
git -C $wt tag v0.3.0 <sourceCommit>; git -C $wt push origin v0.3.0
gh release create v0.3.0 "$pk/Let-me-sleep-0.3.0-Windows.zip" "$pk/Let-me-sleep-0.3.0-Windows.zip.sha256.txt" "$pk/Let-me-sleep-series-2.json" `
  --repo Sauri0/LetMeSleep --verify-tag --draft --prerelease --title 'Let me sleep v0.3.0 — versión de prueba' `
  --notes-file "$wt/docs/v030/RELEASE-NOTES-0.3.0.md"
gh release download v0.3.0 --repo Sauri0/LetMeSleep --dir "$V/verify"   # comparar Get-FileHash con los locales
gh release edit v0.3.0 --repo Sauri0/LetMeSleep --draft=false
```

Después: README ya apunta a esta release y a `docs/player/PRUEBA-V0.3.0.md`;
registrar la entrega en `docs/ceo/STATE.md` y `docs/ceo/runs.jsonl`.
