# Let me sleep 0.5 — integration contract

Active revision following immutable v0.4.0. Godot 4.5.2, native Windows x64, protocol5. Preserve all historical packages and releases. Final release evidence is recorded separately; source checks are not proof of the final executable.

## Gameplay and view

Human remains FPS in rounds, TPS in the separate waiting patio. Mosquito is visible TPS, small relative to humans. W follows view yaw/pitch; releasing input brakes. Held E charges only the mosquito's own private mark; releasing cancels before attachment. Once attached, release keeps contact, and a new E press detaches. Assignment rotation is private, frozen while attached, and detach retains the calendar.

Human view and body yaw are separate. Looking down enters inspection at pitch below -0.70; leaving above -0.45 returns the body toward the view smoothly. Human view pitch reaches -1.92 and yaw is limited to ±75 degrees relative to the body during inspection. WASD stays relative to the view. HumanPose provides shared view_origin, view_direction, clamp_view_yaw, apply_view and aim_angles. Visible arms, body capsules, marks and strike gestures consume the same physical pose.

LMB is a manually aimed strike; Q is a legacy alternate binding for exactly that strike. No automatic body-band selection or snap to an insect. Input action carries click-time yaw/pitch with sequence, eliminating the race against the separate periodic movement packet. Sim.action(id,seq,verb,aim_yaw=NAN,aim_pitch=NAN) preserves older internal callers. Both omitted values use authoritative view; malformed pairs are rejected.

Eight front marks (paired chest, abdomen, forearm, thigh) are individually visible and reachable. Two rear marks are cooperative and only enabled with multiple humans. Excess insects privately wait in a fair FIFO rather than receive unreachable marks. Existing attachment is not stolen. Rotation and detach yield capacity fairly without revealing other insects' assignments or rotation timers.

Public strike contains physical origin/point/progress/tool/hand data for matching animation; private attack has id/state/hit/point/recovery. Human bite_feedback reports actual attached contacts only, including count/side. It never exposes free reservations. Where a held tool prevents hitting its own carrying arm, the other palm performs the same manually aimed strike.

Latest explicit user decision supersedes prior life rules: Blood and Tasks insects fall stunned35s instead of dying; no finite lives or respawn teleport. They remain alive/visible, cannot move/bite/help, and recover control at the fallen location with a new valid assignment/FIFO wait. Further hits do not reset stun. A nearby ally holding E and aiming with LOS accelerates total recovery rate to4x; multiple helpers do not stack, and leaving/releasing/obstruction interrupts assistance. All insects stunned does not end the round. Public state carries stunned/fall/help_target, private insect packets carry own stun/help. Survival alone keeps one-life elimination. Blood quota and Tasks goal/time remain authoritative. Humans1..5, mosquitoes up to12/max16, solo-vs-solo valid, no hunger. Task work+21s reserve/calendar/cutoff remain unchanged. Practice bots use public observations and own private packets, aim manual defense and help visible fallen allies.

## UI, appearance and environment

HUD leaves the center and bottom free: compact objective upper-left, clock/tool upper-right, contextual transient hints and actual bite feedback. Full controls are available through Esc/settings. No permanent full-width instruction panel.

Customization belongs to main menu only. AvatarPreview is an isolated SubViewport studio with role switch, drag rotation, bounded wheel zoom and reset. Profiles retain separate human/mosquito color and accessory values and add face/hair/outfit/accent. Three distinct model options per face/hair/outfit, six primary and accent colors, three accessories. Local persistence migrates old profiles with zero defaults and synchronizes sanitized appearances through lobby/round/practice. Appearance does not change combat dimensions or stats.

Original stylized cartoon room models improve bedroom, living room and kitchen silhouettes, materials, curved furniture, lighting and purposeful details. Existing map solids/routes remain authoritative. Labels are small, distance/LOS limited and suppressed where they obscure contact. No photorealistic or downloaded dependent art.

## Hosting and connection

One in-game Create request starts an owned hidden child for direct ENet. Parent passes an independent local hosting port and unique nonce/reply path. Child acknowledges actual bind with nonce/PID/port/error. Parent joins loopback only after valid acknowledgement; stale friend endpoint cannot affect hosting. Repeated clicks are ignored, bind failure remains explicit, cancel/retry are bounded. Exit/leave closes only the owned server; child checks parent lifetime and exits after parent crash. Room owner departure closes the room without migration. Reliable close notice gets a brief delivery window before owned child termination.

Network states distinguish DNS, UDP transport, room handshake, joined, cancelled, failed and host-closed. Explicit rejection is not replaced by a later timeout. Timeout does not infer NAT or firewall cause. DD3 remains the direct-address invitation format with scope guidance; copying a code never proves Internet reachability.

User's required final online experience is download, create inside game, share a code, friend joins from another house without another app/account/router configuration. Direct ENet alone does not satisfy this. EOSG2.3.0 was isolated-spiked for load/export/native Windows compatibility; DeviceID/lobby/P2P relay integration is separate unfinished work pending provider product/client setup and real two-network verification. No fake integrated code or implied working relay. Tailscale is not the selected user flow. SDK binaries/config credentials are not committed by the spike.

## Ownership and publication

Root: Client/Main/PracticeSession/BotBrain/Preferences/Cosmetics, integration, packaging, docs and GitHub publication. Simulation agent: Simulation/Arena/HumanPose and physical/rules checks. Visual agent: World/ActorView/AvatarPreview and native visual/movement evidence. UI agent: UI/Network/Invitation and connection/editor checks; subsequent EOS transport contract stays isolated until approved for integration.

Canonical public repository https://github.com/Sauri0/LetMeSleep . Root exclusively mutates Git/GitHub. No force push or blanket staging. Release ZIP assets are checked against manifests, downloaded and SHA256 compared before publication. No final0.5 claim until its actual exported executable and packages are checked. Internet reachability, multiple-network relay and human balance remain explicitly unverified until real evidence exists.
