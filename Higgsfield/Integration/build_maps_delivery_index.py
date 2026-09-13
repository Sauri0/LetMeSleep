"""Build a local art index from verified files. No source edits, browser network or native apps."""
import hashlib
import html
import json
import argparse
from integration_receipts import validate_receipt, integration_status, receipt_label
import struct
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit

ROOT = Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas')
HERE = Path(__file__).resolve().parent
INDEX = ROOT / 'ENTREGA.html'
MANIFEST = ROOT / 'ENTREGA.manifest.json'
records = {}
parser_args=argparse.ArgumentParser(description=__doc__)
parser_args.add_argument('--catalog-receipt',type=Path)
parser_args.add_argument('--scene-receipt',type=Path)
parser_args.add_argument('--loading-receipt',type=Path)
args=parser_args.parse_args()

def asset(relative, label, role, recommended=False):
    path = (ROOT / relative).resolve()
    assert path.is_relative_to(ROOT.resolve()) and path.is_file(), relative
    content = path.read_bytes()
    record = dict(path=path.relative_to(ROOT.resolve()).as_posix(), href=quote(path.relative_to(ROOT.resolve()).as_posix(), safe='/'),
                  label=label, role=role, recommended=recommended, bytes=len(content), sha256=hashlib.sha256(content).hexdigest())
    if path.suffix.lower() == '.png':
        assert content[:8] == b'\x89PNG\r\n\x1a\n'
        record.update(zip(('width','height'),struct.unpack('>II',content[16:24])))
    records[record['path']] = record
    return record

specs = [
    ('isla','Isla del Laguito','01-isla','HF_MAP_01_isla','hf-isla-del-laguito-v2',
     'Un lago interior, senderos y una cabaña junto al agua.',
     'HF_MAP_01_isla.glb','HF_MAP_01_isla_UNITY.fbx','HF_MAP_01_isla_overview.png','HF_MAP_01_isla_interior.png',
     'Interior de la cabaña','Fuentes de arte recomendadas; la integración en Unity se verifica por separado.'),
    ('casa','Casa del Patio','02-casa','HF_MAP_02_casa','hf-casa-del-patio-v1',
     'Una casa de dos plantas y su patio, iluminados de noche.',
     'HF_MAP_02_casa_UNITY.glb','HF_MAP_02_casa_UNITY.fbx','HF_MAP_02_casa_overview.png','HF_MAP_02_casa_closeup.png',
     'Interior de la casa','La copia Blender ajustada sitúa Spawn_Human_03.001 en Z = 0,2841115 m, equivalente a la altura Y documentada en Unity (aumento de 0,0341115 m). Los GLB y FBX conservan la altura original de 0,25 m: no se reexportaron ni reimportaron. El ajuste de un marcador no certifica equivalencia portable completa.'),
    ('campamento','Campamento Pinar','03-campamento','HF_MAP_03_campamento','hf-campamento-pinar-v2',
     'Tiendas, fogata y caminos entre pinos, un arroyo y un lago.',
     'NormalsV2/HF_MAP_03_campamento_UNITY_V2.glb','NormalsV2/HF_MAP_03_campamento_UNITY_V2.fbx',
     'renders/HF_MAP_03_campamento_overview.png','renders/HF_MAP_03_campamento_closeup.png','Detalle de las tiendas',
     'La fuente editable y los exports NormalsV2 incluyen la corrección técnica de normales. Las imágenes son vistas de arte de Blender; la revisión del mapa jugable sigue separada.'),
    ('yate','Yate a la Deriva','04-yate','HF_MAP_04_yate','hf-yate-a-la-deriva-v3',
     'Un yate compacto con cubiertas transitables e interiores amueblados.',
     'NormalsV3/HF_MAP_04_yate_UNITY_V3.glb','NormalsV3/HF_MAP_04_yate_UNITY_V3.fbx','HF_MAP_04_yate_overview.png','HF_MAP_04_yate_closeup.png',
     'Salón y cocina','Fuente y exports NormalsV3: las 32.000 caras del océano ahora apuntan hacia arriba, conservando geometría y morphs. Receta v3 con océano GPU y vidrio transparente. Integración Unity en verificación.'),
    ('puerto','Puerto del Faro','05-pueblo','HF_MAP_05_pueblo','hf-puerto-del-faro-v1',
     'Tres casas, un taller, muelles y un faro con escalera interior.',
     'HF_MAP_05_pueblo.glb','HF_MAP_05_pueblo_UNITY.fbx','HF_MAP_05_pueblo_overview.png','HF_MAP_05_pueblo_closeup.png',
     'Interior de una vivienda','Integración candidata con océano GPU. El archivo se llama pueblo; el nombre del mapa es Puerto del Faro.'),
]
concept_manifest = asset('Bocetos/delivery-manifest.json','Manifiesto original de bocetos','evidence')
concepts = json.loads((ROOT/concept_manifest['path']).read_text())
assert sorted(item['index'] for item in concepts) == list(range(1,21))
concept_by_index = {item['index']:item for item in concepts}
concept_labels = ['Vista general','Distribución conceptual','Recorridos e interiores','Piezas y materiales']
blend_overrides = {'casa':'UnityAdjustedSource/HF_MAP_02_casa_UNITY_ADJUSTED.blend',
                   'yate':'NormalsV3/HF_MAP_04_yate_NORMALS_V3.blend'}
