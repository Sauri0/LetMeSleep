"""Assemble measured 0.7 delivery evidence. Does not publish or change game code."""
from pathlib import Path
import datetime, hashlib, json, re, shutil, subprocess, sys, zipfile

ROOT=Path(__file__).resolve().parent.parent
WORK=ROOT/'work'; OUT=ROOT/'outputs'; VERSION='0.7.0'
EXE=OUT/f'Let-me-sleep-{VERSION}-Windows/Let-me-sleep.exe'
VIDEO=OUT/'0.7-preview/Let-me-sleep-0.7-objetos.mp4'
MENU_VIDEO=OUT/'0.7-preview/Let-me-sleep-0.7-tu-pinta.mp4'
EVIDENCE=OUT/'0.7-validacion'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest().upper()
def write(p,data): p.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def git(*args): return subprocess.check_output(['git',*args],cwd=ROOT,text=True).strip()
def artifact(p): return dict(file=p.relative_to(OUT).as_posix(),bytes=p.stat().st_size,sha256=sha(p))

code=(WORK/'release07-code.txt').read_text().strip()
assert re.fullmatch('[a-f0-9]{40}',code)
if '--manifest' not in sys.argv:
    EVIDENCE.mkdir(exist_ok=True)
    checks={}
    for name in ['v07_character_rig_checks','v07_character_client_checks','selected07_mesh_checks','selected07_actor_checks','selected07_facial_envelope_checks','facial_parts08_checks','facial_blink08_checks','customization08_checks','network08_cosmetics_checks','cosmetics_catalog08_test','house07_checks','house07_liso_checks','house07_lighting_probe','video07_checks','doors07_client_checks','hud_input06_checks','camera_turn_checks','ui_navigation_test','practice','hosting','combat','throw_client07_checks','tool07_visual_checks','pickup07_support_checks','frame07_joinery_test','house07_occlusion_checks','occlusion-transitions','network-throw']:
        p=WORK/f'release07-{name}.log'
        text=p.read_text(encoding='utf-8-sig')
        assert not re.search(r'SCRIPT ERROR:|^ERROR:|failures=[1-9]|"failures"\s*:\s*[1-9]',text,re.M),name
        assert (WORK/f'release07-{name}.err').stat().st_size==0,name
        matches=re.findall(r'checks[=:]\s*(\d+)|"checks"\s*:\s*(\d+)',text)
        assert matches,name
        run=read(WORK/f'release07-{name}.run.json')
        assert run['passed'] and not run['source'] and run['exe_sha256'].upper()==sha(EXE),name
        shutil.copy2(WORK/f'release07-{name}.run.json',EVIDENCE/f'release07-{name}.run.json')
        checks[name]=int(next(v for v in matches[-1] if v))
        shutil.copy2(p,EVIDENCE/p.name)
    network=[]
    for name,count,rounds,winner in [('blood',2,2,'mosquito'),('tasks',2,1,'human'),('survival',16,1,'mosquito'),('disconnect',2,0,None),('invalid',1,0,None),('legacy06',1,0,None)]:
        p=WORK/f'release07-network-{name}.json'; record=read(p); reports=record['reports']
        assert record['exe']['sha256'].upper()==sha(EXE) and record['validation']['passed'],name
        assert record['validation']['all_client_exit_codes_zero'] and record['validation']['server_intentionally_stopped'],name
        assert len(reports)==count and not record['stderr'],name
        for r in reports:
            assert not r['errors'] and r['privacy_ok'] and not r['privacy_failures'] and r['privacy_pending']==0,name
            if rounds:
                assert len(r['results'])==rounds and len(r['roles_by_round'])==rounds and r['result']['winner']==winner,name
                assert all(result['winner']==winner for result in r['results']),name
                assert all(r[k] for k in ['lobby_movement_seen','lobby_jump_seen','lobby_crouch_seen','lobby_sprint_seen','cosmetics_synced']),name
            elif name in ['invalid','legacy06']: assert r['incompatible_rejected'] and r['private_packets']==0,name
        if name=='blood': assert all(r['returned_to_lobby'] for r in reports)
        if name=='disconnect':
            assert sum(bool(r.get('intentional_disconnect')) for r in reports)==1
            assert all(r['returned_to_lobby'] and not r['result'].get('winner') for r in reports if not r.get('intentional_disconnect'))
        network.append(dict(scenario=name,clients=count,rounds=rounds,passed=True,private_matched=sum(r['privacy_matched'] for r in reports),private_deferred=sum(r['privacy_deferred'] for r in reports),private_pending=0,run=record['run']))
        shutil.copy2(p,EVIDENCE/p.name)
    performance=[]
    for population,res in [(1,1080),(16,1080),(16,1440),(16,2160)]:
        p=WORK/f'release07-performance-{population}-{res}.json'; r=read(p)
        assert r['version']==VERSION and r['samples']>50 and r['physics_ticks_per_second']==60
        assert r['exe_sha256'].upper()==sha(EXE) and r['fps_limit']==0 and r['occlusion_culling']
        assert sum(r['physics_ticks_per_rendered_frame'].values())==r['samples']
        performance.append(r); shutil.copy2(p,EVIDENCE/p.name)
    for name in ['performance07-baseline06-1.json','performance07-baseline06-16.json']:
        shutil.copy2(WORK/name,EVIDENCE/name)
    demos=[read(WORK/f'release07-{name}.json') for name in ['tools-demo','customization-demo']]
    assert all(d['failures'] in (0,[]) and d['capture']['exe_sha256'].upper()==sha(EXE) and not d['preferences_modified'] for d in demos)
    assert all(p.exists() for p in [VIDEO,MENU_VIDEO])
    for name in ['tools-demo','customization-demo']: shutil.copy2(WORK/f'release07-{name}.json',EVIDENCE/f'release07-{name}.json')
    facial_coverage=read(OUT/'0.7-combinaciones/coverage.json')
    assert facial_coverage['all_neutral_heads_captured'], 'Capture every head geometry class before packaging.'
    assert facial_coverage['capture_modes']==['exported_exe'], 'Final geometry gallery must come from the exported candidate.'
    assert facial_coverage['capture_exe_sha256']==[sha(EXE)], 'Gallery must match the exact candidate EXE.'
    shutil.copy2(OUT/'0.7-combinaciones/coverage.json',EVIDENCE/'facial-coverage.json')
    motion_index=read(WORK/'release07-facial-motion-index.json')
    assert motion_index['complete'] and motion_index['case_count']==285 and motion_index['packed']
    assert motion_index['executable_sha256'].upper()==sha(EXE)
    motion_evidence=EVIDENCE/'facial-motion'
    motion_evidence.mkdir(exist_ok=True)
    for part in motion_index['parts']:
        assert part['verified'] and part['exit_code']==0 and not part['timed_out']
        assert sha(Path(part['file']))==part['sha256'].upper()
        assert not part['summary']['failures']
        for key in ['file','index','stdout','stderr']:
            original=Path(part[key]); target=motion_evidence/original.name
            shutil.copy2(original,target)
            part[key]=target.relative_to(EVIDENCE).as_posix()
    write(EVIDENCE/'facial-motion-index.json',motion_index)
    head_geometry=[]
    for role in ['human','mosquito']:
        source_report=WORK/f'facial08-geometry-{role}.json'
        measured=read(source_report)
        assert measured['role']==role and measured['numerical_passed']
        assert not measured['semantic_failures'] and not measured['unexpected_pairs']
        assert measured['source_sha256'].upper()==sha(ROOT/f'art_source/characters/{role}/{role}_lms06.blend')
        assert measured['pair_samples']>0
        shutil.copy2(source_report,EVIDENCE/source_report.name)
        head_geometry.append(dict(role=role,report=source_report.name,report_sha256=sha(source_report),pair_samples=measured['pair_samples'],limits=measured['limitations']))
        blink_path=WORK/f'blink08-geometry-{role}.json'
        blink=read(blink_path)
        assert blink['role']==role and not blink['failures'] and blink['checks']>0
        assert blink['source_sha256'].upper()==measured['source_sha256'].upper()
        assert {c['eyes'] for c in blink['cases']}=={0,1,2}
        assert all(not c['closure_misses'] and c['ocular_blink_delta_m']==0 and c['closure_rays']>0 for c in blink['cases'])
        shutil.copy2(blink_path,EVIDENCE/blink_path.name)
        head_geometry[-1]['blink_report']=blink_path.name
        head_geometry[-1]['blink_checks']=blink['checks']
    build=(WORK/'build07-full.log').read_text(encoding='utf-8-sig')
    assert not re.search(r'SCRIPT ERROR:|^ERROR:|failures=[1-9]',build,re.M)
    shutil.copy2(WORK/'build07-full.log',EVIDENCE/'build07-full.log')
    summary=dict(version=VERSION,status='candidate_unpublished',code_commit=code,exe=artifact(EXE),checks=checks,network=network,performance=performance,demos=demos,facial_coverage=facial_coverage,facial_motion_export=motion_index,head_geometry=head_geometry)
    write(EVIDENCE/'summary.json',summary)
    rows=[]
    for r in performance:
        f=r['frame_ms']; rows.append(f"| {r['actors']} | {r['resolution']} | {f['p50']:.2f} | {f['p90']:.2f} | {f['p99']:.2f} | {f['over_16_67ms_percent']:.2f}% |")
    table='\n'.join(rows)
    runtime_rows=[]
    for r in performance:
        def pct(key):
            d=r[key]; return f"{d['p50']:.2f} / {d['p90']:.2f} / {d['p99']:.2f}"
        ticks=', '.join(f'{k}: {v}' for k,v in sorted(r['physics_ticks_per_rendered_frame'].items(),key=lambda pair:int(pair[0])))
        runtime_rows.append(f"| {r['actors']} / {r['resolution']} | {pct('render_cpu_ms')} | {pct('render_gpu_ms')} | {pct('physics_ms')} | {ticks} |")
    runtime_table='\n'.join(runtime_rows)
    native='\n'.join(f'| {name} | {count} |' for name,count in checks.items())
    facial_rows='\n'.join(f'| {role} | {r["valid_geometry_classes"]} | {r["captured_neutral_eight_views"]} / {r["expected_head_combinations"]} | {r["visually_reviewed_neutral_eight_views"]} |' for role,r in facial_coverage['roles'].items())
    report=f'''# Let me sleep 0.7.0 — candidato para revisión, sin publicar

Godot 4.5.2, Windows x86_64, Compatibility/OpenGL, protocolo 8 e invitaciones DD4. Código `{code}`. EXE SHA256 `{sha(EXE)}`. Paquetes y commit final de documentación en MANIFIESTO-0.7.0.json, sin referencias circulares.

## Ejecutable candidato

Todas las comprobaciones de esta tabla se ejecutaron sobre el mismo EXE exportado, identificado por hash en cada informe de ejecución. Los informes y el log completo de compilación se conservan en outputs/0.7-validacion.

| Prueba | Comprobaciones sin fallos |
|---|---:|
{native}

El build también ejecuta las suites de reglas, locomoción, defensa manual, foco, aturdimiento/ayuda, puertas, rutas, ataques, índice espacial, caché de pose, movimiento, bots, red, sonido, cosméticos, privacidad y preferencias. Sus resultados exactos están en el log completo adjunto. Humano A compacto y mosquito B alargado mantienen fuentes Blender, rig, personalización y nueve piezas faciales por especie, con diez controles por pieza. El humano importado tiene 36 huesos, incluidos los cuatro nuevos segmentos de pulgares; los JSON históricos de validación 0.6 conservan su recuento anterior y no se usan como prueba de este candidato. La comprobación nativa mide triángulos deformados y los extremos de las expresiones contra sus superficies de contacto. La inspección del antebrazo conserva el límite de 75° y ahora alcanza un máximo de 72,8711° sin giro involuntario del torso.

La red real ENet se probó con seis escenarios y 24 informes: Sangre 1v1 por invitación con revancha, Tareas 1v1, Supervivencia 4+12, desconexión sin ganador, protocolo inválido y rechazo del EXE 0.6. Los {sum(n['private_matched'] for n in network)} paquetes privados cotejados no dejan pendientes. Son procesos en esta misma PC, no redes independientes. El fixture adicional de combate usa servidor y dos clientes ENet en subárboles aislados: herramientas, golpe manual a adversario móvil, bloqueo por puerta y privacidad. Al cerrar espera un par fiable público/privado final en ambos clientes, sin relajar la auditoría. No se atribuye ese combate a las rondas automáticas de la matriz.

## Rendimiento medido, sin grabar vídeo

Equipo: {performance[0]['adapter']}, {performance[0]['cpu']}. Práctica real con Main/Client, bots y autoridad local; física a 60 Hz, VSync desactivado y FPS sin límite. Dos segundos de calentamiento y 12 de medición por caso. Recorrido preparado por pasillo y comandos periódicos de puertas, actualizado según reloj real: no es una trayectoria idéntica fotograma por fotograma. La resolución indicada coincide con la textura de renderizado; la ventana puede ajustarse al escritorio. La solicitud de población 1 usa dos actores, el mínimo de una práctica válida; la tabla informa los actores reales.

| Actores | Resolución | Frame p50 ms | p90 ms | p99 ms | Frames >16,67ms |
|---:|---|---:|---:|---:|---:|
{table}

| Caso | Render CPU p50/p90/p99 ms | Render GPU p50/p90/p99 ms | Física p50/p90/p99 ms | Ticks por frame: cantidad |
|---|---|---|---|---|
{runtime_table}

Son distribuciones separadas, no se suman sus percentiles. El monitor de física informa el tiempo de procesamiento observado por Godot; el histograma indica cuándo se acumulan varios ticks en un fotograma. Estas mediciones localizan costes de renderizado y simulación, pero no atribuyen por sí solas el coste a una función concreta.

La versión 0.7 no se declara terminada ni validada a 60 FPS. No son FPS garantizados ni certificación de requisitos mínimos. El objetivo GTX 1660 Ti / 1080p60 sigue sin validar porque ese equipo no estuvo disponible. El porcentaje sobre 16,67 ms muestra cuándo este recorrido incumple el presupuesto de 60 FPS, incluso si la mediana es rápida. 1440p/4K tampoco garantizan 60 FPS en toda escena.

Referencia del EXE 0.6 inmutable con este fixture a 1080p: dos actores p50/p90/p99 = 5,415/7,260/9,859 ms; 16 actores = 25,666/51,641/85,787 ms, 88,75% sobre 16,67 ms. Es otra ejecución y no garantiza una mejora fija entre equipos o escenas. En 0.7 las decisiones de bots se escalonan a 20 Hz y la simulación conserva 60 Hz. Las antiguas muestras breves a 720p de 0.6 medían otra carga y no prueban 60 FPS en esta prueba prolongada.

## Vídeo y límites

El vídeo proviene de este EXE candidato mediante MovieWriter a 30 FPS y su audio real, sin reemplazo de música ni normalización. Son dos escenas preparadas y rotuladas, con objetos iniciales cercanos y un adversario estacionario para el golpe manual. Se muestran recogida, agarre, golpe, carga corta y completa, lanzamiento y recuperación de la misma pantufla mediante entradas reales de Client. El fixture pasa {demos[0]['checks']} comprobaciones; no representa una partida espontánea ni mide rendimiento o balance humano.

El segundo clip, Tu pinta, muestra partes independientes de ambas especies, color compartido de pelo/cejas/bigote/barba, giro, zoom y guardado/reapertura reales. Pasa {demos[1]['checks']} comprobaciones. Durante esta escena preparada se fija una mezcla de menú audible dentro de Godot; se restauran exactamente los bytes de preferencias al terminar. No se reemplaza ni normaliza el audio del archivo grabado.

## Combinaciones faciales

| Especie | Clases geométricas del catálogo | Cabezas capturadas, ocho vistas | Cabezas revisadas visualmente, ocho vistas |
|---|---:|---:|---:|
{facial_rows}

Cada cabeza tiene ID y vínculo a las clases que sólo cambian prendas/calzado; las variaciones de color se representan por su producto de dominios. El catálogo verifica aceptación y persistencia de valores, no ajuste geométrico. El índice distingue imágenes capturadas de imágenes revisadas, y conserva los faltantes. Los controles de geometría e invariancia de materiales se documentan por separado; estos números no prueban por sí solos ausencia de cruces ni continuidad durante toda animación.

La UI deja la guía plegada en F1 y el aviso de puerta junto a la mira. El arco de oportunidad deriva del recorrido manual y línea de visión, sin apuntado automático ni garantía de impacto. En Sangre/Tareas la caída dura 35 s y ayudar con E acelera 4× sin acumular ayudantes; Supervivencia conserva eliminación.

Los cinco objetos tienen agarre canónico, alcance y gestos propios. Diario y pantufla se lanzan manteniendo y soltando el botón derecho; el cliente cancela de forma segura al abrir menús, perder foco o cambiar de objeto. La autoridad conserva el mismo objeto y resuelve trayectorias, obstáculos y recuperación. El sonido usa objeto y material confirmados. Las escobas descansan en portaescobas del lavadero/taller y las pantuflas en bancos bajos.

La oclusión estática usa exactamente los triángulos finales de paredes y pisos, incluidos sus huecos; no agrega puertas ni objetos móviles a los oclusores. Las imágenes emparejadas verifican el aspecto en ambos pisos y ambos lados de puertas durante una secuencia de apertura/cierre. El histograma de ticks de física por fotograma acompaña las mediciones.

La casa usa el acabado liso elegido para paredes y suelo, preservando paletas, juntas y materiales específicos de muebles y objetos. Tiene 23 focos continuos con sombra y un ReflectionProbe estático en baño; no es un espejo plano del jugador y no se usa SSR/SSAO. Se corrigió el color de piel en origen y se retiraron los rellenos de ventana sin sombra. El control de puerta con receptor dentro del cono demuestra oclusión cualitativa, no fotometría de todas las habitaciones. Persisten facetas visibles y bordes duros de sombra en Compatibility. Las ventanas muestran exterior nocturno detrás de vidrio sólido, sin zona exterior jugable.

Durante integración se corrigió el falso golpe provocado por arrastrar un mosquito sobre el mismo antebrazo atacante. El testigo de defensa se adaptó a la anatomía real de tórax/cabeza/abdomen tras medir cinco contactos que tocaban el modelo alargado fuera de la antigua esfera; el alcance actual se deriva de hombro, brazo y geometría de cada herramienta, con preparación y recuperación propias, sin apuntado automático. El test de sprint comienza en un rellano libre, pues su antiguo origen coincidía con una baranda nueva. Los ajustes se restauran y los gates finales se ejecutan en serie.

Conexión directa requiere una ruta alcanzable. No hay EOS/relay integrado, prueba WAN entre casas, migración de anfitrión, certificación GTX 1660 Ti ni prueba de balance con jugadores humanos. La versión 0.6 publicada y sus archivos se conservan.
'''
    (ROOT/'distribution/PRUEBAS.md').write_text(report,encoding='utf-8')
    (ROOT/'distribution/BUILD.txt').write_text(f'Let me sleep {VERSION} — Windows x86_64\nGodot 4.5.2 / Compatibility OpenGL / protocolo 8 / invitación DD4\nEjecutable: Let-me-sleep.exe\nSHA256: {sha(EXE)}\nCommit de código: {code}\nPaquetes: Let-me-sleep-{VERSION}-Windows.zip / Let-me-sleep-{VERSION}-fuentes.zip\nHashes ZIP y commit de documentación: MANIFIESTO-{VERSION}.json externo.\nPruebas y límites: PRUEBAS.md.\nVersiones anteriores conservadas.\n',encoding='utf-8')
    for p in (ROOT/'distribution').iterdir():
        if p.is_file(): shutil.copy2(p,EXE.parent/p.name)
    print(json.dumps(dict(checks=checks,exe=artifact(EXE)),indent=2))
