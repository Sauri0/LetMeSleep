# Root batch con graphics — sin editor conectado

Sustituye el prerrequisito del runnerCLI conectado para el entorno actual. Leído loader central HiggsfieldExternalFixture.cs: clase global exacta HiggsfieldMapChecks y métodoRun(string configPath,string output). DLL debe residir bajo N:/LetMeSleep/Validation/Higgsfield/. Este wrapper cumple ese contrato y sale del Editor al finalizar; no pasar -quit.

Una llamada PowerShell7 porRoot con turno exclusivo de proyecto/graphics:

```powershell
& 'N:/LetMeSleep/Worktrees/ui/docs/unity/ui/fixtures/Run-TrainingMapBatch.ps1'
```

No ejecutado porUI. Script usaProcessStartInfo.ArgumentList (rutasconespacios seguras) yCreateNoWindow; no abreUnityinteractivo/conectado. No usar concurrente con otroUnity sobre mismo proyecto. Si timeout, devuelvePID y deja proceso para diagnósticoRoot; no mata otrosprocesos ni lanza otrobatch.

Archivos precompilados:

- N:/LetMeSleep/Validation/Higgsfield/TrainingUI/bin/HiggsfieldTrainingUIBatch.dll
- N:/LetMeSleep/Validation/Higgsfield/TrainingUI/Batch.csproj
- N:/LetMeSleep/Validation/Higgsfield/TrainingUI/config.json

DLLautocontenida incluye TrainingMapNativeFixture y entrybatch; sólo referenciasUnity/TMP/UGUI/UI/URP delproyecto, no cargarnuevacopiaUI. El loader central ya existe: no modificarBootstrap/importer/Assets/asmdef.

## Qué hace

Escena vacía temporal, PlayMode sin domainreload para preservar entryexterno, cámara+RT1280x720. Usa fuentes TMPreales deconfig, UIAlfaUiRuntime real con5IDsfixture explícitos, ejecuta tests deintención/busy/retry/volver, luego capturaUI conURPSingleCameraRequest yReadPixels UNAvezporres. Repite1920x1080. LiberaUI/eventSystemcreado/cámara/RT, salePlay, restauraopcionesEnterPlayMode yEditorApplication.Exit0/1. Excepción/timeout genera fallo. Si proceso seinterrumpeabruptamente, Root debe comprobar restauración deopcionesEditor; noasumirla sinrecibo.

## Alcance exactoRT

CanvasScreenSpaceCamera conpixelRect comprobado720/1080, CanvasScaler deshabilitado sóloenfixture yscaleFactor explícito width/1920 (equivalentegeométrico a16:9). Evita Screen/resolución dedesktop de batch. NOprueba selecciónautomáticadeescalaGameView ni clicfísico. Coordenadasdebotones seproyectan concámaraRT, no conScreen. Texto sigue generándose conTMP ylayoutUIreal, noimagenfabricada.

PNG training-720-RT.png / training-1080-RT.png exactosvalidanIHDR. batch.json contiene scope, resultadosbooleanosdechecks y rutasrecibos. Si fixturefalla, intenta conservar captura dediagnóstico yseguiraotrares; elresultadofinal esfixture-checks-failed/exit1. No llamaShowMapForReview para ocultar fallo. Unerrorderender fatal puedeimpedirPNG: fallaexplícito, nunca produce captura ficticia.

Siempregraphics: sin -nographics, rechazaGraphicsDeviceType.Null. Seincluye -monitor1 para banco principal horizontal; la evidenciaesRT, no pantalla física. No512px, noresize posterior, noassets/mapas/geometría. Exit0=checks+capturasproducidas, estado captured-awaiting-visual-review; Root aún revisa2imágenes/recibos y no certificacarga5mapas.

Validaciónoffline: compile0errores/advertencias, parserPowerShellOK; ningunaejecuciónUnity/GPU desdeUI. Primeraejecuciónbatch/URP/CanvaspixelRect pendienteRoot.
