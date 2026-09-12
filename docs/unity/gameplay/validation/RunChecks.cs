using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class RunChecks
{
    public static int Main()
    {
        int tests = 0, failures = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && !t.IsAbstract))
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var cases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
            if (cases.Length == 0 && method.GetCustomAttribute<TestAttribute>() == null) continue;
            var arguments = cases.Length == 0 ? new[] { Array.Empty<object>() } : cases.Select(c => c.Arguments).ToArray();
            foreach (var args in arguments)
            {
                tests++;
                try
                {
                    var converted = args.Select((a, i) => method.GetParameters()[i].ParameterType.IsEnum ? a : Convert.ChangeType(a, method.GetParameters()[i].ParameterType)).ToArray();
                    method.Invoke(Activator.CreateInstance(type), converted);
                }
                catch (Exception error) { failures++; Console.WriteLine("FAIL " + type.Name + "." + method.Name + " " + (error.InnerException ?? error)); }
            }
        }
        Console.WriteLine("GAMEPLAY_CPU_CHECKS tests=" + tests + " failures=" + failures + " unity_runner=false"); return failures == 0 ? 0 : 1;
    }
}
