using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LetMeSleep.Content.Characters;
using LetMeSleep.UI;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private readonly Dictionary<string, BasicCustomizationDraft> peerAppearances = new Dictionary<string, BasicCustomizationDraft>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> peerAppearanceTimes = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> appliedAppearance = new Dictionary<int, string>();
        private double appearanceAt;
        private void TickAppearance(double now)
        {
            if (now < appearanceAt) return; appearanceAt = now + 2;
            if (room?.Current != null && transport != null)
            {
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream,Encoding.UTF8,true);
                writer.Write((byte)1); Core.RoomWireCodec.WriteText(writer,lobby.Code,16); Core.RoomWireCodec.WriteText(writer,appearance.SkinColorId,16);
                Core.RoomWireCodec.WriteText(writer,appearance.PajamaColorId,16); Core.RoomWireCodec.WriteText(writer,appearance.MosquitoColorId,16);
                byte[] packet=stream.ToArray();
                foreach(var member in room.Current.Members) if(member.Id!=LocalId) transport.Send(member.Id,2,new ArraySegment<byte>(packet),true);
            }
            ApplyLiveAppearance();
        }
        private void ReceiveAppearance(string peer,byte channel,ArraySegment<byte> packet)
        {
            if(channel!=2||packet.Count>128||room?.Current==null||!room.Current.Members.Any(m=>m.Id==peer)) return;
            double now=UnityEngine.Time.realtimeSinceStartupAsDouble;
            if(peerAppearanceTimes.TryGetValue(peer,out double previous)&&now-previous<.25) return;
            
            try
            {
                using var stream=new MemoryStream(packet.Array,packet.Offset,packet.Count,false);
                using var reader=new BinaryReader(stream,Encoding.UTF8);
                if(reader.ReadByte()!=1 || Core.RoomWireCodec.ReadText(reader,16)!=lobby.Code) return;
                var draft=new BasicCustomizationDraft(AlfaRole.Human,Core.RoomWireCodec.ReadText(reader,16),Core.RoomWireCodec.ReadText(reader,16),Core.RoomWireCodec.ReadText(reader,16));
                if(stream.Position!=stream.Length||!ValidAppearance(draft)) return;
                peerAppearanceTimes[peer]=now; peerAppearances[peer]=draft; ApplyLiveAppearance();
            }
            catch(IOException) {} catch(InvalidDataException) {} catch(ArgumentException) {}
        }
        private void ApplyLiveAppearance()
        {
            if(menuCharacters && menuCharacters.activeInHierarchy)
                foreach(var view in menuCharacters.GetComponentsInChildren<CharacterView>()) ApplyAppearance(view,appearance);
            if(lobbyMovement && room?.Current!=null)
                foreach(var member in room.Current.Members)
                    if(lobbyMovement.TryGetVisual(member.Id,out var visual)) ApplyLive(visual.GetComponent<CharacterView>(),member.Id);
            if(game && activeRoster!=null)
                foreach(var actor in activeRoster)
                    if(game.World.Actors.TryGetValue(actor.ActorId,out var proxy)) ApplyLive(proxy.GetComponentInChildren<CharacterView>(),actor.OwnerPuid);
        }
        private void ApplyLive(CharacterView view,string owner)
        {
            if(!view) return;
            var draft = owner==(training?"practice":LocalId) ? appearance : peerAppearances.TryGetValue(owner,out var peer) ? peer : new BasicCustomizationDraft(AlfaRole.Human,"warm","blue","red");
            string key=draft.SkinColorId+"/"+draft.PajamaColorId+"/"+draft.MosquitoColorId;
            if(appliedAppearance.TryGetValue(view.GetInstanceID(),out var previous)&&previous==key) return;
            appliedAppearance[view.GetInstanceID()]=key; ApplyAppearance(view,draft);
        }
    }
}

