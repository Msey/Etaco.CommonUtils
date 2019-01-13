using System.Collections.Generic;
using Microsoft.CSharp;

namespace ETACO.CommonUtils.Script
{
    public class CSCodeProvider : CodeProvider<CSharpCodeProvider>
    {
        public CSCodeProvider(string compilerVersion = "v2.0", int WarningLevel = -1)
            : base(new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", compilerVersion } }), "using {0};", WarningLevel) 
        {
            AddReference(AppContext.Config["cscodeprovider", "reference"].Split(";"));
            AddUsing(AppContext.Config["cscodeprovider", "using"].Split(";"));
        }
    }
}