unity_previews=['01-isla/UnityFinal/unity-overview.png','02-casa/UnityFinal/unity-overview.png',
                '03-campamento/UnityFinalV2/unity-overview.png','04-yate/UnityFinalV3/unity-overview.png',
                '05-pueblo/UnityPresentationProvisional/unity-overview.png']
yate_adjustment_path=ROOT/'04-yate/UnityAdjustedSource/adjustment-receipt.json'
yate_adjustment=None
if yate_adjustment_path.is_file():
    candidate=json.loads(yate_adjustment_path.read_text())
    if candidate.get('status')=='PASS_EXCLUSIVE_YATE_COPY_STORAGE_AND_BULKHEAD_READBACK_LIVE_RESTORED':
        assert {p['name'] for p in candidate['parts']}=={'YATE_DeckStorage_01','YATE_DeckStorage_Lid_01','YATE_Lower_EndBulkhead_-11.6'}
        assert Path(candidate['source']).resolve()==(yate_adjustment_path.parent/'HF_MAP_04_yate_UNITY_ADJUSTED.blend').resolve()
        assert hashlib.sha256(Path(candidate['source']).read_bytes()).hexdigest()==candidate['sourceSha256']
        yate_adjustment=candidate;blend_overrides['yate']='UnityAdjustedSource/HF_MAP_04_yate_UNITY_ADJUSTED.blend'
puerto_adjustment_path=ROOT/'05-pueblo/UnityAdjustedSource/adjustment-receipt.json'
puerto_adjustment=None
if puerto_adjustment_path.is_file():
    candidate=json.loads(puerto_adjustment_path.read_text(encoding='utf-8'))
    if candidate.get('status')=='PASS_EXCLUSIVE_PUERTO_COPY_SECOND_STEP_BEVEL_READBACK_LIVE_RESTORED':
        assert candidate['objectName']=='Lighthouse_Approach_StoneStairway'
        assert candidate['geometry']['bevelMeters']==.03 and candidate['geometry']['unchangedTriangles']==468
        assert candidate['objectTransformPreserved'] is True and candidate['newExportsOrUnityImport'] is False
        assert Path(candidate['source']).resolve()==(puerto_adjustment_path.parent/'HF_MAP_05_pueblo_UNITY_ADJUSTED.blend').resolve()
        assert hashlib.sha256(Path(candidate['source']).read_bytes()).hexdigest()==candidate['sourceSha256']
        assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==sha for p,sha in candidate['preservedFiles'].items())
        puerto_adjustment=candidate;blend_overrides['puerto']='UnityAdjustedSource/HF_MAP_05_pueblo_UNITY_ADJUSTED.blend'
