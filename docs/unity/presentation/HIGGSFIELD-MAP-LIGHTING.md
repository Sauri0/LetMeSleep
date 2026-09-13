# Higgsfield map lighting integration

New five-map ticket only. The previous alfa remains frozen. No scene, prefab, geometry, material, old preset or URP asset was edited; no Unity/Blender process was launched.

## Exact Bootstrap contract

Keep a serialized entry `mapPrefab + HiggsfieldMapLighting.Configuration` in the Bootstrap-owned registry. Do not add a Presentation dependency to Content. At runtime, resolve the registry's local light anchors to the instantiated map's transforms (not prefab asset transforms), then call:

```csharp
alfaRig.BindHiggsfield(mapInstance.transform, resolvedConfiguration);
// Before returning to existing lighting:
alfaRig.UnbindHiggsfield();
// Existing BindMap(alphaOrLobbyAnchors, house) also exits Higgsfield first.
```

HiggsfieldMapLighting is lazily added to the lighting rig, not the environmental prefab. It has no serialized Settings field: Configuration is the explicit Bind argument. `IsHiggsfieldBound`, component `MapRoot`, `Period` and `LocalLightCount` are inspectable. Existing AlfaLightingRig BoundAnchors/MapLightCount keep describing the retained legacy lights.

Map/period mapping is fixed metadata: Island=Day, House/Camp=Night, Yacht=Sunset, Port=Twilight. Actual skybox, ambient colors/intensity, reflection intensity, sun world rotation/color/Unity intensity, fog and local light values are supplied by Bootstrap. Period does not manufacture or convert intensities. Intensity/range fields start unset where appropriate and invalid/nonfinite values are rejected before changing the current binding.

Each LocalSource accepts one distinct descendant anchor, Point/Spot type, color, UnityIntensity, Range, spot angles and shadow mode. Lights inherit the anchor's position/rotation; spots emit along Unity +Z. Configuration is limited to64 local sources; this is an input guard, not a performance or shadow budget endorsement. No arbitrary source-name inference or Blender watt conversion occurs. Root must verify coordinate/color/energy interpretation and provide calibrated Unity values from the future scene-audit.json integration.

## Ownership and restoration

The existing moon light is reused as the sole owned directional source. The rig's legacy local lights and lobby fill, previous RenderSettings.sun, any directional lights within the new map, and explicit Configuration.SuppressLights are temporarily disabled with their enabled states preserved. Other scene directionals must be listed explicitly by Bootstrap; this component does not scan unrelated scenes. Imported Point/Spot lights that the explicit list replaces must likewise be disabled by the importer or listed in SuppressLights to avoid duplicate fixtures.

Bind retains legacy lights for restoration rather than rebuilding them. Unbind restores every modified primary-light field, its rotation, global sun/skybox, ambient mode/colors/intensity/probe, reflection intensity, fog settings, and the existing global Volume sharedProfile/weight/enabled state. With no new VolumeProfile, the old volume is temporarily disabled so legacy night grading does not affect Island Day. Profile assets are never mutated.

Repeated Bind first releases previous created lights; they are immediately disabled/inactivated before deferred Destroy. Invalid replacement configurations leave the current binding intact. Exceptions while installing a validated configuration invoke restoration. Explicit Unbind, disabling/destroying the component or rig, and destruction of the bound map root clean up. Map deactivation alone is not interpreted as a scene exit: Bootstrap must call Unbind and deactivate/unload the old map when switching maps. Do not keep two environmental roots with their own active lights when transitioning. Global RenderSettings ownership must remain exclusive to this rig while bound.

Returning via BindMap restores the saved state before the existing alfa/lobby logic runs. Rebinding Higgsfield after leaving recaptures the current state; no old preset asset is modified. ApplyPreset is ignored only while Higgsfield owns lighting, preventing accidental legacy overwrites.

## URP shadow budget and native checks

The integration leaves PC_RPAsset.shadowDistance=50 m unchanged. This is the pipeline's shadow distance, not QualitySettings.shadowDistance. A camera overview at150 m cannot establish that daytime sun shadows work near gameplay actors. Root owns a separate, explicitly documented overview capture profile/temporary pipeline configuration if farther shadows are needed; do not raise the gameplay budget or alter a shared URP asset through this component. At near gameplay distance, validate the existing50 m budget, local shadow count/resolution and actual camera-relative shadow coverage. No automatic selection of shadow-casting locals is made: Root must cap them in the explicit config.

Offline validation: the actual new component, modified AlfaLightingRig and unchanged AlfaPresentationPreset compile against Unity6000.3.24f1/URP runtime assemblies with zero errors/warnings. This is not Unity lifecycle, lighting quality or performance approval.

Director native acceptance: bind each of the five map configs; verify one active intended directional, calibrated sky/ambient/local positions, no duplicate imported fixtures; bind the same map twice and switch maps with old root deactivated; confirm enabled local count does not grow. Try an invalid/foreign anchor and verify the current state survives. Unbind and compare the original sun, skybox, fog, ambient, Volume and legacy enabled states; test map destruction and component disable. Capture both gameplay-near and overview views with their shadow-distance settings recorded. Environment/reflection updates are asynchronous engine work and need native confirmation after settling.
