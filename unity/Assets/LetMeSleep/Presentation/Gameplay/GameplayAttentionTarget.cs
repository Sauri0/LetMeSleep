using LetMeSleep.Gameplay.Unity;
using UnityEngine;

namespace LetMeSleep.Presentation.Gameplay
{
    // Sets only a visual target after pose binding1100 and before the shared facial writer1200.
    [DefaultExecutionOrder(1150)]
    [DisallowMultipleComponent]
    public sealed class GameplayAttentionTarget : MonoBehaviour
    {
        private GameplayRuntime game;
        private GameplayActorProxy proxy;
        private LobbyMovementRuntime lobby;
        private string player;
        private VisualAttentionRig attention;
        private ulong epoch,round;
        private bool identityCaptured;
        public void Bind(GameplayRuntime runtime,GameplayActorProxy actor,VisualAttentionRig rig)
        { game=runtime; proxy=actor; attention=rig; lobby=null; identityCaptured=false; }
        public void Bind(LobbyMovementRuntime runtime,string playerId,VisualAttentionRig rig)
        { lobby=runtime; player=playerId; attention=rig; game=null; proxy=null; epoch=runtime.SessionEpoch; identityCaptured=true; }
        private void LateUpdate()
        {
            if(!attention || !attention.IsConfigured) return;
            Vector3 direction;
            if(game)
            {
                if(!proxy || game.World==null || !game.World.Actors.TryGetValue(proxy.ActorId,out var current) || current!=proxy || game.LatestSnapshot==null)
                { attention.ClearLookTarget(); return; }
                var snapshot=game.LatestSnapshot;
                if(!identityCaptured) { epoch=snapshot.SessionEpoch; round=snapshot.RoundId; identityCaptured=true; }
                if(epoch!=snapshot.SessionEpoch || round!=snapshot.RoundId) { attention.ClearLookTarget(); return; }
                if(proxy.ActorId==game.LocalActorId) direction=game.LocalViewForward.ToUnity();
                else if(proxy.State!=null) direction=proxy.State.ViewForward.ToUnity();
                else { attention.ClearLookTarget(); return; }
            }
            else if(lobby)
            {
                if(!lobby.IsBound || lobby.SessionEpoch!=epoch || !lobby.TryGetVisual(player,out var current) || !current ||
                    (current.transform!=transform && !transform.IsChildOf(current.transform)))
                { attention.ClearLookTarget(); return; }
                // Lobby protocol supplies yaw only. Use the actual interpolated visual forward, never invent remote pitch.
                direction=current.transform.forward;
            }
            else { attention.ClearLookTarget(); return; }
            if(direction.sqrMagnitude>.001f) attention.SetLookPoint(attention.LookOrigin.position+direction.normalized*8);
            else attention.ClearLookTarget();
        }
        private void OnEnable() { if(attention) attention.enabled=true; }
        private void OnDisable() { if(attention) { attention.ClearLookTarget(); attention.enabled=false; } }
    }
}
