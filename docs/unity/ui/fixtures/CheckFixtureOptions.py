"""Exercise extracted production SessionState guard with API doubles; never opens Unity."""
import argparse
from pathlib import Path
import subprocess

p=argparse.ArgumentParser()
p.add_argument('--output',required=True)
args=p.parse_args()
source=(Path(__file__).parent/'FixtureIsolation.cs').read_text(encoding='utf-8-sig')
start=source.index('    public static class FixtureEditorOptions')
end=source.index('{',start)+1
depth=1
while depth:
    depth+=(source[end]=='{')-(source[end]=='}')
    end+=1
guard=source[start:end]
code='''using System;using System.Collections.Generic;
enum EnterPlayModeOptions {None=0,DisableDomainReload=1,DisableSceneReload=2}
static class EditorSettings {public static bool enterPlayModeOptionsEnabled;public static EnterPlayModeOptions enterPlayModeOptions;}
static class SessionState {
 static Dictionary<string,object> values=new();
 public static bool GetBool(string k,bool fallback)=>values.TryGetValue(k,out var v)?(bool)v:fallback;
 public static int GetInt(string k,int fallback)=>values.TryGetValue(k,out var v)?(int)v:fallback;
 public static void SetBool(string k,bool v)=>values[k]=v; public static void SetInt(string k,int v)=>values[k]=v;
 public static void EraseBool(string k)=>values.Remove(k);
}
static class AssemblyReloadEvents {public static Action beforeAssemblyReload;}
static class EditorApplication {public static Action quitting;}
'''+guard+'''
class Program {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
 static void Main(){
 foreach(bool enabled in new[]{false,true})foreach(int options in new[]{0,1,2,3}){
 EditorSettings.enterPlayModeOptionsEnabled=enabled;EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)options;
 FixtureEditorOptions.Capture();EditorSettings.enterPlayModeOptionsEnabled=!enabled;EditorSettings.enterPlayModeOptions=0;
 AssemblyReloadEvents.beforeAssemblyReload?.Invoke();
 Check(EditorSettings.enterPlayModeOptionsEnabled==enabled && (int)EditorSettings.enterPlayModeOptions==options,"before-reload restore "+enabled+"/"+options);
 Check(AssemblyReloadEvents.beforeAssemblyReload==null && EditorApplication.quitting==null,"handlers removed");
 FixtureEditorOptions.Capture();EditorSettings.enterPlayModeOptionsEnabled=!enabled;EditorSettings.enterPlayModeOptions=0;
 // Simulate loss of static subscriptions. SessionState is intentionally retained.
 AssemblyReloadEvents.beforeAssemblyReload=null;EditorApplication.quitting=null;
 FixtureEditorOptions.Restore();
 Check(EditorSettings.enterPlayModeOptionsEnabled==enabled && (int)EditorSettings.enterPlayModeOptions==options,"SessionState recovery after static loss "+enabled+"/"+options);
 FixtureEditorOptions.Capture();EditorSettings.enterPlayModeOptionsEnabled=!enabled;
 EditorApplication.quitting?.Invoke();
 Check(EditorSettings.enterPlayModeOptionsEnabled==enabled,"quitting restore");
 FixtureEditorOptions.Restore();Check(EditorSettings.enterPlayModeOptionsEnabled==enabled,"idempotent restore");
 }
 Console.WriteLine(checks+" offline guard checks; real Unity reload and font isolation require native run.");
 }
}
'''
out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
(out/'Program.cs').write_text(code,encoding='utf-8')
(out/'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>',encoding='utf-8')
subprocess.run(['dotnet','run','--project',str(out/'Checks.csproj'),'--verbosity','quiet'],check=True)
