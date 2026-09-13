"""Offline Newtonsoft check using actual nested DTO declarations and the real catalog."""
import argparse
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

p=argparse.ArgumentParser()
p.add_argument('--output',required=True)
p.add_argument('--catalog',required=True)
p.add_argument('--newtonsoft',required=True)
args=p.parse_args()
base=Path(__file__).parent
batch=(base/'HiggsfieldRoomUIBatch.cs').read_text(encoding='utf-8-sig')
fixture=(base/'RoomMapNativeFixture.cs').read_text(encoding='utf-8-sig')
isolation=(base/'FixtureIsolation.cs').read_text(encoding='utf-8-sig')
def block(source,marker):
    start=source.index(marker);end=source.index('{',start)+1;depth=1
    while depth:
        depth+=(source[end]=='{')-(source[end]=='}');end+=1
    return source[start:end]
dtos='\n'.join(block(batch,marker) for marker in (
    '[Serializable] private class Config','[Serializable] private class Catalog',
    '[Serializable] private class Entry','[Serializable] private class Report',
    '[Serializable] private class TrainingMapName'))
receipt=block(fixture,'[Serializable] public class Receipt')
filedto=block(isolation,'[Serializable] public sealed class SourceFile')
source='''using System;using System.IO;using System.Linq;using System.Collections.Generic;using Newtonsoft.Json;
class PrivateFixtureFonts {'''+filedto+'''}
class Program {
'''+dtos+receipt+'''
static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
static void Main(string[] args){
 var config=JsonConvert.DeserializeObject<Config>("{\\"catalogInput\\":\\"real-input.json\\",\\"headingFont\\":\\"custom-heading\\"}");
 Check(config.catalogInput=="real-input.json" && config.headingFont=="custom-heading" && config.bodyFont.Contains("Atkinson"),"private nested Config populated with defaults retained");
 var catalog=JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(args[0]));
 Check(catalog.entries.Length==5 && catalog.entries.Select(e=>e.mapId).Distinct().Count()==5 && catalog.entries.All(e=>!string.IsNullOrWhiteSpace(e.displayName)),"real catalog has five unique IDs and populated names");
 var expected=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[0])).RootElement.GetProperty("entries");
 Check(catalog.entries.Select(e=>e.mapId+"|"+e.displayName).SequenceEqual(expected.EnumerateArray().Select(e=>e.GetProperty("mapId").GetString()+"|"+e.GetProperty("displayName").GetString())),"IDs and names match independent JSON reader");
 var report=new Report{state="captured-awaiting-visual-review",checks720=true,checks1080=true,sourcesUnchanged=true,optionsRestored=true,
 originalOptions=3,restoredOptions=3,originalOptionsEnabled=false,restoredOptionsEnabled=false,
 catalogInput=args[0],catalogSha256="fixture-hash",fixture720="fixture-720",fixture1080="fixture-1080",failure="",
 sourceFiles=new List<PrivateFixtureFonts.SourceFile>{new PrivateFixtureFonts.SourceFile{path="font.asset",sha256="abc"}},
 maps=catalog.entries.Select(e=>new TrainingMapName{id=e.mapId,name=e.displayName}).ToArray()};
 var json=JsonConvert.SerializeObject(report,Formatting.Indented);
 var roundTrip=JsonConvert.DeserializeObject<Report>(json);
 Check(json!="{}" && roundTrip.maps.Length==5 && roundTrip.sourceFiles[0].sha256=="abc" && roundTrip.optionsRestored && roundTrip.originalOptions==3,"batch receipt preserves maps hashes and restoration fields");
 var receipt=new Receipt{state="failed",failure="explicit failure",width=1280,height=720};receipt.checks.Add("check");receipt.emissions.Add("room|"+catalog.entries[0].mapId);
 var status=JsonConvert.SerializeObject(receipt,Formatting.Indented);
 var decoded=JsonConvert.DeserializeObject<Receipt>(status);
 Check(status.Contains("\\"failed\\"") && decoded.failure=="explicit failure" && decoded.checks.Count==1 && decoded.emissions.Count==1 && decoded.width==1280,"fixture receipt and status remain readable");
 Console.WriteLine("Five JSON checks passed; no Unity execution.");
}
}
'''
assert 'JsonUtility' not in batch and 'JsonUtility' not in fixture
out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
(out/'Program.cs').write_text(source,encoding='utf-8')
(out/'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+escape(str(Path(args.newtonsoft).resolve()))+'</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
subprocess.run(['dotnet','run','--project',str(out/'Checks.csproj'),'--verbosity','quiet','--',str(Path(args.catalog).resolve())],check=True)
