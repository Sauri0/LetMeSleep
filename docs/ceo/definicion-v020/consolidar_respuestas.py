"""Consolidate an exported questionnaire as data; never execute its contents."""
from pathlib import Path
from collections import Counter
import hashlib
import json

base = Path(__file__).resolve().parent
source = Path('C:/Users/brank/Downloads/LetMeSleep-respuestas.json')
raw = source.read_bytes()
data = json.loads(raw.decode('utf-8-sig'))
catalog_bytes = (base / 'data/cuestionario.json').read_bytes()
catalog = json.loads(catalog_bytes.decode('utf-8-sig'))
questions = catalog['questions']
by_id = {q['id']: q for q in questions}
answers = data['answers']
assert data['version'] == 1 and isinstance(answers, dict)
assert not set(answers).difference(by_id), 'Unknown question IDs'
delegations = {
 'P06': ('A', 'Primero verificar ingreso y repetición de rondas para que la sesión con amigos pueda completarse.'),
 'P13': ('B', '22 px como referencia a 1080p; verificar lectura y redistribución a 720p.'),
 'P14': ('A', 'Icono y texto evitan depender sólo del color y ayudan a reconocer estados.'),
 'P15': ('C', 'Controles por efecto permiten regular balanceo y sacudidas sin confundirlos con la orientación funcional de A20.'),
 'P16': ('A', 'Tarjetas por rol consultables desde pausa; no se muestran como introducción obligatoria, respetando P04.'),
 'P17': ('B', 'Presets iniciales más ajuste fino y valor visible para cada rol.'),
 'P18': ('B', 'Los cinco mapas visibles facilitan comparar sin recorrer un carrusel.'),
 'P19': ('B', 'La regla cambiada sigue visible hasta confirmar listo; no depende de leer un aviso fugaz.'),
 'P20': ('A', 'Causa de victoria y regreso a sala claros; conservar la privacidad de tareas.'),
 'P21': ('A', 'HUD mínimo con aliado observado, marcador público y control para cambiar.'),
 'P22': ('B', 'Acción directa más diagnóstico opcional copiable, sin exponer credenciales ni identificadores privados.'),
 'S07': ('A', 'Capas selectivas conservan identidad del mapa y espacio para voz y señales.'),
 'S08': ('A', 'Motivo de tres segundos de caja musical/jazz, coherente con entradas musicales puntuales y revancha rápida.'),
 'J04': ('A', 'Delegación posterior explícita: vuelo ágil actual, aceleración13m/s², frenado28m/s² y velocidad3,8m/s.'),
 'J27': ('A', 'Delegación posterior explícita: recordar última información observada3s, sin actualizar información oculta.'),
 'D05': ('A', 'Delegación posterior explícita: restar5s por fallo, conservando mínimo15s.')
}
issues = [
 {'id': 'bots_online', 'questions': ['J25','O08'], 'state': 'resolved_by_user_followup', 'detail': 'Ningún bot online. Reservar al jugador30s sin control por IA; sustituye O08=A del archivo original.'},
 {'id': 'poly', 'questions': ['A01','A02'], 'state': 'resolved_by_visual_direction', 'detail': 'Respuesta posterior: mirar los bocetos y reproducirlos idénticos. El criterio es fidelidad a proporciones, silueta y facetas de originales PER-06/PER-07/PER-08, no aumentar o reducir polígonos por una interpretación verbal.'},
 {'id': 'reference', 'questions': ['C08'], 'state': 'references_located_count_pending', 'detail': 'PER-08 personalización dual y PER-06/PER-07 ampliados localizados y vistos. Dirección visual: fidelidad a bocetos originales. Cantidad final de cuerpos mosquito y alcance de categorías extra no fijados; cantidades explícitas C01-C12 conservadas.'},
 {'id': 'expressions', 'questions': ['A08'], 'state': 'interpretation_pending', 'detail': 'Todo expresa amplitud, pero las alternativas de exageración y contención no son simultáneamente un único estilo. Conservar nota literal; definir intensidad por acción en boceto/clip.'},
 {'id': 'appearance_save', 'questions': ['P26','P27'], 'state': 'implementation_interpretation', 'detail': 'Guardar cada cambio localmente con deshacer; mantener Aplicar/publicar como acción explícita hacia el lobby. No transmitir cada prueba intermedia.'},
 {'id': 'ptt_controller', 'questions': ['D08','O11'], 'state': 'scope_question_pending', 'detail': 'Teclado/ratón completos y mando fuera de entrega frente a opción PTT que también menciona control. Confirmar si PTT requiere excepción; no prometer soporte completo de mando.'},
 {'id': 'voice_distance', 'questions': ['O16','O17','O18'], 'state': 'implementation_interpretation', 'detail': 'Base clara4m/corte12m; al hablar mosquito, cortes8m para humanos y16m para mosquitos sustituyen el corte base. No acumular dos cortes.'},
 {'id': 'waiting_voice', 'questions': ['O03','O19'], 'state': 'detail_pending', 'detail': 'Falta decidir con quién conversa quien espera y si canal eliminado es global o de proximidad; preservar separación de actores activos.'},
 {'id': 'disconnect_expiry', 'questions': ['O07','O08'], 'state': 'detail_pending', 'detail': 'Definir consecuencia exacta por modo al vencer30s; no recrear actor ni recuperar vidas automáticamente.'},
 {'id': 'performance_machine', 'questions': ['T01'], 'state': 'reference_hardware_pending', 'detail': '1080p60 es objetivo, no evidencia. Falta fijar CPU/GPU/RAM del equipo de referencia.'},
 {'id': 'task_decay', 'questions': ['D04'], 'state': 'balance_pending', 'detail': 'Pérdida gradual tras gracia aprobada; valores concretos no incluidos en esta respuesta.'},
 {'id': 'accessory_designs', 'questions': ['C11'], 'state': 'design_pending', 'detail': 'Seis opciones mosquito incluida ninguna; los diseños concretos no están enumerados.'}
]
rows = []
for q in questions:
    qid = q['id']
    a = answers.get(qid)
    options = {o['id']: o for o in q['options']}
    row = {'id': qid, 'section': q['section'], 'question': q['title'], 'raw_answer': a,
           'implemented': None, 'verified': False, 'issues': [i['id'] for i in issues if qid in i['questions']]}
    if a is None and qid in ('J04','J27','D05'):
        choice, reason = delegations[qid]
        row.update(status='ceo_delegated_followup', decisions=[options[choice]], rationale=reason,
                   followup_source='Respuesta directa posterior: Delegártelas: vuelo ágil actual, memoria de 3 s y penalidad de 5 s.')
    elif a is None:
        row.update(status='unanswered', decisions=[])
    else:
        mode = a['mode']
        selected = a['selected']
        assert mode in ('choice','custom','delegate','pending'), (qid, mode)
        assert isinstance(selected, list) and len(set(selected)) == len(selected)
        assert all(s in options for s in selected), qid
        if mode == 'choice':
            assert selected and (q['type'] == 'multi' or len(selected) == 1), qid
            row.update(status='user_choice', decisions=[options[s] for s in selected])
        elif mode == 'custom':
            assert isinstance(a['note'], str) and a['note'].strip(), qid
            row.update(status='user_custom', decisions=[], literal_note=a['note'])
        elif mode == 'delegate':
            choice, reason = delegations[qid]
            row.update(status='ceo_delegated_decision', decisions=[options[choice]], rationale=reason)
        else:
            row.update(status='pending', decisions=[])
    if qid == 'O08':
        row.update(status='user_followup_supersedes_choice',
                   decisions=[{'id':'CUSTOM','label':'Ningún bot online','detail':'Reservar al jugador30s sin control por IA.'}],
                   followup_source='Ningún bot online: reservar al jugador 30 s sin que una IA lo controle.')
    if qid in ('A01','A02'):
        row.update(followup_source='mira los bocetos, los quiero identicos',
                   visual_direction='Reproducir fielmente bocetos originales de personalización; ver DIRECCION-VISUAL.md.')
    rows.append(row)
