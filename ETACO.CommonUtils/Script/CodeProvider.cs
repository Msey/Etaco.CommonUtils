using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;

namespace ETACO.CommonUtils.Script
{
    /// <summary> Генерация кода и сборок </summary>
    public class CodeProvider<T> where T : CodeDomProvider
    {
        private readonly T codeProvider = null;
        private readonly string usingFormat = null;//"using {0};\n";
        private readonly CompilerParameters compilerParameters = new CompilerParameters();
        private readonly HashSet<string> usingList = new HashSet<string> { "System", "System.Xml", "System.IO", "System.Data", "System.Reflection", "System.Threading", "System.Threading.Tasks", "System.Drawing", "System.Windows.Forms" };

        public CodeProvider(T codeProvider, string usingFormat, int WarningLevel = -1)
        {
            if (codeProvider == null) throw new Exception("CodeProviderException: codeProvider is null.");
            if (usingFormat.IsEmpty()) throw new Exception("CodeProviderException: usingFormat is empty.");
            this.codeProvider = codeProvider;
            this.usingFormat = usingFormat;
            AddReference("System.dll", "System.Data.dll", "System.Xml.dll", "System.Windows.Forms.dll", "System.Drawing.dll", Assembly.GetExecutingAssembly().Location);
            AddUsing(AppContext.GetNamespaces());//AppContext.GetAllReferencedAssemblies() - дополнительно грузит сборки в домен - не подходит
            if (AppContext.GetEntryAssembly() != Assembly.GetExecutingAssembly()) AddReference(AppContext.GetEntryAssembly().Location).AddUsing(AppContext.GetNamespaces(AppContext.GetEntryAssembly()));

            compilerParameters.GenerateExecutable = false;
            compilerParameters.GenerateInMemory = true;
            compilerParameters.IncludeDebugInformation = false;
            compilerParameters.WarningLevel = WarningLevel;
        }

        /// <summary> Добавление ссылки на сборки </summary>
        public CodeProvider<T> AddReference(params string[] assemblies)
        {
            if (assemblies != null) Array.ForEach(assemblies, a => { if (!a.IsEmpty() && !compilerParameters.ReferencedAssemblies.Contains(a)) compilerParameters.ReferencedAssemblies.Add(a); });
            return this;
        }

        /// <summary> Добавление директив using/import и т.д.</summary>
        public CodeProvider<T> AddUsing(params string[] usings)
        {
            if (usings != null) Array.ForEach(usings, u => { if (!u.IsEmpty()) usingList.Add(u); });
            return this;
        }

        public CodeProvider<T> AddTypeReference(params Type[] ts)
        {
            if(ts!= null) Array.ForEach(ts, t => { AddReference(t.Assembly.Location); AddUsing(AppContext.GetNamespaces(t.Assembly)); });
            return this;
        }

        /// <summary> Компиляция исходных кодов в сборку </summary>
        public Assembly Compile(params string[] sources)
        {
            if (sources == null) throw new Exception("CodeProviderException: source for compile is empty.");

            var sUsing = "";
            foreach(var u in usingList) sUsing += usingFormat.FormatStr(u);

            for (int i = 0; i < sources.Length; i++) sources[i] = sUsing + " " + sources[i];

            var cr = codeProvider.CompileAssemblyFromSource(compilerParameters, sources);
            compilerParameters.TempFiles.Delete();
            if (cr.Errors.Count > 0)
            {
                var err = "CodeProviderException: " + Environment.NewLine;
                foreach (CompilerError ce in cr.Errors) err +=  ce.ErrorText + " => line({0}): ".FormatStr(ce.Line) + sources[0].Split('\n')[ce.Line - 1].Insert(ce.Column - 1, "<<<!>>")+"\n";//ce + Environment.NewLine;
                throw new Exception(err);
            }
            return cr.CompiledAssembly;
        }
    }
}