using System;

namespace ETACO.CommonUtils.Plugin
{
    [Serializable]
    public class LogMessageCrossDomainRepeater
    {
        private readonly Log log = null;
        public LogMessageCrossDomainRepeater()
        {
            log = AppContext.Log;
        }

        public void Repeat()
        {
            AppContext.Log.OnLog += log.Write;
        }
    }
}