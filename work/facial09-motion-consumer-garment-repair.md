# Garment motion repair — current fit4

This is a local garment repair, not a redesign of the selected compact human A.
All six outfit/trim meshes remain. The 27 non-garment meshes, 36-bone rig,
HumanPose, camera, collision authority and facial controls are unchanged.

## Measured problem and correction

The former shirt was a closed dome reaching 1.425 m in bind pose. Its central
cap collided with the jaw when the head descended farther than the torso in
crouch. The sleeve-cap sphere extended 79 mm above the shoulder origin; bone
stretch made a pronounced peak. The old nearest-sample shoulder binding also
contained hard spatial transitions.

The source helper now lowers the central neckline with a continuous radial
field, tailors the upper sleeve cap, smooths the upper surface with a 20 mm
displacement bound, and blends torso/upperarm weights continuously. It preserves
topology, cuffs and lower clothing outside the local transition.

Fit1 cleared the original contacts in human-57,119,102 but still left 492
unexpected triangle pairs in human-90 when all compatible beard/head variants
were selected. Fit2 clears all 192 pair evaluations across those four cases.
Expected head/neck garment attachment is still classified by the original
strict region; no exception was enlarged and no alternative was skipped.

Fit2's surface relaxation left the previous UV coordinates in place, causing
stripe isolines to zigzag. Fit3 applies the cylindrical displacement delta to
the existing UV map. It retains the original pattern, palette and shader.
The read-only UV audit proves that fit3 has identical GLB positions, indices,
normals, weights, morphs and materials to fit2: 1,782 checks, zero failures.
Only 6,489 cloth UV entries changed.

The independent normal audit found no frozen normals or hard splits. A native
full-index garment comparison did not remove the fit1 shape problem. Neither
finding justified changing runtime LOD or lighting.

The full fit3 consumer then measured 1,710 residual triangle-pair contacts in
nine crouch/high-breath/look-down poses (54–56,63–65,72–74). They involved only
the central garment cap versus head and beard1, with all three outfits kept.
Exact current witnesses human-63 and human-72 were rendered before the next
edit (45/45 checks, eight views), not substituted by the earlier four poses.
The maximum local triangle-plane extent used for ranking was 2.315 mm; that
quantity is not claimed as a signed closed-mesh penetration depth.

Fit4 adds only 8 mm of source clearance through the same smooth central
neckline field. The shoulder shaping and UV correction remain. The 27-case
block54–80 passes 1,296 pair evaluations with zero unexpected contacts.
The matching eight after views pass 39/39 capture/invariance checks; the
intentional garment change is measured while head/beard geometry stays exact.

## Reproduction and evidence

- Editable helper: art_source/characters/shared/garment_fit.py.
- Repair exporter: art_source/export_presets/characters_garment_repair.py.
- Selected-source pipeline also calls the same helper.
- Current editable: art_source/characters/human/human_lms06.blend.
- Current production: game/assets/art/characters/human/human_lms06.glb.
- Exact original and intermediate models: outputs/0.9-facial-motion-witnesses/source-before, source-fit1 and source-fit2, with SHA manifests.
- Exact before views: outputs/0.9-facial-motion-witnesses/before-exact.
- Fit3 views: outputs/0.9-facial-motion-witnesses/after-fit3.
- Fit4 residual before/after: outputs/0.9-facial-motion-witnesses/fit3-residual-before
  and fit4-residual-after; exact fit3 source is preserved in source-fit3.
- Same actor inputs, camera, studio Compatibility light and expression controls
  are used by game/tests/facial09_motion_witness_views.gd.
- Before capture/invariance: 113/113; after: 101/101 (changed garment positions
  are reported, while the selected unchanged head/face remains compared).
- Native contact inputs: work/facial09-motion-consumer-fit2-{57,119,102,90}.json.
- Pair results: same prefixes ending -contacts.json, 48 pairs per case,
  zero unexpected contacts.
- Geometry equality and exact before/after SHA:
  work/facial09-motion-consumer-uv-audit.json.
- Normal diagnosis and exact inspected SHA: work/garment09-normal-audit.md.

The source-fit2 archive includes a separately named next-recipe-garment-fit3.py;
it is preparation for the subsequent UV change, not claimed as the fit2 recipe.
The fit2 editable blend itself is exact.

## Final measured closure and limits

Fit4 on source version0.9.0 has a fresh complete 243-human/42-mosquito native
export: work/facial09-motion-export-fit4/index.json. Its 11 verified processes
all exited0 with empty stderr and uniform source hashes. The unchanged
consumer completed all285 cases and13,176 compatible-pair evaluations with
zero unexpected contacts: work/facial09-motion-consumer-fit4/consumer-index.json.
The final manifest work/facial09-motion-consumer-repair-final.json ties those
indices to the current editable blend, GLB, recipe and runtime SHA values.

The production surface gate passes972/972 checks across540 measurements with
all tools and three clothing variants; log
work/facial09-motion-consumer-selected-fit4.log. It checks the actual deformed
mesh against existing contact zones without changing their radii or pose.
All earlier full reports, targeted subsets, before captures and model versions
remain historical evidence and are not relabelled with the final model hash.

No global visual approval, all-combination clearance, or performance claim is
inferred from this finite pose/pair coverage. Continuous motion, complete
containment, floating, self-intersection and the exhaustive gallery are outside
the consumer's declared boundary-contact scope.
