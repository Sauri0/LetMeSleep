using System;
using System.Collections.Generic;
using LetMeSleep.Content.Characters;
using LetMeSleep.Core.Customization;
using LetMeSleep.UI;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private readonly AppearancePeerState peerAppearanceState = new AppearancePeerState(ValidAppearance);
        private readonly Dictionary<int, string> appliedAppearance = new Dictionary<int, string>();
        private double appearanceAt;

        private bool TryBuildAppearancePacket(string roomCode, out byte[] packet)
        {
            packet = null;
            if (string.IsNullOrEmpty(roomCode)) return false;
            if (TryGetPublishedModularAppearance(out _, out var catalog, out var published))
                return AppearanceWireCodec.TryEncode(catalog, roomCode, published, out packet);
            // Retained V2/future data must never be silently replaced online by basic defaults.
            if (loadedPreferenceSchema != 1 || preferenceWritesBlocked || preferenceStore?.WriteBlocked == true) return false;
            return ValidAppearance(appearance) && AppearancePeerState.TryEncodeLegacy(roomCode, appearance, out packet);
        }

        private void TickAppearance(double now)
        {
            DressFreshModularVisuals(now);
            if (now < appearanceAt) return;
            appearanceAt = now + 2;
            peerAppearanceState.Synchronize(room?.Current, lobby?.Code);
            if (room?.Current != null && transport != null && TryBuildAppearancePacket(lobby?.Code, out var packet))
                foreach (var member in room.Current.Members)
                    if (member.Id != LocalId && member.Connected) transport.Send(member.Id, 2, new ArraySegment<byte>(packet), true);
            ApplyLiveAppearance();
        }
        private void ReceiveAppearance(string peer,byte channel,ArraySegment<byte> packet)
        {
            if (channel != 2 || peer == LocalId) return;
            var catalog = ModularCustomizationAvailable ? modularCustomizationRuntime.Snapshot : null;
            if (peerAppearanceState.TryReceive(peer, channel, packet, UnityEngine.Time.realtimeSinceStartupAsDouble,
                room?.Current, lobby?.Code, catalog,
                selection => modularCustomizationRuntime != null && modularCustomizationRuntime.CanApply(selection, out _)))
                ApplyLiveAppearance();
        }
        private void ApplyLiveAppearance()
        {
            if(menuCharacters && menuCharacters.activeInHierarchy)
                foreach(var view in menuCharacters.GetComponentsInChildren<CharacterView>()) ApplyLive(view, LocalId);
            if(lobbyMovement && room?.Current!=null)
                foreach(var member in room.Current.Members)
                    if(lobbyMovement.TryGetVisual(member.Id,out var visual)) ApplyLive(visual.GetComponentInChildren<CharacterView>(),member.Id);
            if(game && activeRoster!=null)
                foreach(var actor in activeRoster)
                    if(game.World.Actors.TryGetValue(actor.ActorId,out var proxy)) ApplyLive(proxy.GetComponentInChildren<CharacterView>(),actor.OwnerPuid);
        }
        private double freshVisualsAt;

        /// <summary>
        /// Modular appearances are applied as soon as a menu, lobby or gameplay actor appears (checked four times a
        /// second) instead of at the next two-second appearance tick, so a new actor never shows the authored
        /// default look (pajama and nightcap) before switching to its owner's outfit.
        /// </summary>
        private void DressFreshModularVisuals(double now)
        {
            if (!ModularCustomizationAvailable || now < freshVisualsAt) return;
            freshVisualsAt = now + .25;
            if (menuCharacters && menuCharacters.activeInHierarchy)
                foreach (var view in menuCharacters.GetComponentsInChildren<CharacterView>()) DressIfFresh(view, LocalId);
            if (lobbyMovement && room?.Current != null)
                foreach (var member in room.Current.Members)
                    if (lobbyMovement.TryGetVisual(member.Id, out var visual) && visual) DressIfFresh(visual.GetComponentInChildren<CharacterView>(), member.Id);
            if (game && activeRoster != null)
                foreach (var actor in activeRoster)
                    if (game.World.Actors.TryGetValue(actor.ActorId, out var proxy) && proxy) DressIfFresh(proxy.GetComponentInChildren<CharacterView>(), actor.OwnerPuid);
        }

        private void DressIfFresh(CharacterView view, string owner)
        {
            if (!view) return;
            var assembler = view.GetComponent<CharacterModularVisualAssembler>();
            if (assembler && !assembler.HasAppliedParts) ApplyLive(view, owner);
        }

        private void ApplyLive(CharacterView view,string owner)
        {
            if (!view) return;
            bool local = owner == (training ? "practice" : LocalId);
            if (ModularCustomizationAvailable)
            {
                var host = view.GetComponent<CharacterCustomizationHost>();
                if (!host) return;
                AppearanceSelection selection;
                if (local) selection = publishedModularAppearance;
                else if (!peerAppearanceState.TryGetModular(owner, modularCustomizationRuntime.Snapshot.NetworkFingerprint, out selection))
                {
                    if (peerAppearanceState.TryGetLegacy(owner, out var legacy))
                    {
                        if (!modularCustomizationRuntime.TryMigrate(legacy, out selection, out _)) return;
                    }
                    else selection = modularCustomizationRuntime.Snapshot.DefaultSelection();
                }
                // The host identifies the gameplay role. The editing tab never changes it.
                // The assembler preserves the previous view when a candidate cannot be applied.
                if (TryApplyModularAppearance(view, selection, host.Role, out _)) appliedAppearance.Remove(view.GetInstanceID());
                return;
            }
            if (loadedPreferenceSchema != 1 || preferenceWritesBlocked || preferenceStore?.WriteBlocked == true) return;
            var draft = local ? appearance : peerAppearanceState.TryGetLegacy(owner, out var peer)
                ? peer : new BasicCustomizationDraft(AlfaRole.Human, "warm", "blue", "red");
            string key = draft.SkinColorId + "/" + draft.PajamaColorId + "/" + draft.MosquitoColorId;
            if (appliedAppearance.TryGetValue(view.GetInstanceID(), out var previous) && previous == key) return;
            ClearModularAppearance(view);
            ApplyAppearance(view, draft);
            appliedAppearance[view.GetInstanceID()] = key;
        }
    }
}

