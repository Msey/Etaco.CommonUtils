using System;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    public class WordManager
    {
        private static Type type = Type.GetTypeFromProgID(AppContext.Config["word", "application", "Word.Application"]);//"Word.Application.9"
        //private readonly object word;

        public static void OpenWord(object content)
        {
            if (type == null) throw new Exception("COM object Word.Application not found");
            var wordApp = Activator.CreateInstance(type);
            wordApp._SetProperty("Visible", true);
            var newDoc = wordApp._GetProperty("Documents")._InvokeMethod("Add");
            Clipboard.SetData(DataFormats.Rtf, content);
            newDoc._GetProperty("Content")._InvokeMethod("Paste");
        }
    }
}
