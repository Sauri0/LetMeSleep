"""Two real, reversible proposals, built from the shared original character rig.

No production GLB or .blend is overwritten. Same palette, bone names, animation
inputs and cosmetic categories isolate differences in the actual silhouette.
"""
import argparse, importlib.util, json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('lms_character_source',ROOT/'art_source/export_presets/characters_pipeline.py')
pipeline=importlib.util.module_from_spec(spec);spec.loader.exec_module(pipeline)
ORIGINAL_FINISH=pipeline.finish
VARIANT='A'

def smooth(a,b,x):
    t=min(1,max(0,(x-a)/(b-a)))
    return t*t*(3-2*t)

def transform_point(q,name,species):
    q=Vector(q);a=VARIANT=='A'
    if species=='human':
        if name.startswith(('human_head','human_face','human_hair','human_accessory')):
            strength=smooth(1.36,1.48,q.y)
            q.x*=1+((1.08 if a else .90)-1)*strength
            q.y=1.55+(q.y-1.55)*(1+((.94 if a else 1.08)-1)*strength)
            if q.z<-.10:q.z=-.10+(q.z+.10)*(.96 if a else 1.17)
            if name=='human_accessory_3':
                above=max(0,q.y-1.735)
                q.y+=above*(-.25 if a else .30)
                if q.x<-.08:q.x=-.08+(q.x+.08)*(.82 if a else 1.24)
        elif name.startswith('human_outfit'):
            torso=smooth(.72,.91,q.y)*(1-smooth(.18,.29,abs(q.x)))
            q.x*=1+((1.13 if a else .89)-1)*torso
            q.z*=1+((1.10 if a else .94)-1)*torso
            # A soft fabric line below the waist: same leg pivots, no rigid V.
            if q.y<.74:
                center=(-.135 if q.x<0 else .135)
                q.x=center+(q.x-center)*(1.02 if a else 1.09)
        elif name.startswith('human_footwear'):
            q.z=-.09+(q.z+.09)*(1.02 if a else 1.10)
    else:
        if name.startswith(('mosquito_face','mosquito_hair','mosquito_accessory')) or (name=='mosquito_core' and q.z<-.035):
            q.x*=1.06 if a else .91
            q.y=.018+(q.y-.018)*(.96 if a else 1.08)
        if name.startswith('mosquito_outfit'):
            q.x*=1.15 if a else .80
            q.y=-.008+(q.y+.008)*(1.08 if a else .87)
            q.z=.08+(q.z-.08)*(.84 if a else 1.28)
        if name.startswith('mosquito_wing'):
            sign=-1 if q.x<0 else 1
            q.x=sign*.033+(q.x-sign*.033)*(.87 if a else 1.13)
            q.z=.045+(q.z-.045)*(1.20 if a else .75)
    return q

def proposal_finish(species):
    source=ROOT/'art_source/samples07/characters'/VARIANT/species
    output=ROOT/'game/assets/art/samples07/characters'/VARIANT/species
    pipeline.OUTPUT_PATHS[species]=(output,source)
    pipeline.SAMPLE_QUALITY=True
    for obj in pipeline.OBJECTS:
        original=[v.co.copy() for v in obj.data.vertices]
        key_original={k.name:[v.co.copy() for v in k.data] for k in obj.data.shape_keys.key_blocks} if obj.data.shape_keys else {}
        def converted(v):
            return pipeline.g(transform_point((v.x,v.z,-v.y),obj.name,species))
        for vertex,value in zip(obj.data.vertices,original):vertex.co=converted(value)
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                for vertex,value in zip(key.data,key_original[key.name]):vertex.co=converted(value)
        obj.data.update()
    ORIGINAL_FINISH(species)
    manifest=json.loads((source/'manifest.json').read_text())
    default=manifest['default'];parts=[];positions=[]
    for obj in pipeline.OBJECTS:
        pieces=obj.name.split('_');category=pieces[1] if len(pieces)>1 else 'core'
        selected=True
        if len(pieces)>2 and category in default:selected=int(pieces[2])==default[category]
        if species=='human' and category=='hair':selected=selected and obj.name.endswith('_capped')
        points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in obj.data.vertices]
        low=[min(v[i] for v in points) for i in range(3)];high=[max(v[i] for v in points) for i in range(3)]
        if selected:positions.extend(points)
        parts.append({'name':obj.name,'category':category,'selected_default':selected,'vertices':len(points),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'materials':[m.name for m in obj.data.materials],'bounds_min_m':low,'bounds_max_m':high,'morphs':[k.name for k in obj.data.shape_keys.key_blocks if k.name!='Basis'] if obj.data.shape_keys else []})
    low=[min(v[i] for v in positions) for i in range(3)];high=[max(v[i] for v in positions) for i in range(3)]
    scale=.35 if species=='mosquito' else 1.0
    record={'proposal':VARIANT,'label':'Curva compacta' if VARIANT=='A' else 'Alargada desgarbada','species':species,'status':'Real staged geometry; not selected for production','original':True,'runtime_scale':scale,'bounds_min_m':[v*scale for v in low],'bounds_max_m':[v*scale for v in high],'size_m':[(high[i]-low[i])*scale for i in range(3)],'triangles_default':sum(p['triangles'] for p in parts if p['selected_default']),'surfaces_default':sum(len(p['materials']) for p in parts if p['selected_default']),'textures':['Original procedural cloth at runtime; no external image textures'],'bones':manifest['bones'],'parts':parts,'implemented':['Six selectable facial styles across both species','Ten named facial morphs per style','Shared skeleton names and cosmetic categories','Editable Blender sources and explicit GLB'],'pending':['Direction selection','Final gameplay retarget and eight-zone collision validation for changed silhouette','Performance verification after selection']}
    (source/'model.json').write_text(json.dumps(record,indent=2),encoding='utf8')
    (output/'model.json').write_text(json.dumps(record,indent=2),encoding='utf8')
    print('STAGED_CHARACTER',VARIANT,species,record['size_m'],record['triangles_default'],record['surfaces_default'])

pipeline.finish=proposal_finish
if __name__=='__main__':
    bpy.context.preferences.filepaths.save_version=0
    parser=argparse.ArgumentParser();parser.add_argument('--variant',choices=['A','B','both'],default='both');parser.add_argument('--species',choices=['human','mosquito','both'],default='both')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for VARIANT in (['A','B'] if args.variant=='both' else [args.variant]):
        pipeline.SAMPLE_QUALITY=True
        if args.species in ['human','both']:pipeline.human()
        if args.species in ['mosquito','both']:pipeline.mosquito()
