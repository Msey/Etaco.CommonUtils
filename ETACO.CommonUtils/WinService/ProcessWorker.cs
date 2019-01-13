using System;
using System.Diagnostics;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService
{
    [Plugin("processworker")]
    internal class ProcessWorker : ServiceWorker
    {
        protected override bool DoWork()
        {
            var p = Process.Start(new ProcessStartInfo(GetParameter("file", ""), GetParameter("args", "")) { UseShellExecute = false, RedirectStandardError = true });
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (!err.IsEmpty()) throw new Exception("Error in process > " + GetParameter("file", "") + ": " + err);
            return true;
        }
    }
}
