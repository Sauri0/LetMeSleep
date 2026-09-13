"""Offline logic checks; extracts production methods. No Unity/render/network execution.
Usage: python CheckRoomMaps.py --repository PATH --output PATH --core PATH_TO_CORE_DLL
"""
import argparse
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser()
parser.add_argument('--repository', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--core', required=True)
args = parser.parse_args()
root = Path(args.repository) / 'unity/Assets/LetMeSleep/UI/Runtime'
controller = (root / 'AlfaUiController.cs').read_text(encoding='utf-8-sig')
contracts = (root / 'AlfaUiContracts.cs').read_text(encoding='utf-8-sig')

def block(source, marker):
    start = source.index(marker)
    end = source.index('{', start) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

dtos = '\n'.join(block(contracts, marker) for marker in (
    '    public interface IRoomMapActions', '    public sealed class TrainingMapOption',
    '    public sealed class LobbyMemberUiState', '    public sealed class LobbyUiState',
    '    public static class AlfaRoomCode'))
methods = '\n'.join(block(controller, marker) for marker in (
    '        public void SetRoomMaps', '        private void CycleRoomMap',
    '        private void UpdateRoomMapView', '        private void ChangeLobbyHumanCount'))
predicates = controller[controller.index('        private bool LobbyBusy'):controller.index('        private void CycleRoomMap')]
source = '''using System; using System.Linq; using System.Collections.Generic; using LetMeSleep.Core;
''' + dtos + '''
class Label { public string text; }
class Button { public bool interactable; }
interface Actions { void SetHumanCount(int? count); }
class BasicActions : Actions { public void SetHumanCount(int? count) {} }
class Spy : Actions, IRoomMapActions {
 public List<string> Maps = new(); public List<int?> Counts = new(); public Action<string> OnMap;
 public void SetRoomMap(string id) { Maps.Add(id); OnMap?.Invoke(id); }
 public void SetHumanCount(int? count) { Counts.Add(count); }
}
class Program {
 const string HousePatioMapId="house-patio-v1";
 TrainingMapOption[] roomMaps=Array.Empty<TrainingMapOption>();
 LobbyUiState lobbyState; bool lobbyReadyLatched, lobbyStartLatched, lobbyRulesLatched;
 Label roomMapLabel=new(), lobbyStatus=new(); Button roomMapPrevious=new(), roomMapNext=new();
 Actions actions=new Spy();
 void UpdateLobbyControls() {} // Rendering-only collaborator; not part of offline coverage.
''' + methods + predicates + '''
 void Present(LobbyUiState state) {
 lobbyState=state; lobbyReadyLatched=state.ReadyPending;
 lobbyStartLatched=state.StartPending; lobbyRulesLatched=state.RulesPending;
 UpdateRoomMapView();
 }
 static LobbyUiState State(string id=HousePatioMapId, bool host=true, bool waiting=true,
 bool ready=false, bool start=false, bool rules=false, string label="CASA CON PATIO") =>
 new LobbyUiState(host,"ABCDEF",null,false,ready,null,true,"",id,label,
 startPending:start,isWaiting:waiting,rulesPending:rules);
 static int checks;
 static void Check(bool ok,string name) { if(!ok)throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
 static void Main() {
 var p=new Program(); var spy=(Spy)p.actions;
 p.Present(State()); Check(p.roomMapLabel.text=="CASA CON PATIO" && !p.roomMapNext.interactable,"default is read-only alpha");
 var maps=new List<TrainingMapOption>{new("fixture-a","Mapa A"),new("fixture-b","Mapa B")};
 p.SetRoomMaps(maps); maps.Clear(); Check(p.roomMaps.Length==2,"catalog copied");
 p.CycleRoomMap(1); Check(spy.Maps.SequenceEqual(new[]{"fixture-a"}),"host requests first catalog ID from alpha");
 Check(p.lobbyState.MapId==HousePatioMapId && p.roomMapLabel.text=="CASA CON PATIO","no optimistic state or label");
 p.CycleRoomMap(1); p.ChangeLobbyHumanCount(2);
 Check(spy.Maps.Count==1 && spy.Counts.Count==0 && !p.roomMapNext.interactable,"local latch blocks duplicate and competing rules");
 p.Present(State(rules:true)); p.CycleRoomMap(-1); Check(spy.Maps.Count==1,"authoritative pending blocks");
 p.Present(State("fixture-a")); Check(p.roomMapLabel.text=="Mapa A" && p.roomMapNext.interactable,"authoritative acknowledgement updates label and unlocks");
 p.CycleRoomMap(-1); Check(spy.Maps.Last()=="fixture-b","previous wraps");
 p.Present(State("fixture-b")); p.CycleRoomMap(1); Check(spy.Maps.Last()=="fixture-a","next wraps");
 p.Present(State()); p.CycleRoomMap(-1); Check(spy.Maps.Last()=="fixture-b","previous from unknown selects last");
 p.Present(State()); Check(p.roomMapNext.interactable && p.roomMapLabel.text=="CASA CON PATIO","rejection snapshot restores controls without changing map");
 foreach(var state in new[]{State(host:false),State(waiting:false),State(ready:true),State(start:true),State(rules:true)}) {
 p.Present(state); var before=spy.Maps.Count; p.CycleRoomMap(1);
 Check(spy.Maps.Count==before && !p.roomMapNext.interactable,"guest/phase/pending callback guard"); }
 p.Present(State()); p.actions=new BasicActions(); p.CycleRoomMap(1); p.UpdateRoomMapView();
 Check(!p.roomMapNext.interactable,"missing capability read-only"); p.actions=spy;
 p.Present(State("fixture-a")); p.SetRoomMaps(new[]{new TrainingMapOption("fixture-a","Renombrado")});
 Check(p.roomMapLabel.text=="Renombrado" && !p.roomMapNext.interactable,"single current map read-only");
 p.SetRoomMaps(new[]{new TrainingMapOption("fixture-b","Mapa B")}); p.CycleRoomMap(1);
 Check(spy.Maps.Last()=="fixture-b" && p.roomMapLabel.text=="fixture-a","removed current remains authoritative and single alternative selectable");
 p.Present(State("external",label:"Mapa externo")); p.SetRoomMaps(null); p.CycleRoomMap(1);
 Check(p.roomMapLabel.text=="Mapa externo" && !p.roomMapNext.interactable,"empty catalog retains authoritative label");
 p.SetRoomMaps(new[]{new TrainingMapOption("fixture-a","A")});
 try {p.SetRoomMaps(new[]{new TrainingMapOption("x","X"),new TrainingMapOption("x","Y")});throw new Exception("accepted duplicate");}catch(ArgumentException){}
 try {p.SetRoomMaps(new TrainingMapOption[]{null});throw new Exception("accepted null");}catch(ArgumentException){}
 Check(p.roomMaps.Length==1 && p.roomMaps[0].Id=="fixture-a","invalid catalogs rejected atomically");
 p.Present(State()); spy.OnMap=id=>p.Present(State(id)); p.CycleRoomMap(1);
 Check(p.roomMapLabel.text=="A" && !p.lobbyRulesLatched,"synchronous authoritative callback supported");
 Console.WriteLine(checks+" logic checks passed; no native rendering, network or input coverage.");
 }
}
'''
# Verify the real presenter consumes the pending flags used by the harness.
present = block(controller, '        public void PresentLobby')
for assignment in ('lobbyReadyLatched = state.ReadyPending;', 'lobbyStartLatched = state.StartPending;', 'lobbyRulesLatched = state.RulesPending;'):
    assert assignment in present
assert 'UpdateRoomMapView();' in present and 'UpdateLobbyControls();' in present
assert 'if (!CanEditLobbyRules || !lobbyState.CanStart) return;' in block(controller, '        private void BeginRound')
assert 'if (lobbyState == null || !lobbyState.IsWaiting || LobbyBusy) return;' in block(controller, '        private void BuildLobby')
out = Path(args.output)
out.mkdir(parents=True, exist_ok=True)
(out / 'Program.cs').write_text(source, encoding='utf-8')
(out / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup><Reference Include="LetMeSleep.Core"><HintPath>'+escape(str(Path(args.core).resolve()))+'</HintPath></Reference></ItemGroup></Project>', encoding='utf-8')
subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '--verbosity', 'quiet'], check=True)
