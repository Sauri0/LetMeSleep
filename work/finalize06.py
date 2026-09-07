"""Assemble the reviewed 0.6 evidence and documentation; never publishes."""
from pathlib import Path
import json, hashlib, subprocess, shutil, datetime, sys, zipfile

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'outputs'
WORK = ROOT / 'work'
VERSION = '0.6.0'
EXE = OUT / f'Let-me-sleep-{VERSION}-Windows/Let-me-sleep.exe'
CODE = '262620011a0230e2e8104b58e96cf68eb555aabf'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.file_digest(path.open('rb'), 'sha256').hexdigest().upper()
def write(path, data): path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
def artifact(path): return {'file':path.relative_to(OUT).as_posix(), 'bytes':path.stat().st_size, 'sha256':sha(path)}

if '--manifest' not in sys.argv:
    summaries=[]
    specs=[('blood',2,2,'mosquito'),('tasks',2,1,'human'),('survival',16,1,'mosquito'),('disconnect',2,0,None),('invalid',1,0,None),('legacy05',1,0,None)]
    for name, count, rounds, winner in specs:
        record=read(WORK/f'release06-network-{name}.json')
        reports=record['reports']
        assert len(reports)==count and not record['stderr'], name
        for r in reports:
            assert not r['errors'] and r['privacy_ok'] and not r['privacy_failures'] and r['privacy_pending']==0, (name,r['peer_id'])
            if rounds:
                assert len(r['roles_by_round'])==rounds and len(r['results'])==rounds and r['result']['winner']==winner, name
                assert all(r[k] for k in ['lobby_movement_seen','lobby_jump_seen','lobby_sprint_seen','lobby_crouch_seen','cosmetics_synced']), name
            elif name in ('invalid','legacy05'):
                assert r['incompatible_rejected'] and r['private_packets']==0, name
        if name=='blood': assert all(r['returned_to_lobby'] for r in reports)
        if name=='disconnect':
            assert sum(bool(r.get('intentional_disconnect')) for r in reports)==1
            assert all(r['returned_to_lobby'] and not r['result'].get('winner') for r in reports if not r.get('intentional_disconnect'))
        summaries.append({'scenario':name,'clients':count,'pass':True,'private_matched':sum(r['privacy_matched'] for r in reports),'private_deferred':sum(r['privacy_deferred'] for r in reports),'private_pending':0,'run':record['run']})
    network={'scenarios_passed':6,'clients_total':sum(s['clients'] for s in summaries),'private_packets_matched':sum(s['private_matched'] for s in summaries),'private_deferred':sum(s['private_deferred'] for s in summaries),'private_pending':0,'survival_clients':16,'internet_between_homes_verified':False,'integrated_relay':False,'scenarios':summaries,'runner_correction':'Successful PowerShell script now exits 0 explicitly. Previously LASTEXITCODE stayed null in a fresh caller; null -ne 0 was true. Complete original reports revalidated against every original assertion plus pending privacy count; no game rebuild.'}
    smoke=read(WORK/'release06-network-audiofinal.json')
    assert len(smoke['reports'])==2 and not smoke['stderr']
    assert all(not r['errors'] and r['privacy_ok'] and r['privacy_pending']==0 and r['result']['winner']=='mosquito' and r['cosmetics_synced'] for r in smoke['reports'])
    network['exe_sha256']='4B47820C2238B12482CE6D38C557B89F701AA04C30C68129FE660E2818E902AE'
    network['final_audio_smoke']={'exe_sha256':sha(EXE),'pass':True,'scenario':'blood 1v1 by invitation','clients':2,'private_matched':sum(r['privacy_matched'] for r in smoke['reports']),'private_pending':0,'stderr':[],'run':smoke['run']}
    write(WORK/'release06-network-summary.json',network)
    perf=read(WORK/'performance06-audiofinal-isolated.json')
    before=read(WORK/'performance06-before.json')
    demo=read(WORK/'release06-gameplay-audiofinal.json')
    assert demo['checks']==25 and demo['failures']==0
    table='\n'.join(f"| {r['visible_actors']} / {'host + bots' if r['host_bots'] else 'cliente'} | {b['frame_p50_ms']:.3f} | {r['frame_p50_ms']:.3f} | {r['frame_p90_ms']:.3f} | {r['draws_p50']:.0f} |" for b,r in zip(before['cases'],perf['cases']))
    report=f'''# Let me sleep 0.6.0 — verificación de entrega

7 de septiembre de 2026. Godot 4.5.2, Windows x86_64, Compatibility/OpenGL, protocolo 6 e invitaciones DD3. Código `{CODE}`. EXE SHA256 `{sha(EXE)}`. Los hashes de los ZIP y el commit final de documentación están en el manifiesto externo, sin referencias circulares.

## Validación por compilación

El EXE final difiere del candidato `4B47820C2238B12482CE6D38C557B89F701AA04C30C68129FE660E2818E902AE` únicamente en dos ganancias de audio: ayuda +20 dB y recuperación +10 dB. Se conservan sus informes completos y se repiten las pruebas afectadas sobre el final, sin atribuirle la matriz anterior como una nueva ejecución.

| Prueba | Resultado | EXE |
|---|---|---|
| Práctica nativa, ambos roles y tres modos | 73/73; menú, controles, bots, resultados, repetir y salir. | Final |
| Crear sala desde UI | 13/13; servidor propio, puerto, confirmación, reintento, cancelación y cierre. | Candidato 4B47 |
| Ayuda F1 con Main/Client/Practice reales | 28/28; abre/cierra, bloquea entradas, mantiene estado y devuelve captura sin salto de cámara. | Final |
| Giro continuo mirando el cuerpo | 6/6 a 60 FPS; torso acompaña, movimiento y palmada siguen disponibles. | Candidato 4B47 |
| Malla deformada y personalización | 166/166; rayos contra malla, posturas, combinaciones y editor. | Candidato 4B47 |
| Contacto con Client y simulación | 86/86; ocho zonas propias de pie/agachado, concentración y defensa. Transporte de este fixture simulado. | Candidato 4B47 |
| Casa GLB | 218/218, 52 muebles; posiciones, límites físicos y cinco vistas. | Candidato 4B47 |
| Demo con audio del juego | 25/25; vuelo, frenado, carga, acople, sangre, desprenderse, LMB, caída y rescate. | Final |
| Red ENet completa | 6 escenarios; 24 informes de cliente, incluidos 16 simultáneos. | Candidato 4B47 |
| ENet Sangre 1v1 por invitación | Dos clientes, resultado coincidente, cosméticos, privacidad y cierre sin errores. | Final |

ENet verificó Sangre 1v1 por invitación y revancha con dos rondas, Tareas 1v1, Supervivencia 4 humanos + 12 mosquitos, desconexión sin ganador, rechazo de protocolo y rechazo del EXE 0.5 real. {network['private_packets_matched']} paquetes privados coinciden, {network['private_deferred']} diferidos resueltos, cero pendientes, errores de cliente o stderr. Todos los clientes de rondas completas comprobaron movimiento, salto, carrera, agacharse y cosméticos. Son procesos en una misma PC, no redes independientes. Aturdimiento/rescate se comprueban por separado; no se afirma que las rondas ENet naturales los hayan ejercitado.

El vídeo dura 21,77 s, H.264/AAC, 1280×720, 30 FPS. MovieWriter capturó el mezclador real del EXE final, sin sustituir ni normalizar audio: promedio −40,2 dB, pico −21,8 dB, decodificación completa sin errores. Preferencias observadas: master 0,59; música 0,55; efectos 0,80; ambiente 0,45; UI 0,65. La escena está rotulada como prueba preparada: rival quieto, un corte empieza con mosquito adherido y otro con aliado aturdido. Las acciones posteriores usan Client y autoridad reales. El rescate medido duró 8,63 s desde 34,43 s restantes, a una tasa observada de 3,95×. No representa una partida espontánea ni prueba de balance.

Se detectó y corrigió un nivel insuficiente de ayuda respecto a la base musical: ganancia del player −15→+5 dB y recuperación espacial −14→−4 dB. SFX136 y música46 se repitieron después. La ventana de ayuda del PCM real pasó de −41,22 a −39,13 dB RMS y la recuperación de −46,08 a −44,72; los primeros ocho segundos permanecieron idénticos. La señal aislada de ayuda se acerca a −43,3 dB RMS, frente a aproximadamente −41,7 de base musical. La palmada destaca 10,07 dB de pico sobre el fondo previo y la música baja durante el golpe. Es una mezcla deliberadamente suave con mejor feedback; estas medidas no certifican escucha perceptual ni audibilidad en cualquier parlante. El master de fábrica0,60 sólo difiere0,15 dB del perfil0,59, y no se usa esa diferencia como explicación del nivel anterior.

## Fuente, arte y audio

Reglas 1622, sala 321, mapas 431, rutas 303, locomoción 7325, defensa 6795, concentración 145, aturdimiento/ayuda 297, plazos 59, práctica 79, invitación 88, orden de red 11, conexión 23, privacidad 37, orientación/contacto 306, música 46, SFX 136, UI nativa 146 y migración 28 pasan. Cosméticos y geometría pasan. Las preferencias originales se restauran byte a byte. El codec aislado pasó 114 y sigue fuera de Network/EOS.

HUD: ayuda plegada con F1, indicadores contextuales y alertas críticas visibles. La comparación de rectángulos de paneles en seis estados y dos resoluciones pasó 36/36; ocupación de esos paneles baja 53–61,3%. Esa cifra no mide toda la oclusión ni rendimiento y procede de fixtures de interfaz.

Fuentes editables Blender, GLB y generadores en art_source. Humano de pijama con pantuflas/gorro al iniciar un perfil nuevo; perfiles existentes preservados. Editor con siete categorías, tarjetas, giro y enfoque. Humano visible por defecto: 38.946 triángulos, ligeramente por encima de la guía inicial; fuente sin triángulos degenerados y nueve clips validados contra postura compartida (error máximo 0,00000013 m). Postura relajada sólo en lobby/editor; las superficies de contacto del gameplay mantienen el contrato autoritativo. Mosquito con 21 huesos y diez clips. Casa de 16 habitaciones y dos plantas; 18 modelos domésticos editables.

48 OGG (9,41 MB), 35 efectos/ambientes, seis acentos, seis stems y una pieza tranquila. Tema principal original «Pasos de puntillas», 104 BPM, y variante calma «La casa bosteza», 80 BPM. Samples instrumentales VSCO2 Community CC0 con procedencia/licencia; Foley y zumbidos sintetizados. Fuentes incluyen WAV, MIDI, eventos y scripts. Buses independientes de música, efectos, ambiente y UI; las capas no revelan información enemiga privada. Licencias de audio visibles también junto al ejecutable.

## Medición local

RTX 3060 Ti, 1280×720, misma escena y fixture, vsync desactivado; 60 frames de calentamiento y 120 muestras por caso. Un personaje visible usa autoridad válida 1v1; 16 usa 4 humanos/12 mosquitos. Comparación entre EXE release 0.5 y release 0.6, separados de la medición provisional de desarrollo.

| Visibles / trabajo | 0.5 mediana ms | 0.6 mediana ms | 0.6 p90 ms | Draw calls mediana |
|---|---:|---:|---:|---:|
{table}

Memoria de vídeo 0.6: 25,3–43,1 MiB; memoria estática no disponible en estos EXE release (monitor devuelve 0). El fixture mide una vista local fija y bots/autoridad en la variante host, no una ronda completa, WAN ni requisitos mínimos. No certifica otros equipos.

## Incidencias conservadas

- Al mover la mesa del living, el primer lugar invadió una ruta; quedó corregido y rutas303/303 pasa. Los fixtures antiguos de mesa se actualizaron a su nueva posición; aterrizar y bajar del mueble pasan.
- El runner intentó malla y captura de ratón con el backend dummy headless, que no ofrece esas capacidades. Se clasificaron como pruebas nativas: malla166/166 y UI146/146 pasan; el build ahora las ejecuta con renderizador.
- El primer hosting del EXE no logró reservar el puerto de su fixture después del cierre; repetición sin cambios13/13 y sin stderr. Causa del primer fallo no demostrada; no se presenta como corrección de runtime.
- El ensayo de cámara contaba frames de renderizado y a FPS libres no dio tiempo al recentrado. A60FPS pasó6/6; se conserva la diferencia y se especifica la frecuencia requerida del fixture. Las reglas de giro se prueban además a20/60Hz.
- El wrapper de red interpretó LASTEXITCODE null como fallo cuando el script había pasado. Se reprodujo en shell fresca y se añadió exit0 explícito al helper. Los informes completos originales se reauditaron con todas las aserciones, no se aceptaron resultados parciales.
- Los fallos de fixtures documentados en0.5 siguen archivados con su entrega; no se atribuyen causas nuevas sin evidencia.

## Límites

Conexión directa ENet con dirección alcanzable. Internet integrado/EOS sigue pendiente de adaptación, configuración y pruebas entre casas; no se incluye SDK, relay ni credenciales EOS. No se certifican balance humano, diversión, accesibilidad completa, hardware mínimo ni tolerancia a pérdida real de Internet. Sangre/Tareas conservan aturdimiento35s y ayuda4× no acumulable; sólo Supervivencia elimina definitivamente. Sin cambios de duración por golpes repetidos ni victoria instantánea por quedar todos aturdidos.
'''
    (ROOT/'distribution/PRUEBAS.md').write_text(report, encoding='utf-8')
    (ROOT/'distribution/BUILD.txt').write_text(f'Let me sleep {VERSION} — Windows x86_64\nGodot4.5.2 / Compatibility OpenGL / protocolo6 / invitaciónDD3\nEjecutable: Let-me-sleep.exe\nSHA256: {sha(EXE)}\nCommit de código: {CODE}\nPaquetes: Let-me-sleep-{VERSION}-Windows.zip / Let-me-sleep-{VERSION}-fuentes.zip\nHashes ZIP y commit de documentación: MANIFIESTO-{VERSION}.json externo.\nEvidencias y límites: PRUEBAS.md.\nEOS no integrado; conexión directa requiere dirección alcanzable.\nFecha: 2026-09-07. Entregas anteriores conservadas.\n',encoding='utf-8')
    for p in (ROOT/'distribution').iterdir():
        if p.is_file(): shutil.copy2(p, EXE.parent/p.name)
    print(json.dumps({'network':network,'exe':artifact(EXE)},indent=2))