maps = []
for index,(slug,title,folder,stem,map_id,description,glb,fbx,overview,detail,detail_label,note) in enumerate(specs):
    entry=dict(slug=slug,title=title,mapId=map_id,description=description,source='Higgsfield / Scene Builder',
               artStatus='Arte terminado',unityStatus='Integración Unity en verificación',portableUnityEquivalence='not_claimed',note=note)
    blend=blend_overrides.get(slug,f'{stem}.blend')
    entry['sources']=[asset(f'{folder}/{blend}','Fuente ajustada · Blender' if blend.startswith('UnityAdjustedSource/') else 'Fuente editable · Blender','blend',True),
                      asset(f'{folder}/{glb}','Modelo · GLB','glb',True),asset(f'{folder}/{fbx}','Export limpio · FBX','fbx',True)]
    entry['previews']=[asset(f'{folder}/{overview}','Vista general del arte','overview',True),
                       asset(f'{folder}/{detail}',detail_label,'detail',True)]
    entry['unityCapture']=asset(unity_previews[index],
        'Captura Unity · presentación provisional' if slug=='puerto' else 'Captura Unity · vista general revisada','unity_capture',True)
    entry['unityCaptureScope']='Revisión de imagen estática; no certifica interiores ocultos, física, navegación ni rendimiento.'
    entry['concepts']=[]
    for offset,label in enumerate(concept_labels):
        number=index*4+offset+1;original=concept_by_index[number]
        relative=Path(original['path']).resolve().relative_to(ROOT.resolve()).as_posix()
        item=asset(relative,f'{number:02} · {label}','concept',True)
        assert item['sha256']==original['sha256'].lower() and item['bytes']==original['bytes'], relative
        item['conceptIndex']=number;entry['concepts'].append(item)
    maps.append(entry)

# Exact source/native coordinates distinguish a corrected Unity value from an actual offset.
source_audit=asset('02-casa/scene-audit.json','Auditoría de la fuente de Casa','evidence')
native_review=asset('02-casa/UnityFinal/unity-native-review.json','Revisión nativa de Casa','evidence')
audited=json.loads((ROOT/source_audit['path']).read_text())
native=json.loads((ROOT/native_review['path']).read_text())
old=next(o for o in audited['objects'] if o['name']=='Spawn_Human_03.001')['matrix_world'][2][3]
new=next(o for o in native['humanSpawns'] if o['name']=='Spawn_Human_03.001')['position']['y']
assert abs(old-.25)<1e-6 and abs(new-.2841115)<1e-6
adjustment=asset('02-casa/UnityAdjustedSource/marker-adjustment.json','Casa · comprobante del marcador ajustado','evidence')
receipt=json.loads((ROOT/adjustment['path']).read_text())
assert receipt['status']=='PASS_EXCLUSIVE_CASA_COPY_MARKER_DELTA_AND_SAVED_EMPTY_READBACK'
assert receipt['sourceSha256']==maps[1]['sources'][0]['sha256']
assert abs(receipt['adjustedBlenderXYZ'][2]-new)<1e-7 and receipt['originalBlenderXYZ'][2]==old
assert receipt['adjustedBlenderXYZ'][:2]==receipt['originalBlenderXYZ'][:2]
for path,sha in receipt['preservedFiles'].items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==sha, path
maps[1]['unitySourceDelta']=dict(marker='Spawn_Human_03.001',originalSourceBlenderZ=old,exportsExpectedUnityY=old,
    adjustedSourceBlenderZ=receipt['adjustedBlenderXYZ'][2],observedUnityY=new,actualOffsetY=new-old,
    status='Adjusted exclusive blend marker verified; original GLB/FBX unchanged; no reimport or full portable equivalence claimed',
    evidence=[source_audit,native_review,adjustment])
maps[1]['evidence']=[adjustment]
duplicate=asset('Bocetos/05-detail.png','Copia duplicada del boceto 05','duplicate',False)
assert duplicate['sha256']==maps[1]['concepts'][0]['sha256']

history=[
    asset('01-isla/UnityReview/unity-native-review.json','Isla · revisión anterior','history'),
    asset('03-campamento/UnityFinal/unity-native-review.json','Campamento v1 · evidencia anterior a corregir normales','history'),
    asset('04-yate/UnityRecipe/hf-yate-a-la-deriva-v1.recipe.json','Yate v1 · candidato CPU fallido en importación','history'),
    asset('04-yate/UnityRecipeV2/hf-yate-a-la-deriva-v2.recipe.json','Yate v2 · reemplazado por normales del océano invertidas','history'),
    asset('02-casa/HF_MAP_02_casa.blend','Casa · fuente original con marcador a 0,25 m','history'),
    asset('05-pueblo/UnityRecipe/hf-puerto-del-faro-v1.recipe.json','Puerto · candidato CPU reemplazado antes de entrega','history')]
