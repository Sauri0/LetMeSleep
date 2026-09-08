"""Collect the bounded, unpublished integration evidence after build.ps1 passes."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'outputs/0.7-integracion'
WORK = ROOT / 'work'
log = WORK / 'build07-integration.log'
text = log.read_text(encoding='utf-8-sig')
assert 'Algorithm' in text and 'SHA256' in text, 'Successful export/hash footer missing'
assert not re.search(r'failures=[1-9]|SCRIPT ERROR|Godot check failed|Native check failed', text)
logs = OUT / 'validacion'
logs.mkdir(parents=True, exist_ok=True)
shutil.copy2(log, logs / log.name)
for name in ('forearm-a-yaw-comparison.md', 'forearm-a-yaw-diagnosis-before.json',
             'forearm-a-yaw-diagnosis-after.json', 'selected-impact07-report.md'):
    shutil.copy2(WORK / name, logs / name)
native = ('v07_character_rig_checks', 'v07_character_client_checks', 'selected07_mesh_checks',
          'selected07_actor_checks', 'selected07_facial_envelope_checks', 'house07_checks',
          'house07_lighting_probe', 'ui_navigation_test', 'video07_checks', 'doors07_client_checks')
for name in native:
    for suffix in ('.log', '.err'):
        source = WORK / ('build-' + name + suffix)
        assert source.exists()
        if suffix == '.err': assert source.stat().st_size == 0, str(source)
        shutil.copy2(source, logs / source.name)
lighting = ROOT / 'outputs/0.7-lighting-ab'
shutil.copytree(lighting / 'energy-055/production-final', OUT / 'iluminacion', dirs_exist_ok=True)
lighting_report = (lighting / 'RESULTADO.md').read_text(encoding='utf-8')
lighting_report = lighting_report.replace('energy-055/production-final/', '')
lighting_report = lighting_report.replace('Root medirá el candidato integrado.', 'La medida de una partida con el candidato integrado queda pendiente para el cierre posterior de 0.7.')
(OUT / 'iluminacion/RESULTADO.md').write_text(lighting_report, encoding='utf-8')
exe = ROOT / 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe'
preview = OUT / 'Windows-preview'
preview.mkdir(exist_ok=True)
shutil.copy2(exe, preview / exe.name)
for folder in ('Licencias-fuentes', 'Licencias-audio'):
    shutil.copytree(exe.parent / folder, preview / folder, dirs_exist_ok=True)
commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
digest = hashlib.sha256(exe.read_bytes()).hexdigest()
report = f'''# Integración de personajes e iluminación · 8 de septiembre de 2026

Tanda integrada y compilada; **acabado de la casa pendiente de elección liso/sutil**. Esta entrega es una vista previa local, no una publicación ni la versión 0.7 terminada.

- Humano A compacto y mosquito B alargado, con seis caras, diez controles faciales por cara y personalización conservada.
- Malla visible y superficies de contacto ajustadas juntas. El mosquito comparte orientación y anatomía entre dibujo e impactos; alas, patas, antenas y probóscide siguen siendo apéndices visuales.
- Corregido el giro involuntario al inspeccionar el antebrazo: 0 de 1024 casos supera los 75°, máximo 72,8711°. Seguirlo durante 2 s deja el torso en 0° de giro, tanto en fase fija como en marcha animada.
- Piel corregida a su color original; iluminación continua con 23 focos con sombra y sin los rellenos de ventana que no proyectaban sombra. Oclusión por puertas comprobada mediante un control con receptor dentro del cono.
- HUD compacto y ayuda con F1. En Sangre y Antes de dormir se conserva el aturdimiento de 35 s; ayudar cerca acelera la recuperación. Supervivencia conserva la eliminación.

## Validación actual

`work/build.ps1` completo pasó: importación, análisis de scripts, regresiones de lógica, diez comprobaciones nativas y exportación Windows. El script rechaza errores de Godot en stderr aunque el proceso devuelva código cero. Registro completo y diez logs nativos en `validacion/`.

Malla 378/378; cliente real 86/86; actores 411/411; movimiento 11396/11396; envolvente facial 67056/67056; impacto 1492/1492; defensa manual 6795/6795; aturdimiento/ayuda 297/297; combate por RPC de producción 34/34. Este último fixture ahora espera el par fiable final público/privado en ambos clientes antes de cerrar la auditoría: cero paquetes pendientes, sin cambiar la red del juego ni relajar las comprobaciones de privacidad. El registro adjunto contiene las demás suites. Son comprobaciones sobre el proyecto integrado; no equivalen a una nueva matriz multijugador de seis escenarios ejecutada desde este EXE.

## Evidencia para revisar

- `faciales-integrados.mp4`: 8 s, 1280 × 720, 30 fps. Visor nativo de expresiones de producción; no es un benchmark ni una partida.
- `faciales/rostros-alert.png`: las seis caras completas.
- `client/gesto-manual-10.png`: golpe manual confirmado; `client/propio-stand-zona-4.png`: antebrazo visible.
- `iluminacion/candidate-2048/world-detail.png` y `17-rosa.png`: personaje en el entorno con materiales de producción.
- `iluminacion/candidate-2048/door-*.png` y `iluminacion/metrics.json`: control cualitativo de puerta cerrada/abierta/sin sombra. No representa fotometría de todas las habitaciones.
- `validacion/forearm-a-yaw-comparison.md`: comparación de postura antes/después.

Las carpetas `before`, `surface-pass1` y `surface-pass2` son diagnósticos históricos, no el resultado final. La ficha `INTEGRACION.json` documenta los modelos y controles.

## Vista previa y límites

Ejecutable: `Windows-preview/Let-me-sleep.exe` ({exe.stat().st_size:,} bytes).
SHA-256: `{digest}`.
Commit local de integración: `{commit}`.

Persisten bordes duros de sombra y facetas visibles en brazos y algunos marcos. El acabado liso/sutil no se aplicó globalmente. Las medidas estáticas de iluminación no certifican 60 FPS con 16 jugadores ni rendimiento en GTX 1660 Ti. Rendimiento con el arte integrado, la matriz completa de red desde el EXE y el cierre de distribución/publicación quedan para el cierre posterior de 0.7. No se inició otra tanda de arte ni optimización.

La publicación 0.6 y el candidato previo conservado en `outputs/0.7-candidate-before-live-cpu/` permanecen intactos.
'''
(OUT / 'INFORME-FINAL.md').write_text(report, encoding='utf-8')
(preview / 'LEEME-PREVIEW.md').write_text('Vista previa local de integración. No es una versión final publicada.\n\nVer ../INFORME-FINAL.md para cambios, validación y pendientes.\n', encoding='utf-8')
manifest = {'status': 'bounded integration complete; environment finish pending', 'commit': commit,
            'exe_sha256': digest, 'files': []}
for base in (OUT / 'INFORME-FINAL.md', OUT / 'INTEGRACION.json', OUT / 'faciales-integrados.mp4', preview / exe.name):
    manifest['files'].append({'path': base.relative_to(OUT).as_posix(), 'bytes': base.stat().st_size,
                              'sha256': hashlib.sha256(base.read_bytes()).hexdigest()})
(OUT / 'CIERRE.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(json.dumps(manifest, indent=2))
