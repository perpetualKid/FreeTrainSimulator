using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp;

namespace Orts.Scripting.Api
{
    public class ScriptManager
    {
        private readonly Dictionary<string, Assembly> scripts = new Dictionary<string, Assembly>();
        private static readonly CSharpCodeProvider compiler = new CSharpCodeProvider();

        private static string runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

        private static CompilerParameters GetCompilerParameters()
        {
            var cp = new CompilerParameters()
            {
                GenerateInMemory = true,
                IncludeDebugInformation = Debugger.IsAttached,

            };
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("System.Core.dll");
            cp.ReferencedAssemblies.Add("Orts.Common.dll");
            cp.ReferencedAssemblies.Add("Orts.Scripting.Api.dll");
            return cp;
        }

        private static readonly MetadataReference[] CompilerParameters = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(Trace).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<object>).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile("Orts.Common.dll"),
            MetadataReference.CreateFromFile("Orts.Settings.dll"),
            MetadataReference.CreateFromFile("Orts.Scripting.Api.dll"),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "mscorlib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.InteropServices.RuntimeInformation.dll")),
#if NETCOREAPP
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "netstandard.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll"))
#endif
        };

        private static readonly IEnumerable<string> DefaultNamespaces = new[]
        {
                "System",
                "System.IO",
                //"System.Linq",
                "System.Text",
                //"System.Text.RegularExpressions",
                "System.Collections.Generic"
   };

        private static readonly CSharpCompilationOptions DefaultCompilationOptions =
            new CSharpCompilationOptions(OutputKind.ConsoleApplication).
            WithOverflowChecks(true).WithOptimizationLevel(Debugger.IsAttached ? OptimizationLevel.Debug : OptimizationLevel.Release).
            WithUsings(DefaultNamespaces);

        public object LoadRoslyn(string path, string name)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
                return null;

            if (Path.GetExtension(name) != ".cs")
                name += ".cs";

            path = Path.Combine(path, name);

            string type = $"Orts.Scripting.Script.{Path.GetFileNameWithoutExtension(path)}";

            if (scripts.ContainsKey(path))
                return scripts[path].CreateInstance(type, true);

            SourceText sourceText = SourceText.From(new FileStream(path, FileMode.Open));
            SyntaxTree parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, CSharpParseOptions.Default);

            CSharpCompilation compilation = CSharpCompilation.Create("Test", new SyntaxTree[] { parsedSyntaxTree }, CompilerParameters, DefaultCompilationOptions);

            EmitResult emitResult = null;
            using (MemoryStream ms = new MemoryStream())
            {
                emitResult = compilation.Emit(ms);
                ms.Seek(0, SeekOrigin.Begin);

                if (!emitResult.Success)
                {
                    Trace.TraceWarning(string.Join(Environment.NewLine, emitResult.Diagnostics.Select((x, i) => $"{i + 1}. {x}")));
                    return null;
                }
                Assembly scriptAssembly = Assembly.Load(ms.ToArray());
                scripts.Add(path, scriptAssembly);
                return scriptAssembly.CreateInstance(type);
            }
        }

        public object Load(string path, string name)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
                return null;

            if (Path.GetExtension(name) != ".cs")
                name += ".cs";

            path = Path.Combine(path, name);

            string type = $"Orts.Scripting.Script.{Path.GetFileNameWithoutExtension(path)}";

            if (scripts.ContainsKey(path))
                return scripts[path].CreateInstance(type, true);

            try
            {
                var compilerResults = compiler.CompileAssemblyFromFile(GetCompilerParameters(), path);
                if (!compilerResults.Errors.HasErrors)
                {
                    Assembly script = compilerResults.CompiledAssembly;
                    scripts.Add(path, script);
                    return script.CreateInstance(type, true);
                }
                else
                {
                    StringBuilder errorString = new StringBuilder();
                    errorString.AppendFormat("Skipped script {0} with error:", path);
                    errorString.Append(Environment.NewLine);
                    foreach (CompilerError error in compilerResults.Errors)
                    {
                        errorString.AppendFormat($"   {error.ErrorText}, line: {error.Line}, column: {error.Column}");
                        errorString.Append(Environment.NewLine);
                    }
                    Trace.TraceWarning(errorString.ToString());
                    return null;
                }
            }
            catch (InvalidDataException error)
            {
                Trace.TraceWarning("Skipped script {0} with error: {1}", path, error.Message);
                return null;
            }
            catch (Exception error)
            {
                if (File.Exists(path))
                    Trace.WriteLine(new FileLoadException(path, error));
                else
                    Trace.TraceWarning("Ignored missing script file {0}", path);
                return null;
            }
        }

        public string GetStatus()
        {
            return ($"{scripts.Keys.Count:F0} scripts");
        }
    }
}
