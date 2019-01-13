using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;

namespace ETACO.CommonUtils.WinService
{
    public class ServicesContainer : ServiceBase
    {
        private readonly List<ServiceWorker> _workers = new List<ServiceWorker>();

        public ServicesContainer(IEnumerable<ServiceWorker> workers = null)
        {
            if (workers != null) _workers.AddRange(workers);
        }

        public int Count { get { return _workers.Count; } }

        public ServicesContainer Add(ServiceWorker sw)
        {
            _workers.Add(sw);
            return this;
        }

        public ServicesContainer AddRange(IEnumerable<ServiceWorker> workers)
        {
            _workers.AddRange(workers);
            return this;
        }

        public void Start()
        {
            _workers.ForEach(w => w.Start());
        }

        protected override void OnStart(string[] args)
        {
            Start();
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            _workers.ForEach(w => w.Stop());
            base.OnStop();
        }

        public List<KeyValuePair<string, ServiceWorkerStatus>> Info()
        {
            var result = new List<KeyValuePair<string, ServiceWorkerStatus>>();
            _workers.ForEach(w => result.Add(new KeyValuePair<string, ServiceWorkerStatus>(w.Name, w.Status)));
            return result;
        }

        public void Start(string name)
        {
            _workers.ForEach(w => { if (w.Name == name) w.Start(); });
        }

        public void Stop(string name)
        {
            _workers.ForEach(w => { if (w.Name == name && w.Status == ServiceWorkerStatus.Running) w.Stop(); });
        }

        public void Install()
        {
            var installer = new AssemblyInstaller(Assembly.GetExecutingAssembly(), null) { UseNewContext = true };
            var state = new Hashtable();
            try
            {
                installer.Install(state);
                installer.Commit(state);
            }
            catch
            {
                installer.Rollback(state);
                throw;
            }
        }

        public void Uninstall()
        {
            var installer = new AssemblyInstaller(Assembly.GetExecutingAssembly(), null) { UseNewContext = true };
            var state = new Hashtable();
            try
            {
                installer.Uninstall(state);
            }
            catch
            {
                installer.Rollback(state);
                throw;
            }
        }
    }
}