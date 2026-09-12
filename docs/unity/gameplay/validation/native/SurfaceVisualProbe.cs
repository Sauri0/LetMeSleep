using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LetMeSleep.Bootstrap;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Presentation.Gameplay;
using LetMeSleep.UI;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// External diagnostic assembly, loaded by eval_file. Never add to Assets.
public static class SurfaceVisualProbe
{
    private const string Root = "N:/LetMeSleep/Validation/SurfaceVisual-20260912";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Feet = { "Leg103.L", "Leg203.L", "Leg303.L", "Leg103.R", "Leg203.R", "Leg303.R" };
    private static AlfaApplication app;
    private static GameplayRuntime runtime;
    private static ActorVisualBinding binding;
    private static Animator animator;
    private static bool active, holding, pending, frozenBinding, originalBindingEnabled;
    private static float animatorSpeed;
    private static bool manualCheckpoints, epochEstablished, trainingRequested;
    private static Camera gameCamera;
    private static StreamWriter frames;
    private static int frameSamples, lastRenderedFrame;
    private static double lastRender;
    private static readonly Dictionary<string, Vector3> previousFeet = new Dictionary<string, Vector3>();
    private static readonly Dictionary<string, float> previousClearances = new Dictionary<string, float>();
    private static uint previousSupport;
    private static CursorLockMode oldCursorLock;
    private static bool oldCursorVisible;
    private static ulong epoch;
    private static uint sequence, actionSequence, lastTick, phaseTick, progressTick;
    private static int phase, nextPhase, captureNumber;
    private static float closest;
    private static double began, holdBegan, heldSeconds;
    private static string folder, surface, pendingLabel, status;
    private static Vector3 target, normal, tangent, crawlStart, detachStart;
    private static Float3 aim;
    private static SurfaceContact support;
    private static bool hasSupport;
    private static readonly List<object> trace = new List<object>();

