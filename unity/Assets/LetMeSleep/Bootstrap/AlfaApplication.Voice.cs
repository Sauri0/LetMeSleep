using System;
using LetMeSleep.Core;
using LetMeSleep.UI;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private VoiceRuntimeCoordinator voiceRuntime;

        private void StartVoiceRoom()
        {
            StopVoiceRoom();
            if (transport == null || lobby == null || ui == null) return;
            voiceRuntime = new VoiceRuntimeCoordinator(gameObject, transport, lobby, Mixer, ui);
            ApplyVoicePreferences();
        }

        private void SyncVoiceContext(RoomView view = null)
        {
            if (voiceRuntime == null) return;
            voiceRuntime.Sync(view ?? room?.Current, activeConfig, activeRoster, game, lobbyMovement, LocalId);
        }

        private void ApplyVoicePreferences()
        {
            voiceRuntime?.Configure(settings.VoiceDevice, settings.PushToTalkBinding, settings.VoiceVolume);
        }

        private void StopVoiceRoom()
        {
            voiceRuntime?.Dispose(); voiceRuntime = null;
        }

        public void SetLocalVoiceMuted(bool muted) => voiceRuntime?.SetLocalMuted(muted);
        public void SetPeerVoiceMuted(string memberId, bool muted) => voiceRuntime?.SetPeerMuted(memberId, muted);

        public void BeginPushToTalkRebind(Action<string, string> completed)
        {
            voiceRuntime?.BeginRebind((path, label) =>
            {
                settings.PushToTalkBinding = path;
                SavePreferences(); PresentPreferences();
                completed?.Invoke(path, label);
            });
        }

        private void OnApplicationFocus(bool focused) => voiceRuntime?.SetApplicationFocused(focused);
        private void OnApplicationPause(bool paused) => voiceRuntime?.SetApplicationPaused(paused);
    }
}