else:
    commit=git('rev-parse','HEAD'); summary=read(EVIDENCE/'summary.json')
    packages=[OUT/f'Let-me-sleep-{VERSION}-{kind}.zip' for kind in ['Windows','fuentes']]
    for p in packages:
        with zipfile.ZipFile(p) as z:
            assert z.testzip() is None
            if 'Windows' in p.name:
                n=next(n for n in z.namelist() if n.endswith('/Let-me-sleep.exe'))
                assert hashlib.sha256(z.read(n)).hexdigest().upper()==sha(EXE)
            else: assert z.comment.decode().strip()==commit
        p.with_suffix(p.suffix+'.sha256.txt').write_text(sha(p)+'  '+p.name+'\n',encoding='utf-8')
    record=dict(product='Let me sleep',version=VERSION,status='candidate_unpublished',protocol=8,platform='Windows x86_64',engine='Godot 4.5.2',generated_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),code_commit=code,source_and_docs_commit=commit,artifacts=[artifact(p) for p in packages+[EXE,VIDEO,MENU_VIDEO]],delivery_exe_checks=summary['checks'],network=summary['network'],performance=summary['performance'],evidence='0.7-validacion',preview='0.7-preview',guide=f'Let-me-sleep-{VERSION}-Windows/LEEME.html',limits=['ENet tested on one PC; no independent WAN or integrated relay','GTX 1660 Ti target not certified; measured RTX 3060 Ti results are scene-specific','Prepared gameplay demonstration with real inputs/audio','Static bathroom reflection; Compatibility retains hard shadow edges','Human balance not tested'],preserved_versions=['0.1.0','0.2.0','0.3.0','0.4.0','0.5.0','0.6.0'])
    write(OUT/f'MANIFIESTO-{VERSION}.json',record)
    print(json.dumps(record['artifacts'],indent=2))