else:
    commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    packages=[OUT/f'Let-me-sleep-{VERSION}-{kind}.zip' for kind in ('Windows','fuentes')]
    for p in packages:
        with zipfile.ZipFile(p) as z:
            assert z.testzip() is None, p
            if 'Windows' in p.name:
                item=next(n for n in z.namelist() if n.endswith('/Let-me-sleep.exe'))
                assert hashlib.sha256(z.read(item)).hexdigest().upper()==sha(EXE)
        p.with_suffix(p.suffix+'.sha256.txt').write_text(sha(p)+'  '+p.name+'\n',encoding='utf-8')
    network=read(WORK/'release06-network-summary.json')
    record={'product':'Let me sleep','version':VERSION,'protocol':6,'invitation':{'prefix':'DD3','format':1},'platform':'Windows x86_64','engine':'Godot4.5.2','generated_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'code_commit':CODE,'source_and_docs_commit':commit,'artifacts':[artifact(p) for p in packages+[EXE,OUT/'0.6-preview/Let-me-sleep-0.6-gameplay-y-sonido.mp4']],'delivery_exe_checks':{'native_practice':73,'native_help':28,'native_audio_gameplay_demo':25,'enet_invitation_clients':2},'pre_audio_candidate_checks':{'exe_sha256':'4B47820C2238B12482CE6D38C557B89F701AA04C30C68129FE660E2818E902AE','native_hosting':13,'native_camera_60fps':6,'native_mesh':166,'client_contacts':86,'native_house':218,'network_scenarios':6,'unchanged_runtime_except_two_audio_gains':True},'network':network,'performance':read(WORK/'performance06-audiofinal-isolated.json'),'rules':{'stun_seconds':35,'help_total_rate':4,'helpers_stack':False,'repeated_hits_reset':False,'survival_only_elimination':True},'evidence':'0.6-validacion','preview':'0.6-preview','guide':f'Let-me-sleep-{VERSION}-Windows/LEEME.html','limits':['Direct ENet only; integrated EOS/WAN pending','ENet processes on one PC; no independent network test','Controls video uses explicit staged setups and real subsequent inputs/audio','No human balance or minimum hardware certification','Initial hosting fixture could not reserve port; unchanged repeat passes, cause undetermined'],'preserved_versions':['0.1.0','0.2.0','0.3.0','0.4.0','0.5.0']}
    write(OUT/f'MANIFIESTO-{VERSION}.json',record)
    print(json.dumps(record['artifacts'],indent=2))
