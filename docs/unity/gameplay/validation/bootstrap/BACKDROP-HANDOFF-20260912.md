# Bootstrap backdrop hookup — 2026-09-12

Director authorized this separate Bootstrap delta after native Quiesce checks. Source `AlfaApplication.cs` was compared against central 61956fa: its pre-change contents match logically. This hookup depends on UI 1e795ed (central integration 7fe86a2), whose `ScreenChanged` contract is in `docs/unity/ui/BACKDROP-PREVIEW-CONTRACT-20260912.md`.

## Behavior

Bootstrap subscribes to `ui.ScreenChanged` and immediately synchronizes `CurrentScreen`. In Customization it retains the exact current `menuCharacters` / `MenuCharacterDisplay` root and its previous `activeSelf`, then deactivates only that decorative root. Repeated Customization synchronization does not overwrite the saved prior state. Confirmation modals leave the screen unchanged, so the backdrop stays hidden.

Leaving Customization restores that same root's prior state; it does not reactivate a root already hidden, or one now owned by lobby movement. Map replacement drops the old root reference and resynchronizes the newly created menu root before the next rendered frame. Quiesce unsubscribes before UI teardown, and the handler ignores calls during shutdown. No scene-wide object search is used.

Map, lighting, cameras, preview stage/texture/instance, player/network actors and their physics are unchanged. Source changes are confined to AlfaApplication.cs plus the offline compiler's optional `-UiAssembly` input and this document. Nothing was imported into ScriptAssemblies or Assets.

## Offline evidence

The first compile against the existing central UI DLL correctly failed because that DLL did not yet contain ScreenChanged. The new central UI source was copied read-only to `N:/LetMeSleep/Validation/BootstrapBackdrop-20260912/UI-source-20260912-2030`, built as `LetMeSleep.UI` against the real Core assembly, and compiled with **0 errors, 0 warnings**. This is not UI's combined contract-check assembly and was not copied over Unity's DLL.

Bootstrap Editor and Player paths then compiled with **0 errors, 0 warnings** in `N:/LetMeSleep/Validation/BootstrapBackdrop-20260912/20260912-202840-813`. Source/dependency hashes and logs are retained. Reproduce with `Compile-Quiesce.ps1 -UiAssembly N:/LetMeSleep/Validation/BootstrapBackdrop-20260912/UI-source-20260912-2030/bin/Debug/netstandard2.1/LetMeSleep.UI.dll -OutputRoot N:/LetMeSleep/Validation/BootstrapBackdrop-20260912` or use freshly imported central UI assemblies. No Unity process, native capture or visual acceptance was performed by this worker.

## Director's native check

Use the actual controller: menu → Personalizar → humano/mosquito → unsaved change → Escape → Seguir editando → Escape → Salir. Capture the gap between panels and the preview in each context. Repeat entry/exit and LoadMap while Customization is open. Verify the prior decorative root stays hidden if it was already hidden before entering, and that lobby movement's hiding is not reversed. Check Quiesce/OnDestroy after subscription without a late callback or reactivation. A rendered result, not compilation, establishes that no decorative hand/character remains visible through the panel gap.
