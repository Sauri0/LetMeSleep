# Opt-in camera distance and GPU water fog proposal

Implementation is in the presentation worktree only. Root owns central integration. Copies of Bootstrap are review inputs, never files to overwrite blindly; apply their adjacent diffs after checking SOURCE-HASHES.txt against the central source.

- CameraFarPlane = 0 preserves existing serialized catalogs. Nonzero must be finite 10..1000 metres.
- AlfaApplication reuses one component on its persistent object. Bind follows presentation creation; StopGame and LoadMap unbind before destroying cameras/maps.
- Component snapshots far, rejects competing ownership, restores on unbind/disable/destruction/map deactivation. LateUpdate order1500 reapplies after role camera orders1250/1300. Near/FOV and preset assets are untouched.
- GPUWater.Parameters.UseFog defaults false. It affects the private material color pass only; shared vertex deformation/depth and mesh buffers retain their behavior. Forward fog variants use URP ComputeFogFactor/MixFog.
- Root must propagate CameraFarPlane through its catalog source builder and UseFog through its GPU water wrapper; runtime classes alone do not activate candidates. Preserve Casa/Camp night configuration.

Candidate settings remain the A/B values in COMPLETE-SCOPE-PROPOSAL.md. No catalog values or shader assets were imported centrally by this worker. Shader variant compilation and actual role-switch/map-switch restoration still require the leased Unity run. Far1000 is a diagnostic control only.

Offline validation: CameraWater.csproj, Camera.csproj, BootstrapProposal.csproj, Sweep.csproj all compile against local Unity6000.3.24f1 references with zero errors and zero warnings. This checks C# signatures, not native behavior, shader compilation or visual quality.

External fixture at N:/LetMeSleep/Validation/Higgsfield/CompleteScope/Presentation now accepts puertoOceanExtentCandidate=false. True applies Root helper to a Puerto clone only, records bounds before/after and changed/preserved vertices, disposes/restores before scene cleanup. Reports include the executing fixture DLL SHA256. Existing pilot evidence remains untouched. The corrected physics diagnostic still cannot certify concave collider containment or actual gameplay traversal.

Native acceptance still needed: preset reset followed by normal frame late update; shared Human/Mosquito camera; repeated map changes without extra components; stop/disable/unload restores original far; zero catalog opt-out; GPU fog off/on with same time, geometry, camera; restored materials/bounds; no shader errors; five map viewpoints including supported ground samples. Classify outside-limit cameras explicitly as diagnostics.