extras=[asset('Bocetos/INDICE.md','Notas de interpretación de los bocetos','evidence'),
        asset('03-campamento/NormalsV2/normals-repair.json','Corrección de normales de Campamento','evidence'),
        asset('04-yate/NormalsV3/normals-repair.json','Corrección de normales de Yate','evidence'),
        asset('04-yate/NormalsV3/glb-winding-readback.json','Yate · verificación independiente del GLB','evidence'),
        asset('04-yate/UnityRecipeV3/hf-yate-a-la-deriva-v3.recipe.json','Yate · receta GPU v3','evidence')]
maps[3]['evidence']=extras[2:]
if yate_adjustment:
    maps[3]['note']='Copia Blender ajustada: baúl y tapa desplazados; mamparo rebajado dentro del corredor de la escalera. Los GLB y FBX NormalsV3 conservan esos elementos originales: no se reexportaron ni reimportaron. El océano mantiene las normales corregidas. Estos ajustes no certifican equivalencia portable completa.'
    maps[3]['unitySourceAdjustment']=yate_adjustment
    maps[3]['evidence'].append(asset('04-yate/UnityAdjustedSource/adjustment-receipt.json','Yate · recibo de copia ajustada','evidence'))
    history.append(asset('04-yate/NormalsV3/HF_MAP_04_yate_NORMALS_V3.blend','Yate · NormalsV3 anterior a ajustes de baúl y mamparo','history'))
if puerto_adjustment:
    maps[4]['note']='Copia Blender ajustada: bisel de 30 mm en el canto superior frontal del segundo peldaño exterior del faro, con material conservado. La superficie recortada sigue el plano final de Unity; su triangulación difiere. Los GLB y FBX conservan el peldaño original: no se reexportaron ni reimportaron. No se certifica equivalencia portable completa. El archivo se llama pueblo; el mapa es Puerto del Faro.'
    maps[4]['unitySourceAdjustment']=puerto_adjustment
    maps[4]['evidence']=[asset('05-pueblo/UnityAdjustedSource/adjustment-receipt.json','Puerto · recibo de copia ajustada','evidence')]
    history.append(asset('05-pueblo/HF_MAP_05_pueblo.blend','Puerto · fuente original anterior al bisel del segundo peldaño','history'))

# Final integration evidence is optional and must explicitly declare success.
# External source receipts are copied as immutable evidence only on final generation.
integration_receipts=[]
validated_receipts={}
receipt_rejections=[]
for kind,label,source in [
    ('catalog','Catálogo Unity',args.catalog_receipt or ROOT/'UnityPackage/catalog-receipt.json'),
    ('scene','Escenas Unity',args.scene_receipt or ROOT/'UnityPackage/scene-receipt.json'),
    ('loading','Carga de los cinco mapas',args.loading_receipt or ROOT/'UnityPackage/GameLoading/five-map-game-loading.txt')]:
    if not source.is_file():
        receipt_rejections.append(dict(kind=kind,reason='Receipt absent'))
        continue
    raw=source.read_bytes()
    try: data=validate_receipt(kind,raw)
    except (ValueError, UnicodeDecodeError) as error:
        receipt_rejections.append(dict(kind=kind,reason=str(error)))
        continue
    validated_receipts[kind]=data
    if source.resolve().is_relative_to(ROOT.resolve()):relative=source.resolve().relative_to(ROOT.resolve()).as_posix()
    else:
        folder=ROOT/'UnityEvidence';folder.mkdir(exist_ok=True)
        target=folder/(kind+'-'+hashlib.sha256(raw).hexdigest()[:12]+source.suffix.lower())
        if target.exists():assert target.read_bytes()==raw
        else:target.write_bytes(raw)
        relative=target.relative_to(ROOT).as_posix()
    item=asset(relative,receipt_label(kind,data),'successful_integration_receipt')
    integration_receipts.append(dict(kind=kind,validation='native_schema_valid',scope=data['scope'],sourcePath=str(source),asset=item))