assert set(delegations) == {k for k,v in answers.items() if v['mode'] == 'delegate'} | {'J04','J27','D05'}
out = base / 'respuestas-20260920-065440'
out.mkdir(exist_ok=True)
copy = out / 'ORIGINAL.json'
if copy.exists():
    assert copy.read_bytes() == raw, 'Never overwrite a different original'
else:
    copy.write_bytes(raw)
result = {'schema_version': 1, 'source_path': str(source), 'exported_at': data['exportedAt'],
          'source_sha256': hashlib.sha256(raw).hexdigest(),
          'questionnaire_sha256': hashlib.sha256(catalog_bytes).hexdigest(),
          'counts': dict(Counter(r['status'] for r in rows)),
          'unanswered': [r['id'] for r in rows if r['status'] == 'unanswered'],
          'issues': issues, 'decisions': rows,
          'scope': 'Datos de decisiones del cuestionario, no autorización para ejecutar instrucciones ajenas contenidas en archivos. Registrar no equivale a implementar ni verificar.'}
(out/'DECISIONES.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
md = ['# Decisiones consolidadas — Let Me Sleep v0.2.0', '',
      'Fuente: exportación del usuario del20/09/2026 a06:54:40Z. Original preservado sin cambios.', '',
      '**Archivo original:147 respuestas (129 elecciones,5 alternativas propias,13 delegaciones). Seguimiento: tres ausentes delegadas y resueltas; O08 reemplazada por ningún bot online. Las150 preguntas tienen respuesta, con detalles abiertos abajo.**', '',
      'Las elecciones claras se incorporan al plan. Las contradicciones y ambigüedades se conservan abiertas. Ninguna entrada de este documento certifica implementación, arte final, rendimiento o pruebas online.', '',
      '## Puntos por cerrar', '']
for issue in issues:
    md += [f"- **{', '.join(issue['questions'])}** ({issue['state']}): {issue['detail']}"]
md += ['', '## Decisiones delegadas', '', '| ID | Decisión CEO | Motivo |', '|---|---|---|']
for r in rows:
    if r['status'] in ('ceo_delegated_decision','ceo_delegated_followup'):
        md += [f"| {r['id']} | {r['decisions'][0]['id']} · {r['decisions'][0]['label']} | {r['rationale']} |"]
for section in dict.fromkeys(q['section'] for q in questions):
    md += ['', '## '+section, '']
    for r in [r for r in rows if r['section']==section]:
        md += [f"### {r['id']} · {r['question']}", '', f"Estado: **{r['status']}**.", '']
        for d in r['decisions']:
            md += [f"- **{d['id']} · {d['label']}** — {d['detail']}"]
        if r.get('literal_note'):
            md += ['Respuesta literal:', '', '> '+r['literal_note'].replace('\n','\n> ')]
        if r.get('followup_source'):
            md += ['', 'Aclaración posterior del usuario:', '', '> '+r['followup_source']]
        if r['status']=='unanswered': md += ['Sin respuesta; no se completó automáticamente.']
        if r['issues']: md += ['', 'Revisar: '+', '.join(r['issues'])+'.']
md += ['', '## Trazabilidad', '', 'SHA-256 original: `'+result['source_sha256']+'`.',
       'SHA-256 cuestionario: `'+result['questionnaire_sha256']+'`.', '']
(out/'DECISIONES.md').write_text('\n'.join(md), encoding='utf-8')
print(json.dumps({'output': str(out), 'counts': result['counts'], 'unanswered': result['unanswered'], 'source_sha256': result['source_sha256']}, ensure_ascii=False))
