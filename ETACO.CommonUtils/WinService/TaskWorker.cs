using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService
{
    public class TaskInfo
    {
        private readonly StringCrypter stringCrypter = new StringCrypter();
        public readonly Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public string this[string name] { get { return Parameters.GetValue(name, "").FormatStrEx(); } set { Parameters[name] = value; } }
        public T Get<T>(string name, T defaultValue = default(T)) { var v = Parameters.GetValue(name, "").FormatStrEx(); return v.IsEmpty() ? defaultValue : (T)Convert.ChangeType(v, typeof(T)); }
        public TaskInfo Set(string name, string value) { Parameters[name] = value; return this; }
        public string GetPassword()
        {
            var v = Parameters.GetValue("password", "");//чтобы получить пароль без изменения от FormatStrEx()
            if (!v.IsEmpty()) return v;
            v = Parameters.GetValue("encryptpassword", "");
            return v.IsEmpty() ? "" : stringCrypter.DecryptFromBase64(v);
        }
        public override string ToString() { return string.Join(";", Parameters.Where(v => !v.Key.ToLower().Contains("password")).OrderBy(v => v.Key).Select(v => v.Key + "=" + v.Value).ToArray()); }

    }

    public abstract class ActionTask
    {
        protected readonly TaskInfo TaskInfo = new TaskInfo();
        protected readonly TaskWorker worker;
        public ActionTask(XPathNavigator nav, TaskWorker worker) { this.worker = worker;  foreach (var a in nav.GetAllAttributes()) TaskInfo[a.Key] = a.Value; }
        public abstract bool Processing();//true - ждём WaitTimeout
    }

    [Plugin("evaltask")]
    public class EvalTask : ActionTask
    {
        private Script.JSEval jsEval = new Script.JSEval();
        private string Code = "";
        public EvalTask(XPathNavigator nav, TaskWorker worker):base(nav, worker) { Code = nav.InnerXml;}
        public override bool Processing() { jsEval.Set(TaskInfo.Parameters); jsEval.Eval(Code); return true; }
    }


    public class TaskWorker : ServiceWorker<ActionTask>
    {
        private Dictionary<string, ActionTask> Tasks = new Dictionary<string, ActionTask>();
        public TaskWorker(string name):base(name) {}
        protected override IEnumerable<ActionTask> GetTaskList()
        {
            var tasks = AppContext.Config.GetSection("tasks").SelectChildren("task", "");
            while (tasks.MoveNext())
            {
                if (tasks.Current.GetAttribute("worker", "") != Name) continue;
                var tName = tasks.Current.GetAttribute("name", "");
                var t = Tasks.GetValue(tName);
                t = t == null ? Tasks[tName] = AppContext.PluginManager.CreateInstance<ActionTask>(tasks.Current.GetAttribute("type", "").IfEmpty("evaltask"), tasks.Current, this) : t;
                yield return t;
            }
        }
        protected override bool Processing(ActionTask task) { return task.Processing(); }
    }
}