unity_status=integration_status(validated_receipts)
for entry in maps:entry['unityStatus']=unity_status
manifest=dict(schemaVersion=1,generatedAtUtc=datetime.now(timezone.utc).isoformat(),root=str(ROOT),index='ENTREGA.html',
    scope='Entrega de arte Higgsfield; '+unity_status+'; sin equivalencia portable afirmada',
    integrationStatus=unity_status,receiptRejections=receipt_rejections,
    counts=dict(maps=5,recommendedBlendFiles=5,recommendedGlbFiles=5,recommendedCleanFbxFiles=5,artPreviews=10,unityCaptures=5,concepts=20),
    maps=maps,history=history,excludedDuplicates=[duplicate],evidence=[concept_manifest]+extras,
    integrationReceipts=integration_receipts,visualReview=dict(scope='Cinco capturas Unity revisadas; sin bloqueos P0/P1 identificados en esas imágenes.',
        remainingP2=['Yate: patrón radial marcado del agua.','Puerto: escala y lectura del fondo de acantilado.'],redesignRequested=False),
    fileRecords=list(records.values()),pathPolicy='All href/src values are relative to ENTREGA.html; move the complete folder to retain links.')

escape=html.escape
def link(item,text=None):
    return f'<a href="{escape(item["href"],quote=True)}">{escape(text or item["label"])}</a>'

def figure(item,title):
    return f'''<figure><a href="{item['href']}" aria-label="Abrir {escape(title,quote=True)}"><img src="{item['href']}" width="{item['width']}" height="{item['height']}" loading="lazy" decoding="async" alt="{escape(title,quote=True)}"></a><figcaption>{escape(item['label'])}</figcaption></figure>'''

sections=[]
for i,entry in enumerate(maps,1):
    downloads=''.join(f'<li>{link(item)}<small>{item["bytes"]/1048576:.1f} MB</small></li>' for item in entry['sources'])
    previews=''.join(figure(item,f'{entry["title"]}: {item["label"]}') for item in entry['previews'])
    unity_capture=figure(entry['unityCapture'],f'{entry["title"]}: {entry["unityCapture"]["label"]}')
    sketches=''.join(figure(item,f'{entry["title"]}: boceto {item["label"]}') for item in entry['concepts'])
    hashes=''.join(f'<tr><th scope="row">{escape(item["role"].upper())}</th><td><code>{item["sha256"]}</code><small>{escape(item["path"])}</small></td></tr>' for item in entry['sources'])
    evidence_links=''.join(f'<li>{link(item)}</li>' for item in entry.get('evidence',[]))
    sections.append(f'''<article id="{entry['slug']}" aria-labelledby="title-{entry['slug']}">
      <header class="map-heading"><span class="number">{i:02}</span><div><h2 id="title-{entry['slug']}">{escape(entry['title'])}</h2><p>{escape(entry['description'])}</p></div></header>
      <p class="state"><span>Higgsfield · Arte terminado</span><span>{escape(entry["unityStatus"])}</span></p>
      <div class="preview-grid">{previews}</div>
      <details class="unity-capture"><summary>Ver captura Unity revisada</summary>{unity_capture}<p class="muted">{escape(entry['unityCaptureScope'])}</p></details>
      <ul class="downloads" aria-label="Fuentes recomendadas de {escape(entry['title'],quote=True)}">{downloads}</ul>
      <p class="note{' attention' if entry['slug']=='casa' else ''}">{escape(entry['note'])}</p>
      {'<ul class="evidence">'+evidence_links+'</ul>' if evidence_links else ''}
      <details><summary>Ver los 4 bocetos aprobados</summary><p class="muted">Referencias conceptuales; las dimensiones y conexiones del modelo prevalecen.</p><div class="concept-grid">{sketches}</div></details>
      <details class="technical"><summary>Rutas y SHA256 de las fuentes</summary><table><caption>{escape(entry['title'])} · archivos recomendados</caption><tbody>{hashes}</tbody></table></details>
    </article>''')
