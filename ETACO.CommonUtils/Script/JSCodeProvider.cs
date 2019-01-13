using Microsoft.JScript;

namespace ETACO.CommonUtils.Script
{
    public class JSCodeProvider : CodeProvider<JScriptCodeProvider>
    {
        public JSCodeProvider(int WarningLevel = -1)
            : base(new JScriptCodeProvider(), "import {0};", WarningLevel)
        {
            AddReference("Microsoft.JScript.dll");
            AddUsing("Microsoft.JScript","System.Threading");
            AddReference(AppContext.Config["jscodeprovider", "reference"].Split(";"));
            AddUsing(AppContext.Config["jscodeprovider", "using"].Split(";"));
        }
    }
}