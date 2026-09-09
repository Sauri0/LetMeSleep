# EOS transport for Windows

The game uses EOSG 2.3.0 with a local receive-path patch, Epic Online Services
SDK 1.19.1.2 and Godot 4.5.2. `build-manifest.json` pins the source revisions,
SDK archive hash, compiled source hashes and both shipped binary hashes.

To reproduce the native build, clone
https://github.com/3ddelano/epic-online-services-godot at commit
`e84320567a3a17d305478f5796707e69d2bdac4f`. Initialize its `godot-cpp` submodule
at `54136ee8357c5140a3775c54f08db5f7deda2058`. Apply `eosg-2.3.0.patch` with
`git apply --check` followed by `git apply`. Obtain the official SDK from Epic
and place its `SDK` directory under `thirdparty/eos-sdk/SDK`. The upstream SDK
submodule is not needed; the SDK itself is not included in this repository.

With LLVM-MinGW 20260826 UCRT x86_64 on PATH and SCons 4.10.1:

```powershell
python -m SCons -j2 platform=windows arch=x86_64 use_mingw=yes target=template_release dev_build=no
python -m SCons -j2 platform=windows arch=x86_64 use_mingw=yes target=template_debug dev_build=yes
```

Copy the built DLLs from `sample/addons/epic-online-services-godot/bin/windows`
to the matching game addon directory. Keep the SDK and XAudio redistributables
alongside the extension as specified by its `.gdextension` file. Preserve the
notices under `game/addons/epic-online-services-godot/licenses` in distributions.
The game descriptor targets Windows x86_64 only.

The patch validates packet lengths and reliability fields and binds peer IDs
to authenticated EOS Product User IDs. It also includes two required scoped
enum casts and a MinGW linker option correction. The manifest's 291 checks per
variant cover native loading, API availability and teardown, with no SDK
initialization. They do not certify P2P, relay or Internet connectivity.

## Product configuration

Create a product client with the Peer2Peer policy in Epic's Developer Portal.
For the release build, put the five `[eos]` fields (`product_id`, `sandbox_id`,
`deployment_id`, `client_id`, `client_secret`) in `work/eos-config.local.cfg`.
The build copies this ignored file to `game/eos.local.cfg` for inclusion in the
Windows executable. Source checkouts can also pass `--eos-config=<path>` after
Godot's `--` separator. Do not commit either local configuration file.

These are the distributed game client's credentials, with user-required
Peer2Peer permissions. Never substitute trusted-server or administrative
credentials. Players receive the configured release and need no Developer
Portal access. The hosting player runs the game server; EOS supplies identity,
lobby discovery and direct-or-relayed peer connections.
