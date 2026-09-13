"""Save an isolated Camp source after the native 3 mm contact repair is applied.

Run through the visible Blender MCP, with no concurrent generation/render.
Original files and live object references are restored; no export is claimed.
"""
import hashlib
import json
from pathlib import Path


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run(validation_path, application_path, output_directory):
    import bpy
    import sys

    validation = json.loads(Path(validation_path).read_text(encoding="utf-8-sig"))
    application = json.loads(Path(application_path).read_text(encoding="utf-8-sig"))
    assert validation["status"] == "PASS_SCOPED" and validation["cleanup"]
    assert not validation["errors"] and len(validation["cases"]) == 49
    assert not validation["pending"] and len(validation["passages"]) == 25
    assert all(case["status"] == "PASS" for case in validation["cases"])
    assert all(passage["status"] == "PASS_STATIC_CLEARANCE" for passage in validation["passages"])
    change = validation["campEdgeCandidate"]
    assert change["revision"] == "camp-path-north-edge-lower3mm-01"
    assert change["deltaY"] == -0.003 and change["materialReferencesPreserved"]
    assert application["status"] == "APPLIED_READBACK_PASS"
    assert application["mapId"] == validation["mapId"] == "hf-campamento-pinar-v2"
    assert application["oldContentHash"] == validation["contentHash"]
    assert application["newContentHash"] != application["oldContentHash"]
    assert application["edge"] == change
    assert application["prefabReadback"] and application["sceneReadback"]

    root = Path("N:/LetMeSleep/Artifacts/Higgsfield/Mapas")
    source = root / "03-campamento/HF_MAP_03_campamento.blend"
    assert sha(source) == "47b8e7d6e674244d9d46aaa32bf6f1e8fe0f71d99750612f84eaa44811a887df"
    out = Path(output_directory).resolve()
    assert out.parent == (root / "03-campamento").resolve() and not out.exists()
    assert bpy.context.mode == "OBJECT" and not bpy.app.is_job_running("RENDER")
    composer = sys.modules["bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface"]
    assert not composer._scene_conversation().busy()
    scene = bpy.data.scenes["HF_MAP_03_campamento"]
    path_object = scene.objects["CAMP_Terrain_Paths"]
    marker = scene.objects["Spawn_Human_05.002"]
    mesh = path_object.data
    assert not mesh.shape_keys and not path_object.modifiers
    assert tuple(path_object.users_scene) == (scene,)
    expected = {
        114: (-11.22790241241455, 17.44693374633789, .03999999910593033),
        116: (-9.602903366088867, 17.94693374633789, .03999999910593033),
    }
    for index, point in expected.items():
        assert max(abs(mesh.vertices[index].co[i] - point[i]) for i in range(3)) < 1e-7
    assert max(abs(marker.matrix_world.translation[i] - value) for i, value in enumerate(
        (29.799999237060547, 15.399999618530273, -.02499937079846859))) < 1e-7
    live_file, active_scene = bpy.data.filepath, bpy.context.scene
    preserved = {str(source): sha(source), live_file: sha(live_file)}
    original_marker = marker.matrix_world.copy()
    vertices_before = [tuple(vertex.co) for vertex in mesh.vertices]
    topology = [(tuple(p.vertices), p.material_index) for p in mesh.polygons]
    replacement = mesh.copy()
    out.mkdir()
    target = out / "HF_MAP_03_campamento_UNITY_ADJUSTED.blend"
    try:
        for index in expected:
            replacement.vertices[index].co.z -= .003
        replacement.update()
        assert [(tuple(p.vertices), p.material_index) for p in replacement.polygons] == topology
        changed = [i for i, vertex in enumerate(replacement.vertices)
                   if tuple(vertex.co) != vertices_before[i]]
        assert changed == [114, 116]
        path_object.data = replacement
        adjusted_marker = original_marker.copy()
        adjusted_marker.translation.z = .011650065
        marker.matrix_world = adjusted_marker
        bpy.data.libraries.write(str(target), {scene}, path_remap="RELATIVE_ALL", compress=False)
        recorded = {str(i): list(replacement.vertices[i].co) for i in changed}
    finally:
        marker.matrix_world = original_marker
        path_object.data = mesh
        bpy.data.meshes.remove(replacement)
    assert bpy.data.filepath == live_file and bpy.context.scene == active_scene
    assert [tuple(vertex.co) for vertex in mesh.vertices] == vertices_before
    assert all(sha(path) == digest for path, digest in preserved.items())
    with bpy.data.libraries.load(str(target), link=False) as (stored, unused):
        assert stored.scenes == [scene.name]
    receipt = {
        "status": "SOURCE_COPY_SAVED_LIVE_RESTORED",
        "source": str(target), "sha256": sha(target), "scene": scene.name,
        "nativeApplication": str(application_path), "nativeApplicationSha256": sha(application_path),
        "nativeContentHash": application["newContentHash"],
        "object": path_object.name, "changedVertices": recorded,
        "priorSpawn05CorrectionBlenderZ": .011650065,
        "preservedFiles": preserved,
        "scope": "Editable scene copy. Existing FBX/GLB exports are unchanged. Native closed collision mesh is authored in Unity.",
    }
    (out / "adjustment-receipt.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    return receipt