    public static string Start(string requestedSurface, bool useManualCheckpoints = true)
    {
        if (active) throw new InvalidOperationException("Stop the current probe first.");
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == typeof(SurfaceVisualProbe).Assembly || !assembly.GetName().Name.StartsWith("SurfaceVisualProbe_")) continue;
            var other = assembly.GetType("SurfaceVisualProbe")?.GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
            if (other != null && (bool)other.GetValue(null)) throw new InvalidOperationException("Another loaded probe is active. Stop its package first.");
        }
        if (!Application.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Use unpaused Play Mode from the main menu.");
        if (requestedSurface != "floor" && requestedSurface != "wall" && requestedSurface != "ceiling")
            throw new ArgumentException("Expected floor, wall or ceiling.");
        app = Object.FindFirstObjectByType<AlfaApplication>();
        if (!app) throw new InvalidOperationException("AlfaApplication is not loaded.");
        var ui = (AlfaUiController)Read(app, "ui");
        if (ui == null || ui.CurrentScreen != AlfaUiScreen.MainMenu || (bool)Read(app, "pendingOnline") ||
            Read(app, "room") != null || Read(app, "game") != null ||
            Object.FindObjectsByType<GameplayRuntime>(FindObjectsSortMode.None).Length != 0)
            throw new InvalidOperationException("Refusing to replace an existing runtime/room. Return to the main menu first.");
        surface = requestedSurface;
        folder = Path.Combine(Root, "native-" + surface + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        if (!Environment.GetCommandLineArgs().Any(a => string.Equals(a, "-noaudio", StringComparison.OrdinalIgnoreCase)) && AudioListener.volume != 0)
            throw new InvalidOperationException("Director must launch with -noaudio or mute AudioListener before this probe. Probe does not change global volume.");
        oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
        trace.Clear(); sequence = actionSequence = 0; lastTick = uint.MaxValue;
        phase = nextPhase = captureNumber = 0; phaseTick = progressTick = 0;
        closest = float.MaxValue; heldSeconds = 0; hasSupport = holding = pending = frozenBinding = false;
        runtime = null; binding = null; animator = null; gameCamera = null; epochEstablished = trainingRequested = false;
        manualCheckpoints = useManualCheckpoints; frameSamples = 0; lastRenderedFrame = -1;
        previousFeet.Clear(); previousClearances.Clear(); previousSupport = 0;
        began = EditorApplication.timeSinceStartup; active = true; status = "RUNNING";
        lastRender = began;
        target = surface == "ceiling" ? new Vector3(3.6f, 2.57f, 1.3f) :
            surface == "wall" ? new Vector3(4.75f, 1.9f, 2f) : new Vector3(3.6f, .23f, 1.3f);
        normal = surface == "ceiling" ? Vector3.down : surface == "wall" ? Vector3.left : Vector3.up;
        tangent = surface == "wall" ? Vector3.back : Vector3.left;
        try
        {
            frames = new StreamWriter(Path.Combine(folder, "frames.jsonl"), false) { AutoFlush = true };
            trainingRequested = true;
            app.StartTraining(AlfaRole.Mosquito, AlfaUiController.BloodModeId, RoomRules.AlfaMap);
            runtime = (GameplayRuntime)Read(app, "game");
            if (!runtime || runtime.LocalPrincipal != "practice" || !runtime.IsHost || runtime.LatestSnapshot == null)
                throw new InvalidOperationException("Training did not create the expected local host runtime.");
            epoch = runtime.LatestSnapshot.SessionEpoch;
            epochEstablished = true;
            runtime.CaptureLocalInput = false;
            // Sequence counters belong to this newly created runtime. Keep them in sync for cleanup.
            sequence = (uint)Read(runtime, "inputSequence"); actionSequence = (uint)Read(runtime, "actionSequence");
            binding = runtime.World.Actors[runtime.LocalActorId].GetComponentInChildren<ActorVisualBinding>();
            if (!binding || binding.View == null) throw new InvalidOperationException("Integrated ActorVisualBinding missing.");
            animator = binding.View.Animator;
            if (!animator) throw new InvalidOperationException("Character animator missing.");
            var presentation = (GameObject)Read(app, "presentation");
            gameCamera = presentation.GetComponentsInChildren<Camera>().FirstOrDefault(c => c.enabled && c.cameraType == CameraType.Game);
            if (!gameCamera) throw new InvalidOperationException("Owned presentation game camera missing.");
            trace.Add(new { kind = "runtime_identity", runtime.LatestSnapshot.MapId, runtime.LatestSnapshot.ContentHash,
                runtime.LatestSnapshot.BalanceHash, sessionEpoch = epoch, camera = gameCamera.name,
                controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                animatorCulling = animator.cullingMode.ToString(), animatorUpdateMode = animator.updateMode.ToString(),
                meshes = binding.View.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => new {
                    r.name, source = AssetDatabase.GetAssetPath(r.sharedMesh), readable = r.sharedMesh && r.sharedMesh.isReadable,
                    r.updateWhenOffscreen, r.enabled, worldScale = V(r.transform.lossyScale),
                    bones = r.bones.Select(b => b ? b.name : null).ToArray() }).ToArray() });
            originalBindingEnabled = binding.enabled; animatorSpeed = animator.speed;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            EditorApplication.update += Observe;
            RenderPipelineManager.endContextRendering += AfterRender;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            Save(null);
            return Status();
        }
        catch (Exception e) { End("ERROR", e.ToString()); throw; }
    }

    private static object Read(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Write(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static bool OwnsRuntime() => app && runtime && ReferenceEquals(Read(app, "game"), runtime) &&
        (bool)Read(app, "training") && runtime.LocalPrincipal == "practice" && runtime.LatestSnapshot != null &&
        epochEstablished && runtime.LatestSnapshot.SessionEpoch == epoch;
    private static ActorSnapshot Self() => runtime.LatestSnapshot.Actors.First(a => a.ActorId == runtime.LocalActorId);
    private static float[] V(Vector3 p) => new[] { p.x, p.y, p.z };
    private static object State()
    {
        var a = Self();
        return new { tick = runtime.LatestSnapshot.HostTick, position = V(a.Position.ToUnity()),
            velocity = V(a.Velocity.ToUnity()), speedMetersPerSecond = a.Velocity.Length, a.MotionPhase,
            bodyForward = V(a.BodyRotation.ToUnity() * Vector3.forward), life = a.LifeState.ToString(), a.ViewRevision,
            surfaceId = a.SurfaceAttachment.HasValue ? a.SurfaceAttachment.Value.SurfaceId : 0 };
    }

    private static void Input(float amount, Vector3 direction)
    {
        if (!OwnsRuntime()) throw new InvalidOperationException("Probe no longer owns its training runtime.");
        direction.Normalize();
        float yaw = Mathf.Atan2(direction.x, direction.z), pitch = Mathf.Clamp(Mathf.Asin(direction.y), -110 * Mathf.Deg2Rad, 89 * Mathf.Deg2Rad);
        aim = MathEx.Aim(yaw, pitch);
        // Same local view fields as mouse input, exclusively on the runtime this helper created.
        Write(runtime, "yaw", yaw); Write(runtime, "pitch", pitch);
        var s = runtime.LatestSnapshot; var a = Self();
        var reject = runtime.Authority.SubmitInput(runtime.LocalPrincipal, new PlayerInputCommand(
            new CommandHeader(s.SessionEpoch, s.RoundId, a.ActorId, ++sequence, s.HostTick, a.ViewRevision),
            new Float2(0, amount), 0, yaw, pitch, aim));
        Write(runtime, "inputSequence", sequence);
        if (reject != CommandReject.None) throw new InvalidOperationException("Normal input rejected: " + reject);
    }

    private static void PerchToggle()
    {
        var s = runtime.LatestSnapshot; var a = Self();
        var reject = runtime.Authority.SubmitAction(runtime.LocalPrincipal, new PlayerActionCommand(
            new CommandHeader(s.SessionEpoch, s.RoundId, a.ActorId, ++actionSequence, s.HostTick, a.ViewRevision), ActionKind.PerchToggle, aim));
        Write(runtime, "actionSequence", actionSequence);
        trace.Add(new { kind = "F", rejection = reject.ToString(), state = State() });
        if (reject != CommandReject.None) throw new InvalidOperationException("F rejected: " + reject);
    }

    private static void Observe()
    {
        if (!active) return;
        try
        {
            if (!OwnsRuntime()) { End("ABORTED", "Runtime replaced or round ended; no writes to the replacement."); return; }
            double now = EditorApplication.timeSinceStartup;
            if (holding) { if (now - holdBegan > 600) End("CANCELLED", "Ten-minute checkpoint lease expired."); return; }
            if (now - lastRender > 10) { End("INCONCLUSIVE", "No owned game-camera frame for ten seconds."); return; }
            if (pending) { if (now - holdBegan > 10) End("INCONCLUSIVE", "Checkpoint render timeout."); return; }
            if (now - began - heldSeconds > 60) { End("INCONCLUSIVE", "60-second active-time bound reached."); return; }
            var s = runtime.LatestSnapshot;
            if (s.SimulationPhase == SimulationPhase.Ended) { End("INCONCLUSIVE", "Practice ended before the route completed."); return; }
            if (s.HostTick == lastTick) return;
            lastTick = s.HostTick; var a = Self(); var p = a.Position.ToUnity();
            if (a.LifeState == LifeState.Falling || a.LifeState == LifeState.Stunned || a.LifeState == LifeState.Fainted)
            { End("INCONCLUSIVE", "Normal bot defense interrupted the route."); return; }
            if (phase == 0)
            {
                var delta = target - p;
                if (delta.magnitude < .025f) { Input(0, -normal); phase = 1; phaseTick = s.HostTick; return; }
                if (delta.magnitude < closest - .02f) { closest = delta.magnitude; progressTick = s.HostTick; }
                if (s.HostTick - progressTick > 90) { End("INCONCLUSIVE", "Flight route blocked in current map; no teleport fallback."); return; }
                Input(Mathf.Min(1, delta.magnitude / .7f), delta); return;
            }
            if (phase == 1)
            {
                Input(0, -normal); if (s.HostTick - phaseTick < 6) return;
                if (!runtime.World.TrySurface(new SurfaceQuery(a.ActorId, a.Position, aim, .25f), out support) ||
                    Vector3.Dot(support.WorldNormal.ToUnity(), normal) < .98f)
                { End("INCONCLUSIVE", "Requested authored support is not reachable at route endpoint."); return; }
                hasSupport = true; PerchToggle(); phase = 2; phaseTick = s.HostTick; return;
            }
            if (phase == 2)
            {
                Input(0, -normal);
                if (a.LifeState == LifeState.Surface && a.SurfaceAttachment.HasValue && s.HostTick - phaseTick >= 24) { HoldForRender("perched_toward", 3); return; }
                if (s.HostTick - phaseTick > 25) End("OBSERVED_FAILURE", "F did not establish Surface state.");
                return;
            }
            if (phase >= 3 && phase <= 5)
            {
                if (!a.SurfaceAttachment.HasValue) { End("OBSERVED_FAILURE", "Support lost while turning."); return; }
                Input(0, phase == 3 ? tangent : phase == 4 ? -tangent : normal);
                if (s.HostTick - phaseTick >= 24) HoldForRender(phase == 3 ? "turn_parallel" : phase == 4 ? "turn_180" : "turn_away", phase + 1);
                return;
            }
            if (phase == 6)
            {
                Input(1, tangent);
                if (!a.SurfaceAttachment.HasValue || a.LifeState != LifeState.Surface)
                { End("OBSERVED_FAILURE", "Support lost during crawl."); return; }
                if (s.HostTick - phaseTick >= 60)
                {
                    float distance = Vector3.Dot(p - crawlStart, tangent);
                    trace.Add(new { kind = "crawl_distance", meters = distance });
                    HoldForRender("surface_walk", 7);
                }
                return;
            }
            if (phase == 7)
            {
                Input(0, normal); detachStart = p; PerchToggle(); phase = 8; phaseTick = s.HostTick; return;
            }
            if (phase == 8)
            {
                if (a.SurfaceAttachment.HasValue || a.LifeState != LifeState.Flying)
                { End("OBSERVED_FAILURE", "First tick after F did not detach."); return; }
                trace.Add(new { kind = "detach_observed", ticksAfterAction = s.HostTick - phaseTick });
                HoldForRender("first_observed_detach", 9); return;
            }
            if (phase == 9)
            {
                Input(1, normal);
                if (s.HostTick - phaseTick >= 24)
                {
                    trace.Add(new { kind = "flight_distance", meters = Vector3.Dot(p - detachStart, normal) });
                    HoldForRender("flight_after_detach", 10);
                }
            }
        }
        catch (Exception e) { End("ERROR", e.ToString()); }
    }

    private static void HoldForRender(string label, int followingPhase)
    {
        if (manualCheckpoints) runtime.AutomaticTick = false;
        pending = true; holdBegan = EditorApplication.timeSinceStartup;
        pendingLabel = label; nextPhase = followingPhase; status = "WAITING_FOR_RENDER";
        Save(null);
    }

    private static void AfterRender(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (!active || holding || !gameCamera || !cameras.Contains(gameCamera) || lastRenderedFrame == Time.frameCount) return;
        try
        {
            if (!OwnsRuntime()) { End("ABORTED", "Runtime ownership lost before frame capture."); return; }
            lastRender = EditorApplication.timeSinceStartup; lastRenderedFrame = Time.frameCount;
            if (++frameSamples > 4000) { End("INCONCLUSIVE", "4000-frame sampling bound reached."); return; }
            // Current LateUpdate and skinning have run. Freeze only this probe's visual for review.
            var observation = Measure();
            frames.WriteLine(JsonConvert.SerializeObject(new { sample = frameSamples, frame = Time.frameCount,
                editorSeconds = lastRender - began, activeSeconds = lastRender - began - heldSeconds,
                unitySeconds = Time.unscaledTime, renderDeltaSeconds = Time.unscaledDeltaTime, phase,
                checkpointPending = pending, state = State(), observation }));
            if (!pending) return;
            if (manualCheckpoints)
            {
                animatorSpeed = animator.speed; animator.speed = 0;
                originalBindingEnabled = binding.enabled; binding.enabled = false; frozenBinding = true;
            }
            pending = false; holding = true; status = "HOLDING";
            string file = Path.Combine(folder, (++captureNumber).ToString("00") + "-" + pendingLabel + ".json");
            File.WriteAllText(file, JsonConvert.SerializeObject(new { label = pendingLabel, frame = Time.frameCount,
                unity = Application.unityVersion, state = State(), observation,
                scope = "Measured after a real game-camera frame. A PNG and human visual review must be captured separately; no native PASS is inferred." }, Formatting.Indented));
            trace.Add(new { kind = "rendered_checkpoint", pendingLabel, file, frame = Time.frameCount, state = State() });
            Save(null);
            if (!manualCheckpoints) Next();
        }
        catch (Exception e) { End("ERROR", e.ToString()); }
    }

    private static object Measure()
    {
        var a = Self();
        SurfaceContact currentSupport = default;
        bool resolvedCurrentAttachment = a.SurfaceAttachment.HasValue && runtime.World.ResolveSurface(a.SurfaceAttachment.Value, out currentSupport);
        if (resolvedCurrentAttachment)
        { support = currentSupport; hasSupport = true; }
        var animation = AnimationState();
        if (!hasSupport) return new { hasReferencePlane = false, animation,
            actorRoot = V(a.Position.ToUnity()), visualRoot = V(binding.transform.position), visualUp = V(binding.transform.up),
            note = "Approach before a resolved support query; no contact distance invented." };
        Vector3 n = support.WorldNormal.ToUnity().normalized, point = support.WorldPoint.ToUnity();
        var clearances = new Dictionary<string, float>();
        var verticesPerFoot = Feet.ToDictionary(f => f, f => 0);
        var footSums = Feet.ToDictionary(f => f, f => Vector3.zero);
        var minimumPoints = Feet.ToDictionary(f => f, f => Vector3.zero);
        foreach (var foot in Feet) clearances[foot] = float.PositiveInfinity;
        var failures = new List<string>(); float minimum = float.PositiveInfinity; int vertexCount = 0;
        foreach (var renderer in binding.View.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.sharedMesh) continue;
            var baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked, false);
                var vertices = baked.vertices;
                foreach (var v in vertices)
                { minimum = Mathf.Min(minimum, Vector3.Dot(renderer.transform.TransformPoint(v) - point, n)); vertexCount++; }
                var weights = renderer.sharedMesh.boneWeights;
                if (weights.Length != vertices.Length) throw new InvalidOperationException("Bone weights unavailable or count mismatch.");
                var bones = renderer.bones;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    int index = w.weight0 > .8f ? w.boneIndex0 : w.weight1 > .8f ? w.boneIndex1 :
                        w.weight2 > .8f ? w.boneIndex2 : w.weight3 > .8f ? w.boneIndex3 : -1;
                    if (index < 0 || index >= bones.Length || !bones[index] || !clearances.ContainsKey(bones[index].name)) continue;
                    string name = bones[index].name;
                    Vector3 worldVertex = renderer.transform.TransformPoint(vertices[i]);
                    float clearance = Vector3.Dot(worldVertex - point, n);
                    if (clearance < clearances[name]) { clearances[name] = clearance; minimumPoints[name] = worldVertex; }
                    footSums[name] += worldVertex;
                    verticesPerFoot[name]++;
                }
            }
            catch (Exception e) { failures.Add(renderer.name + ": " + e.Message); }
            finally { Object.DestroyImmediate(baked); }
        }
        var ground = binding.View.GetAnchor("GroundContact");
        var meshClearance = vertexCount > 0 ? (float?)minimum : null;
        var root = a.Position.ToUnity();
        var side = Vector3.Cross(n, binding.transform.forward).normalized;
        var footMotion = new Dictionary<string, object>();
        foreach (string foot in Feet)
        {
            if (verticesPerFoot[foot] == 0) { previousFeet.Remove(foot); previousClearances.Remove(foot); continue; }
            Vector3 centroid = footSums[foot] / verticesPerFoot[foot];
            bool consecutive = resolvedCurrentAttachment && previousSupport == support.Attachment.SurfaceId && previousFeet.ContainsKey(foot);
            float? distance = consecutive ? (float?)Vector3.ProjectOnPlane(centroid - previousFeet[foot], n).magnitude : null;
            footMotion[foot] = new { centroid = V(centroid), minimumVertexWorld = V(minimumPoints[foot]),
                tangentialCentroidDeltaMeters = distance,
                contactCandidateAtBothSamples = consecutive && Mathf.Abs(clearances[foot]) <= .0015f && Mathf.Abs(previousClearances[foot]) <= .0015f };
            previousFeet[foot] = centroid; previousClearances[foot] = clearances[foot];
        }
        previousSupport = resolvedCurrentAttachment ? support.Attachment.SurfaceId : 0;
        return new {
            hasReferencePlane = true, supportId = support.Attachment.SurfaceId, referencePlaneFromCurrentAttachment = resolvedCurrentAttachment,
            planePoint = V(point), planeNormal = V(n), actorRoot = V(root), visualRoot = V(binding.transform.position),
            rootClearanceMeters = Vector3.Dot(root - point, n), visualUp = V(binding.transform.up),
            upNormalDot = Vector3.Dot(binding.transform.up, n), forwardNormalDot = Vector3.Dot(binding.transform.forward, n),
            groundAnchorClearanceMeters = ground ? (float?)Vector3.Dot(ground.position - point, n) : null,
            minimumMeshClearanceMeters = meshClearance, vertexCount,
            footClearanceMeters = clearances.ToDictionary(p => p.Key, p => verticesPerFoot[p.Key] > 0 ? (float?)p.Value : null),
            verticesPerFoot, footMotion, completeFootCoverage = verticesPerFoot.All(p => p.Value > 0) && failures.Count == 0,
            supportingFeetWithin1_5mm = clearances.Count(p => verticesPerFoot[p.Key] > 0 && Mathf.Abs(p.Value) <= .0015f),
            metricErrors = failures,
            animation,
            suggestedReviewCameraPosition = V(root + n * .30f + side * .38f - binding.transform.forward * .24f),
            suggestedReviewCameraTarget = V(root), suggestedReviewCameraUp = V(n),
            note = "Signed distances to resolved plane, including evaluated vertices with >0.8 weight on named distal bones. Centroid motion is an apparent-slip proxy, not planted-contact proof. After detach the plane is historical. Missing feet/errors invalidate full coverage; no threshold constitutes visual acceptance."
        };
    }

    private static object AnimationState()
    {
        var current = animator.GetCurrentAnimatorStateInfo(0);
        var next = animator.GetNextAnimatorStateInfo(0);
        return new { current.fullPathHash, current.normalizedTime, current.length, current.loop,
            animatorSpeed = animator.speed, current.speed, current.speedMultiplier,
            inTransition = animator.IsInTransition(0), transitionNormalizedTime = animator.GetAnimatorTransitionInfo(0).normalizedTime,
            nextStateHash = next.fullPathHash, nextNormalizedTime = next.normalizedTime,
            clips = animator.GetCurrentAnimatorClipInfo(0).Select(c => new { name = c.clip ? c.clip.name : null, c.weight, length = c.clip ? c.clip.length : 0 }),
            nextClips = animator.GetNextAnimatorClipInfo(0).Select(c => new { name = c.clip ? c.clip.name : null, c.weight }) };
    }

    public static string Next()
    {
        if (!active || !holding || !OwnsRuntime()) throw new InvalidOperationException("No owned rendered checkpoint is holding.");
        if (nextPhase == 10) { End("COMPLETE_UNREVIEWED", "Route and rendered checkpoints collected; Director must review metrics/images."); return Status(); }
        Thaw(); if (manualCheckpoints) heldSeconds += EditorApplication.timeSinceStartup - holdBegan;
        trace.Add(new { kind = "checkpoint_resume", pendingLabel, manualCheckpoints,
            seconds = EditorApplication.timeSinceStartup - holdBegan, animatorTimelineWasContinuous = !manualCheckpoints });
        previousFeet.Clear(); previousClearances.Clear(); previousSupport = 0;
        lastRender = EditorApplication.timeSinceStartup;
        holding = false; phase = nextPhase; phaseTick = runtime.LatestSnapshot.HostTick; lastTick = uint.MaxValue;
        if (phase == 6) crawlStart = Self().Position.ToUnity();
        runtime.AutomaticTick = true; status = "RUNNING"; Save(null); return Status();
    }

    private static void Thaw()
    {
        if (!frozenBinding) return;
        if (animator) animator.speed = animatorSpeed;
        if (binding) binding.enabled = originalBindingEnabled;
        frozenBinding = false;
    }
    public static string Status() => JsonConvert.SerializeObject(new { active, status, surface, phase, holding, pending,
        checkpoint = pendingLabel, folder, frameSamples, manualCheckpoints, cameraWasChanged = false, nativeVisualAccepted = false });
    public static string Stop() { if (active) End("CANCELLED", "Stopped by Director."); return Status(); }
    private static void End(string result, string reason)
    {
        status = result;
        try { Save(reason); }
        finally { Cleanup(); }
    }
    private static void Save(string reason) => File.WriteAllText(Path.Combine(folder, "trace.json"), JsonConvert.SerializeObject(new {
        status, reason, surface, phase, pendingLabel, utc = DateTime.UtcNow, unity = Application.unityVersion,
        pid = System.Diagnostics.Process.GetCurrentProcess().Id, integrationExpected = "fc3d97b + cb9119e (Director must record actual HEAD/dirty state)",
        manualCheckpoints, frameSamples, probeAssembly = typeof(SurfaceVisualProbe).Assembly.FullName,
        target = V(target), normal = V(normal), tangent = V(tangent),
        scope = "Owned local training; normal authority inputs/actions; real bots and clock; manual checkpoint holds. No teleport, rule changes, online activity, asset saves or camera mutations.",
        nativeVisualAccepted = false, trace }, Formatting.Indented));
    private static void Cleanup()
    {
        EditorApplication.update -= Observe; RenderPipelineManager.endContextRendering -= AfterRender;
        EditorApplication.playModeStateChanged -= PlayModeChanged; AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        try
        {
            // An exception inside synchronous StartTraining can precede epoch assignment. Only reclaim
            // that partial training while Start is on the stack and no room has been created.
            bool owned = (app && runtime && ReferenceEquals(Read(app, "game"), runtime) &&
                (bool)Read(app, "training") && runtime.LocalPrincipal == "practice" &&
                epochEstablished && (runtime.LatestSnapshot == null || runtime.LatestSnapshot.SessionEpoch == epoch)) ||
                (!epochEstablished && trainingRequested && app &&
                (bool)Read(app, "training") && Read(app, "room") == null &&
                (runtime == null || ReferenceEquals(Read(app, "game"), runtime)));
            Thaw(); // References belong to the old visual even if another runtime has replaced it.
            if (owned)
            {
                if (runtime) { runtime.AutomaticTick = true; runtime.CaptureLocalInput = true; }
                app.CancelTraining(); // Normal lifecycle; never CancelTraining on a replacement runtime.
                Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
            }
        }
        finally
        {
            frames?.Dispose(); frames = null;
            active = holding = pending = trainingRequested = epochEstablished = false;
            runtime = null; binding = null; animator = null; gameCamera = null;
        }
    }
    private static void PlayModeChanged(PlayModeStateChange change)
    { if (active && change == PlayModeStateChange.ExitingPlayMode) End("CANCELLED", "Exiting Play Mode."); }
    private static void BeforeReload() { if (active) End("CANCELLED", "Assembly reload requested."); }
}
