using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LetMeSleep.Content.Editor.Higgsfield
{
    public static class HiggsfieldExternalFixture
    {
        public static void Run()
        {
            try
            {
                string dll=Argument("-higgsfieldFixtureAssembly"),config=Argument("-higgsfieldFixtureConfig"),output=Argument("-higgsfieldFixtureOutput");
                string full=Path.GetFullPath(dll).Replace('\\','/');
                if(!full.StartsWith("N:/LetMeSleep/Validation/Higgsfield/",StringComparison.OrdinalIgnoreCase))throw new Exception("Use the coordinator's compiled Higgsfield fixture");
                var assembly=Assembly.LoadFrom(full);
                var result=assembly.GetType("HiggsfieldMapChecks",true).GetMethod("Run").Invoke(null,new object[]{config,output});
                Debug.Log("HIGGSFIELD_FIXTURE_REPORT "+result);
            }
            catch(Exception e){Debug.LogException(e.InnerException??e);EditorApplication.Exit(1);}
        }
        static string Argument(string key){var args=System.Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);if(i<0 || i+1>=args.Length)throw new Exception("Missing "+key);return args[i+1];}
    }
}
