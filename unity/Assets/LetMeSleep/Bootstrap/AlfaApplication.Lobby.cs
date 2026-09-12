using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LetMeSleep.Core;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using LetMeSleep.Online;
using UnityEngine;

namespace LetMeSleep.Bootstrap
{
    public sealed partial class AlfaApplication
    {
        private LobbyMovementRuntime lobbyMovement;
        private MessageFraming lobbyFrames;
        private string lobbyRosterKey;
        private void SyncLobbyMovement(RoomView view)
        {
            string key = string.Join("|",view.Members.Select(m=>m.Id));
            if (lobbyMovement && key == lobbyRosterKey) return;
            var occupied = new System.Collections.Generic.List<Vector3>();
            if (lobbyMovement) foreach(var member in view.Members)
                if(lobbyMovement.TryGetVisual(member.Id,out var existing)) occupied.Add(existing.transform.position);
            var roster = view.Members.Select(m => {
                if(lobbyMovement && lobbyMovement.TryGetVisual(m.Id,out var existing)) return new LobbySpawn(m.Id,existing.transform.position);
                Vector3 spawn = map.LobbySpawnPoints.Select(p=>p.position).OrderByDescending(p => occupied.Count==0 ? 999 : occupied.Min(o=>(p-o).sqrMagnitude)).First();
                occupied.Add(spawn); return new LobbySpawn(m.Id,spawn);
            }).ToArray();
            if (!lobbyMovement)
            {
                lobbyMovement = new GameObject("WaitingRoomPlayers").AddComponent<LobbyMovementRuntime>();
                lobbyMovement.GetComponent<UnityGameplayWorld>().MapRoot=map.transform;
                lobbyMovement.HumanPrefab=HumanPrefab; lobbyMovement.LocalCamera=MenuCamera;
                lobbyMovement.MouseSensitivity=.002f*settings.HumanSensitivity; lobbyMovement.InvertY=settings.InvertY;
                using var hash=SHA256.Create(); ulong epoch=BitConverter.ToUInt64(hash.ComputeHash(Encoding.UTF8.GetBytes(lobby.Code)),0);
                lobbyMovement.Bind(epoch==0?1:epoch,LocalId,lobby.IsOwner,roster,checked((uint)view.Revision + 1));
                lobbyMovement.gameObject.AddComponent<LetMeSleep.Presentation.Gameplay.LobbyVisualPresenter>().Bind(lobbyMovement);
                lobbyMovement.SetInputBlocked(true);
                lobbyFrames=new MessageFraming(); lobbyFrames.MessageReceived+=ReceiveLobbyMessage;
                transport.PacketReceived+=ReceiveLobbyPacket;
                lobbyMovement.InputReady+=SendLobbyInput;
                lobbyMovement.SnapshotReady+=SendLobbySnapshot;
            }
            else lobbyMovement.SetRoster(roster,checked((uint)view.Revision + 1));
            lobbyRosterKey=key;
        }
        public void SetLobbyExploration(bool exploring) { lobbyMovement?.SetInputBlocked(!exploring); }
        private void StopLobbyMovement()
        {
            if (transport!=null) transport.PacketReceived-=ReceiveLobbyPacket;
            if (lobbyFrames!=null) { lobbyFrames.MessageReceived-=ReceiveLobbyMessage; lobbyFrames.Clear(); lobbyFrames=null; }
            if (lobbyMovement) { lobbyMovement.Unbind(); lobbyMovement.gameObject.SetActive(false); Destroy(lobbyMovement.gameObject); lobbyMovement=null; }
            lobbyRosterKey=null; appliedAppearance.Clear();
        }
        private void SendLobbyInput(LobbyMoveCommand command)
        {
            using var stream=new MemoryStream(); using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            writer.Write(command.SessionEpoch); writer.Write(command.RosterRevision); writer.Write(command.Sequence);
            writer.Write(command.Move.x); writer.Write(command.Move.y); writer.Write(command.Yaw);
            SendLobby(lobby.OwnerId,1,stream.ToArray());
        }
        private void SendLobbySnapshot(LobbySnapshot snapshot)
        {
            using var stream=new MemoryStream(); using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            writer.Write(snapshot.SessionEpoch); writer.Write(snapshot.RosterRevision); writer.Write(snapshot.HostTick); writer.Write((byte)snapshot.Poses.Count);
            foreach(var pose in snapshot.Poses)
            {
                RoomWireCodec.WriteText(writer,pose.PlayerId,128); WriteVector(writer,pose.Position); WriteVector(writer,pose.Velocity);
                writer.Write(pose.Yaw); writer.Write(pose.Grounded); writer.Write(pose.LastInputSequence); writer.Write(pose.MotionPhase);
            }
            byte[] data=stream.ToArray();
            foreach(var member in room.Current.Members) if(member.Id!=LocalId) SendLobby(member.Id,2,data);
        }
        private void SendLobby(string member,byte kind,byte[] data)
        { if(lobbyFrames!=null) foreach(var packet in lobbyFrames.Encode(kind,data)) transport.Send(member,3,new ArraySegment<byte>(packet),false); }
        private void ReceiveLobbyPacket(string peer,byte channel,ArraySegment<byte> packet)
        {
            if(channel==3 && room.Current?.Phase==RoomPhase.Waiting && room.Current.Members.Any(m=>m.Id==peer))
                lobbyFrames?.Accept(peer,packet,Time.realtimeSinceStartupAsDouble);
        }
        private void ReceiveLobbyMessage(string peer,byte kind,byte[] packet)
        {
            if(!lobbyMovement || room.Current?.Phase!=RoomPhase.Waiting || packet.Length>8192) return;
            try
            {
                using var stream=new MemoryStream(packet,false); using var reader=new BinaryReader(stream,Encoding.UTF8);
                ulong epoch=reader.ReadUInt64(); uint revision=reader.ReadUInt32(), sequence=reader.ReadUInt32();
                if(kind==1 && lobby.IsOwner)
                {
                    var move=new Vector2(reader.ReadSingle(),reader.ReadSingle()); float yaw=reader.ReadSingle();
                    if(stream.Position==stream.Length) lobbyMovement.SubmitMove(peer,new LobbyMoveCommand(epoch,revision,sequence,move,yaw));
                }
                else if(kind==2 && !lobby.IsOwner && peer==lobby.OwnerId)
                {
                    int count=reader.ReadByte(); if(count<1||count>RoomRules.Capacity||count!=room.Current.Members.Count) return;
                    var poses=new LobbyPose[count];
                    for(int i=0;i<count;i++)
                    {
                        string id=RoomWireCodec.ReadText(reader,128); var position=ReadVector(reader); var velocity=ReadVector(reader);
                        float yaw=reader.ReadSingle(); byte grounded=reader.ReadByte(); if(grounded>1) return;
                        uint ack=reader.ReadUInt32(); float motion=reader.ReadSingle();
                        if(!room.Current.Members.Any(m=>m.Id==id)) return;
                        poses[i]=new LobbyPose(id,position,velocity,yaw,grounded==1,ack,motion);
                    }
                    if(stream.Position==stream.Length) lobbyMovement.ApplySnapshot(new LobbySnapshot(epoch,revision,sequence,poses));
                }
            }
            catch(IOException) { }
            catch(InvalidDataException) { }
            catch(ArgumentException) { }
        }
        private static void WriteVector(BinaryWriter writer,Vector3 value) { writer.Write(value.x);writer.Write(value.y);writer.Write(value.z); }
        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    }
}



