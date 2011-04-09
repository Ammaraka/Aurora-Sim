using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Text;
using Microsoft.CSharp;
//using Microsoft.JScript;
using Microsoft.VisualBasic;
using log4net;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;

namespace Aurora.ScriptEngine.AuroraDotNetEngine.CompilerTools
{
    public class CSConverter : IScriptConverter
    {
        private CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();

        public string DefaultState { get { return ""; } }

        public void Initialise(Compiler compiler)
        {
        }

        public void Convert(string Script, out string CompiledScript, out Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> PositionMap)
        {
            CompiledScript = CreateCompilerScript(Script);
            PositionMap = null;
        }

        public string Name
        {
            get { return "cs"; }
        }
        public void Dispose()
        {
        }
        private string CreateCompilerScript(string compileScript)
        {
            compileScript = compileScript.Replace("string", 
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLString");

            compileScript = compileScript.Replace("integer", 
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLInteger");

            compileScript = compileScript.Replace("float", 
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLFloat");

            compileScript = compileScript.Replace("list",
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.list");

            compileScript = compileScript.Replace("rotation",
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.Quaternion");

            compileScript = compileScript.Replace("vector",
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.Vector3");
            string compiledScript = "";
            compiledScript = String.Empty +
                "using Aurora.ScriptEngine.AuroraDotNetEngine.Runtime;\n" +
                "using Aurora.ScriptEngine.AuroraDotNetEngine;\n" +
                "using System;\n" +
                "using System.Collections.Generic;\n" +
                "using System.Collections;\n" +
                "namespace Script\n" +
                "{\n";

            compiledScript += "public class ScriptClass : Aurora.ScriptEngine.AuroraDotNetEngine.Runtime.ScriptBaseClass, IDisposable\n";
            compiledScript += "{\n";
            compiledScript +=
                     compileScript;

            compiledScript += "\n}"; // Close Class

            compiledScript += "\n}"; // Close Namespace

            return compiledScript;
        }

        public CompilerResults Compile(CompilerParameters parameters, bool isFile, string Script)
        {
            bool complete = false;
            bool retried = false;
            CompilerResults results;
            do
            {
                lock (CScodeProvider)
                {
                    if (isFile)
                        results = CScodeProvider.CompileAssemblyFromFile (
                            parameters, Script);
                    else
                        results = CScodeProvider.CompileAssemblyFromSource (
                            parameters, Script);
                }
                // Deal with an occasional segv in the compiler.
                // Rarely, if ever, occurs twice in succession.
                // Line # == 0 and no file name are indications that
                // this is a native stack trace rather than a normal
                // error log.
                if (results.Errors.Count > 0)
                {
                    if (!retried && (results.Errors[0].FileName == null || results.Errors[0].FileName == String.Empty) &&
                        results.Errors[0].Line == 0)
                    {
                        // System.Console.WriteLine("retrying failed compilation");
                        retried = true;
                    }
                    else
                    {
                        complete = true;
                    }
                }
                else
                {
                    complete = true;
                }
            } while (!complete);
            return results;
        }

        public void FinishCompile (IScriptModulePlugin plugin, ScriptData data, IScript Script)
        {
        }
    }

    public class AScriptConverter : IScriptConverter
    {
        private CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();

        public string DefaultState { get { return ""; } }
        public List<string> m_includedDefines = new List<string>();
        public bool m_addLSLAPI = false;
        public bool m_allowUnsafe = false;
        private Compiler m_compiler;

        public void Initialise(Compiler compiler)
        {
            m_compiler = compiler;
        }

        public void Convert(string Script, out string CompiledScript, out Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> PositionMap)
        {
            CompiledScript = CreateCompilerScript(Script);
            PositionMap = null;
        }

        public string Name
        {
            get { return "ascript"; }
        }

        public void Dispose()
        {
        }

        private string CreateCompilerScript(string compileScript)
        {
            bool newLine = true;
            bool reading = true;
            string lastLine = "";
            for (int i = 0; i < compileScript.Length; i++)
            {
                if (compileScript[i] == '\n')
                {
                    if (lastLine != "")
                        ReadLine(lastLine);
                    reading = false;
                    lastLine = "";
                    newLine = true;
                }
                if (!newLine)
                {
                    reading = false;
                    continue;
                }
                if (compileScript.Length <= i + 2)
                    continue;
                if (!reading && !(compileScript[i + 1] == '#' || (compileScript[i + 1] == '/' && compileScript[i + 2] == '/')))
                {
                    newLine = false;
                    continue;
                }
                reading = true;
                if (compileScript[i] != '\n')
                    lastLine += compileScript[i];
                compileScript = compileScript.Remove(i, 1);
                i--;//Removed a letter, remove a char here as well
            }

            if (m_addLSLAPI)
            {
                foreach (IScriptApi api in m_compiler.ScriptEngine.GetAPIs())
                {
                    foreach (string functionName in m_compiler.ScriptEngine.GetFunctionNames(api))
                    {
                        compileScript = compileScript.Replace(functionName, "((" + api.InterfaceName + ")m_apis[\"" + api.Name + "\"])." + functionName);
                    }
                }
            }

            string compiledScript = "";
            compiledScript = String.Empty +
                "using Aurora.ScriptEngine.AuroraDotNetEngine.Runtime;\n" +
                "using Aurora.ScriptEngine.AuroraDotNetEngine;\n" +
                "using Aurora.ScriptEngine.AuroraDotNetEngine.APIs.Interfaces;\n" +
                "using OpenSim.Framework;\n" +
                "using OpenSim.Services.Interfaces;\n" +
                "using OpenSim.Region.Framework.Interfaces;\n" +
                "using OpenSim.Region.Framework.Scenes;\n" +
                "using OpenMetaverse;\n" +
                "using System;\n" +
                "using System.Collections.Generic;\n" +
                "using System.Collections;\n" +
                "namespace Script\n" +
                "{\n";

            compiledScript += "public class ScriptClass : Aurora.ScriptEngine.AuroraDotNetEngine.Runtime.ScriptBaseClass, IDisposable\n";
            compiledScript += "{\n";
            compiledScript +=
                     compileScript;

            compiledScript += "\n}"; // Close Class

            compiledScript += "\n}"; // Close Namespace

            return compiledScript;
        }

        private void ReadLine(string line)
        {
            if (line.StartsWith("#include"))
            {
                line = line.Replace("#include", "");
                m_includedDefines.Add(line); //TODO: Add a check here
            }
            else if (line.StartsWith("#threaded"))
            {
            }
            else if (line.StartsWith("#useLSLAPI"))
            {
                m_addLSLAPI = true;
            }
            else if (line.StartsWith("#allowUnsafe"))
            {
                m_allowUnsafe = true;
            }
        }

        public CompilerResults Compile(CompilerParameters parameters, bool isFile, string Script)
        {
            string rootPath =
                            Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenSim.Region.Framework.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenMetaverse.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenMetaverseTypes.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenMetaverse.StructuredData.dll"));
            bool complete = false;
            bool retried = false;
            CompilerResults results;
            do
            {
                lock (CScodeProvider)
                {
                    if (isFile)
                        results = CScodeProvider.CompileAssemblyFromFile (
                            parameters, Script);
                    else
                        results = CScodeProvider.CompileAssemblyFromSource(
                            parameters, Script);
                }
                // Deal with an occasional segv in the compiler.
                // Rarely, if ever, occurs twice in succession.
                // Line # == 0 and no file name are indications that
                // this is a native stack trace rather than a normal
                // error log.
                if (results.Errors.Count > 0)
                {
                    if (!retried && (results.Errors[0].FileName == null || results.Errors[0].FileName == String.Empty) &&
                        results.Errors[0].Line == 0)
                    {
                        // System.Console.WriteLine("retrying failed compilation");
                        retried = true;
                    }
                    else
                    {
                        complete = true;
                    }
                }
                else
                {
                    complete = true;
                }
            } while (!complete);
            return results;
        }

        public void FinishCompile(IScriptModulePlugin plugin, ScriptData data, IScript Script)
        {
            Script.SetSceneRefs(data.World, data.Part);
        }
    }
}
