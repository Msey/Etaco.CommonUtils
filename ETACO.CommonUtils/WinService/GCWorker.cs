using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService
{
    [Plugin("gcworker")]
    internal class GCWorker : ServiceWorker
    {
        const int mb = 1024 * 1024;
        protected override bool DoWork()
        {
            long mem = AppContext.TotalAllocatedMemory / mb;
            if (mem > GetParameter("membound", 64))
            {
                _LOG.Info("Total allocated memory {0} MB. Call garbage collector.".FormatStr(mem));
                AppContext.GarbageCollect();
                _LOG.Info("Total allocated memory {0} MB.".FormatStr(AppContext.TotalAllocatedMemory / mb));
            }
            return true;
        }
    }
}