import bpy
import math
import json
from pathlib import Path
from mathutils import Quaternion

root = Path('N:/LetMeSleep/Worktrees/characters/Higgsfield/HUM-NOCHE-001')
target = root / 'source/HUM-NOCHE-001_workspace.blend'
status = Path('N:/LetMeSleep/Repository/Higgsfield/BLENDER-SESION.json')

def setup():
    if bpy.data.filepath:
        raise RuntimeError('Unexpected existing file; refusing to replace an open project')
    if target.exists():
        raise RuntimeError('New workspace already exists; refusing to overwrite')
    target.parent.mkdir(parents=True, exist_ok=True)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    scene.name = 'HUM-NOCHE-001_NewFromSketches'
    refs = bpy.data.collections.new('00_REFERENCIAS_BOCETOS')
    scene.collection.children.link(refs)
    new = bpy.data.collections.new('01_HUMANO_NUEVO')
    scene.collection.children.link(new)
    sources = [
        ('UI-06_LetMeSleep', 'C:/Users/brank/Desktop/bocetos/ui/ChatGPT Image 12 sept 2026, 06_01_41 p.m..png', -3.1),
        ('PER-06_ConstruccionHumana', 'C:/Users/brank/Desktop/bocetos/personajes/ChatGPT Image 12 sept 2026, 05_21_31 a.m. (1).png', 3.1),
    ]
    for label, path, x in sources:
        image = bpy.data.images.load(path, check_existing=True)
        image.pack()
        obj = bpy.data.objects.new(label, None)
        obj.empty_display_type = 'IMAGE'
        obj.data = image
        obj.empty_display_size = 5.8
        obj.location = (x, 0, 2.5)
        obj.rotation_euler = (math.pi / 2, 0, 0)
        refs.objects.link(obj)
    scene['workflow'] = 'New human directly from original sketches. No alpha meshes.'
    scene['native_slot_owner'] = 'Encargado de Higgfield'
    scene.unit_settings.system = 'METRIC'
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_perspective = 'ORTHO'
                space.region_3d.view_rotation = Quaternion((math.sqrt(.5), math.sqrt(.5), 0, 0))
                space.region_3d.view_location = (0, 0, 2.5)
                space.region_3d.view_distance = 13
                space.overlay.show_floor = False
                space.overlay.show_axis_x = False
                space.overlay.show_axis_y = False
                space.show_region_ui = True
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    status.write_text(json.dumps({'state':'visible_workspace_ready','file':str(target),
        'owner':'Encargado de Higgfield','old_assets_loaded':False,
        'reference_objects':[s[0] for s in sources],'generation_jobs_in_this_script':0},indent=2),encoding='utf-8')
    return None

bpy.app.timers.register(setup, first_interval=2)