nav=''.join(f'<a href="#{m["slug"]}">{escape(m["title"])}</a>' for m in maps)
history_links=''.join(f'<li>{link(item)}</li>' for item in history)
integration_links=''.join(f'<li>{link(item["asset"])}</li>' for item in integration_receipts)
integration_section=('<details><summary>Recibos de integración con éxito declarado</summary><ul>'+integration_links+'</ul><p>Cada recibo conserva su alcance. Las comprobaciones ausentes o pendientes no se presentan como aprobadas.</p></details>') if integration_receipts else ''
page=f'''<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="light"><title>Let me sleep · Entrega de mapas</title>
<style>
:root{{--paper:#f4f3ee;--ink:#202b29;--muted:#57635e;--line:#d9ded6;--green:#28574b}}
*{{box-sizing:border-box}}body{{margin:0;background:var(--paper);color:var(--ink);font:16px/1.6 system-ui,-apple-system,"Segoe UI",sans-serif}}
a{{color:var(--green);text-underline-offset:3px}}a:hover{{text-decoration-thickness:2px}}a:focus-visible,summary:focus-visible{{outline:3px solid #967b26;outline-offset:4px}}
.wrap{{width:min(1100px,calc(100% - 40px));margin:auto}}.masthead{{padding:48px 0 26px;border-bottom:1px solid var(--line)}}.eyebrow{{margin:0 0 12px;font-size:12px;letter-spacing:.14em;text-transform:uppercase;color:var(--muted)}}
h1{{font-size:clamp(30px,4.6vw,49px);line-height:1.15;letter-spacing:-.035em;margin:0 0 16px;font-weight:650}}.intro{{max-width:760px;margin:0;color:var(--muted)}}
nav{{display:flex;gap:8px 24px;flex-wrap:wrap;margin-top:24px;font-size:14px}}.totals{{display:flex;flex-wrap:wrap;gap:8px 28px;margin:22px 0 0;font-size:13px;color:var(--muted)}}
article{{padding:32px 0;border-bottom:1px solid var(--line);scroll-margin-top:20px}}.map-heading{{display:flex;gap:17px;align-items:baseline}}.number{{font-size:13px;color:var(--muted);font-variant-numeric:tabular-nums}}h2{{font-size:25px;line-height:1.25;margin:0;letter-spacing:-.02em}}.map-heading p{{margin:6px 0 0;color:var(--muted)}}
.state{{display:flex;flex-wrap:wrap;gap:8px 20px;font-size:12px;color:var(--muted);margin:14px 0}}.state span:first-child{{color:var(--green);font-weight:650}}
.preview-grid,.concept-grid{{display:grid;grid-template-columns:1fr 1fr;gap:16px}}figure{{margin:0;min-width:0}}figure a{{display:block;background:#e4e8e2;line-height:0}}img{{display:block;width:100%;height:auto;aspect-ratio:3/2;object-fit:contain}}figcaption{{font-size:12px;color:var(--muted);padding:6px 0}}
.unity-capture figure{{margin-top:12px;max-width:900px}}.unity-capture img{{aspect-ratio:8/5}}
.downloads{{list-style:none;display:flex;flex-wrap:wrap;gap:10px;margin:18px 0 0;padding:0}}.downloads li{{display:flex;align-items:center;gap:12px;border:1px solid var(--line);background:#fff;padding:9px 13px;font-size:14px}}small{{font-size:12px;color:var(--muted)}}
.note{{font-size:13px;max-width:950px;margin:16px 0;color:var(--muted)}}.attention{{padding:12px 15px;background:#efe7d6;border-left:3px solid #967b26;color:#554821}}
details{{margin-top:12px}}summary{{cursor:pointer;color:var(--green);font-size:14px;width:fit-content;padding:4px 0}}.muted{{color:var(--muted);font-size:13px}}.concept-grid{{margin-top:14px}}.concept-grid img{{aspect-ratio:16/9}}
table{{border-collapse:collapse;width:100%;margin:10px 0;background:#fff;font-size:12px}}caption{{text-align:left;color:var(--muted);padding:8px 0}}th,td{{padding:10px;border-top:1px solid var(--line);text-align:left;vertical-align:top}}th{{width:56px}}code{{overflow-wrap:anywhere;word-break:break-word;font-size:11px}}td small{{display:block;overflow-wrap:anywhere;margin-top:3px}}
footer{{padding:30px 0 44px;color:var(--muted);font-size:13px}}footer h2{{font-size:18px}}footer li{{margin:6px 0}}.footer-links{{display:flex;gap:20px;flex-wrap:wrap;margin:18px 0}}
@media(max-width:620px){{.wrap{{width:calc(100% - 28px)}}.masthead{{padding-top:30px}}.preview-grid,.concept-grid{{grid-template-columns:1fr}}.downloads{{display:grid;grid-template-columns:1fr}}.downloads li{{justify-content:space-between}}h2{{font-size:23px}}.number{{display:none}}}}
@media(prefers-reduced-motion:reduce){{*{{scroll-behavior:auto}}}}
</style></head><body><div class="wrap">
<header class="masthead"><p class="eyebrow">Let me sleep · Archivo de arte</p><h1>Cinco mapas para explorar</h1>
<p class="intro">Fuentes Higgsfield, vistas del arte terminado, cinco capturas Unity revisadas y los 20 bocetos aprobados. {escape(unity_status)}. Abrí una imagen para verla completa o elegí el formato del modelo.</p>
<p class="totals"><span>5 fuentes editables</span><span>10 vistas de arte</span><span>5 capturas Unity</span><span>20 bocetos</span></p><nav aria-label="Ir a un mapa">{nav}</nav></header>
<main>{''.join(sections)}</main>
<footer><h2>Sobre esta entrega</h2><p>Las vistas de Blender, los bocetos y las capturas Unity se identifican por separado; no certifican una partida completa ni rendimiento. La revisión de las cinco capturas no identificó bloqueos P0/P1. Quedan dos observaciones P2: el patrón radial del agua del Yate y la escala del fondo de acantilado de Puerto. Los enlaces principales señalan las fuentes recomendadas. Conservá la carpeta completa para mantener los enlaces locales.</p>
{integration_section}
<div class="footer-links"><a href="ENTREGA.manifest.json">Manifiesto completo · rutas y SHA256</a>{link(extras[0])}{link(extras[1])}</div>
<details><summary>Historial técnico · candidatos anteriores, no recomendados</summary><p>Estas referencias se conservan como evidencia. No reemplazan los archivos recomendados de cada mapa.</p><ul>{history_links}</ul></details>
<p>Inventario verificado: {escape(manifest['generatedAtUtc'])}. La copia duplicada 05-detail.png no se cuenta nuevamente entre los 20 bocetos.</p></footer>
</div></body></html>'''

MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
INDEX.write_text(page,encoding='utf-8')

class Links(HTMLParser):
    def __init__(self):super().__init__();self.links=[];self.ids=set();self.images=0
    def handle_starttag(self,tag,attrs):
        attrs=dict(attrs)
        if 'id' in attrs:self.ids.add(attrs['id'])
        for key in ('href','src'):
            if key in attrs:self.links.append(attrs[key])
        if tag=='img':assert attrs.get('alt');self.images+=1
parser=Links();parser.feed(page)
for value in parser.links:
    url=urlsplit(value)
    assert not url.scheme and not url.netloc, value
    if value.startswith('#'):assert url.fragment in parser.ids
    else:
        target=(ROOT/unquote(url.path)).resolve()
        assert target.is_relative_to(ROOT.resolve()) and target.is_file(), value
assert parser.images==35
assert len({item['sha256'] for m in maps for item in m['concepts']})==20
assert all(hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest()==item['sha256'] for item in records.values())
verification=dict(status='PASS_LOCAL_RELATIVE_LINKS_AND_FILE_HASHES',files=len(records),links=len(parser.links),imageElements=parser.images,
    counts=manifest['counts'],sourceFilesModified=False,externalRequests=False,
    htmlSha256=hashlib.sha256(INDEX.read_bytes()).hexdigest(),manifestSha256=hashlib.sha256(MANIFEST.read_bytes()).hexdigest())
(ROOT/'ENTREGA.verification.json').write_text(json.dumps(verification,indent=2)+'\n')
print(json.dumps(verification))
