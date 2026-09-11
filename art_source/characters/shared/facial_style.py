"""Rest silhouettes for the existing cosmetic IDs, before surface attachment.

The selected A/B anatomy, vertex counts, weights and public morph names stay
intact. Applying the same rest offset to every key preserves each authored
expression delta; facial_parts subsequently fits every endpoint to its support.
Coordinates are Blender metres: X across the face, Z up, +Y forward.
"""
import json
import math


STYLE_VERSION = "LMS091.rest1"


def shape_rest_feature(obj, species, category, variant):
    if category not in {"brows", "mouth"}:
        return
    mesh = obj.data
    keys = mesh.shape_keys.key_blocks
    basis = [point.co.copy() for point in keys["Basis"].data]
    human = species == "human"
    width = max(abs(point.x) for point in basis)
    offsets = []
    if category == "mouth":
        # Reposo is a compact horizontal mouth. Sonrisa and Seria have actual
        # opposing curves in the dark opening as well as in the lip contour.
        # Previously all three openings were the very same shallow ellipsoid.
        scale_x = ([1.04, .78, .95] if human else [.98, .78, .95])[variant]
        curl = ([.007, 0.0, -.006] if human else [.005, 0.0, -.004])[variant]
        opening = {i for polygon in mesh.polygons
                   if mesh.materials[polygon.material_index].name == "ink"
                   for i in polygon.vertices}
        for index, point in enumerate(basis):
            offset = point.copy() * 0.0
            offset.x = point.x * (scale_x - 1.0)
            # The lip already carries a shaped corner. Keep its extra lift
            # small so the full smile retains room below optional mustaches.
            relief = 1.0 if index in opening else .25
            offset.z = curl * relief * min(1.0, abs(point.x) / width) ** 2
            if not human and variant == 0:
                offset.z -= .0018
            offsets.append(offset)
    else:
        # Keep the soft brow, give Arqueadas/Altas a narrower raised crown,
        # and make Firmes/Bajas wider. Thin surface-conformed relief is still
        # supplied by facial_parts; these are not added floating decorations.
        scale_x = ([1.0, .92, 1.0] if human else [1.0, .80, 1.08])[variant]
        inner = min(abs(point.x) for point in basis)
        for point in basis:
            t = max(0.0, min(1.0, (abs(point.x) - inner) / (width - inner)))
            offset = point.copy() * 0.0
            offset.x = point.x * (scale_x - 1.0)
            if variant == 1:
                offset.z = (.003 if human else .009) * math.sin(math.pi * t)
            elif variant == 2 and not human:
                offset.z = -.002 * (1.0 - t)
            offsets.append(offset)
    for key in keys:
        for point, offset in zip(key.data, offsets):
            point.co += offset
    for vertex, point in zip(mesh.vertices, keys["Basis"].data):
        vertex.co = point.co
    mesh.update()
    obj["rest_style091"] = json.dumps({
        "version": STYLE_VERSION,
        "category": category,
        "variant": variant,
        "maximum_rest_offset_m": max(offset.length for offset in offsets),
        "width_scale": scale_x,
        "topology_and_weights_unchanged": True,
        "attachment": "Applied before the existing per-endpoint support projection",
    })


def fit_mosquito_goggle_bridge(obj):
    """Relieve only the inner bridge where the old frame crossed both globes."""
    changed = 0
    for vertex in obj.data.vertices:
        t = max(0.0, min(1.0, (.024 - abs(vertex.co.x)) / .012))
        # The frame's bridge is above the proboscis. Leave lower support and
        # both outer rings unchanged; four source millimetres are 1.4mm in play.
        if vertex.co.z > .024 and t > 0.0:
            vertex.co.y += .004 * t * t * (3.0 - 2.0 * t)
            changed += 1
    obj.data.update()
    obj["goggle_bridge091"] = json.dumps({"moved_vertices": changed, "maximum_forward_relief_m": .004})
