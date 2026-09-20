from pathlib import Path
import json,re,html,zipfile
p=Path(__file__).resolve().parent
qs=[]
for name in ['producto','gameplay','arte','catalogo','online','audio']:
 qs+=json.loads((p/'data'/f'{name}.json').read_text(encoding='utf-8-sig'))
fixed=json.loads((p/'data/cuestionario.json').read_text(encoding='utf-8'))['fixed']
order=['Identidad y experiencia','Jugabilidad','Modos','Reglas por cerrar','Inventario y herramientas','Bots','Arte y personajes','Catálogo y alcance exacto','Personalización','Animación y cámaras','Mapas y ambiente','Interfaz y accesibilidad','Online y sesiones','Voz y sonido','Música y efectos','Entrega y validación']
assert len(qs)==150 and len({q['id'] for q in qs})==150
ids={q['id'] for q in qs}
for q in qs:
 assert [o['id'] for o in q['options']]==['A','B','C'],q['id']
 assert q['section'] in order
 assert q['type'] in ['single','multi'] and q['priority'] in ['decisiva','detalle']
 assert q['recommended'] in [None,'A','B','C']
 assert q['dependsOn'] is None or q['dependsOn'] in ids
 assert all(q.get(k) for k in ['title','why','example','source'])
s=(p/'template.html').read_text(encoding='utf-8').replace('__COUNT__',str(len(qs))).replace('__FIXED__',''.join('<li>'+html.escape(f)+'</li>' for f in fixed)).replace('__DATA__',json.dumps(qs,ensure_ascii=False).replace('</',r'<\/'))
(p/'index.html').write_text(s,encoding='utf-8')
(p/'data/cuestionario.json').write_text(json.dumps({'version':1,'questions':qs,'fixed':fixed},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
md=['# Let Me Sleep — 150 decisiones para definir v0.2.0','','Versión 1 · 20/09/2026. Ninguna opción está aprobada ni preseleccionada. Las cifras propuestas se convierten en balance inicial sólo tras tu elección y pruebas.','','**Cómo responder:** `P01=A`, `J21=A+B` cuando admite varias, o `A01=Mi alternativa: ...`. También `J02=CEO, priorizá sesiones cortas`. Dejar pendiente no aprueba una recomendación. Una nota sola tampoco.','','## Acuerdos conservados','']+['- '+f for f in fixed]+['','## Bocetos de comparación','','Exploraciones generadas con imagegen; no son capturas de Unity, arte aprobado ni modelos Higgsfield. La geometría final deberá respetar los mapas existentes.','','![A/B/C de personajes](visuales/personajes-abc.png)','','A: facetado; B: suave; C: mixto pintado. Usar con A01/A02.','','![A/B/C de ambiente](visuales/iluminacion-abc.png)','','A: noche cálida; B: luna fría; C: crepúsculo. Usar con A23/A24; Isla sigue diurna. HUD y cámara tienen esquemas adicionales en index.html.','']
ans=['# Plantilla de respuestas — Let Me Sleep v0.2.0','','Cambiar PENDIENTE por A, B, C, A+B (sólo multiselección), MI ALTERNATIVA: ... o DELEGAR CEO: criterios. No hace falta responder todo de una vez.','']
for sec in order:
 md+=['## '+sec,''];ans+=['## '+sec,'']
 for q in [x for x in qs if x['section']==sec]:
  md+=['### '+q['id']+' · '+q['title'],'',f"**{q['priority'].capitalize()} · {'Elegí varias' if q['type']=='multi' else 'Elegí una'} · {q['basis']}.**",'',q['why'],'','**Ejemplo:** '+q['example'],'']
  for o in q['options']:md+=['- **'+o['id']+' · '+o['label']+'** — '+o['detail']]
  md+=['','**Tu respuesta:** ___  | **Notas o alternativa:** ___','']
  if q['recommended']:md+=['Propuesta del CEO, sin aprobar: '+q['recommended']+'.','']
  if q['dependsOn']:md+=['Relacionada con '+q['dependsOn']+'.','']
  md+=['Contexto: `'+q['source']+'`. El texto histórico no crea una orden nueva.','']
  ans+=[f"- {q['id']} = PENDIENTE — {q['title']}"]
 ans+=['']
(p/'CUESTIONARIO-COMPLETO.md').write_text('\n'.join(md),encoding='utf-8')
(p/'RESPUESTAS-PLANTILLA.md').write_text('\n'.join(ans),encoding='utf-8')
r=(p/'README.md').read_text(encoding='utf-8').replace('140 preguntas, 420 opciones','150 preguntas, 450 opciones');(p/'README.md').write_text(r,encoding='utf-8')
(p/'ui-check.js').write_text(re.findall(r'<script>(.*?)</script>',s,re.S)[0],encoding='utf-8')
with zipfile.ZipFile(p/'LetMeSleep-cuestionario-v1.zip','w',zipfile.ZIP_DEFLATED) as z:
 for name in ['index.html','CUESTIONARIO-COMPLETO.md','RESPUESTAS-PLANTILLA.md','README.md','visuales/personajes-abc.png','visuales/iluminacion-abc.png']:
  z.write(p/name,'LetMeSleep-cuestionario/'+name)
print(json.dumps({'questions':len(qs),'options':len(qs)*3,'sections':len(order),'critical':sum(q['priority']=='decisiva' for q in qs)},ensure_ascii=False))
