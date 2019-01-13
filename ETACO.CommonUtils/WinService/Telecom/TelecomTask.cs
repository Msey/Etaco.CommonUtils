using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml.XPath;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService.Telecom
{
    public class MessageInfo : TaskInfo
    {
        public string Name = "";
        public DateTime CreationTime;
        public Encoding Encoding { get { return Encoding.GetEncoding(Get("encoding", "utf-8")); } set { this["encoding"] = value.WebName; } }
        public override string ToString() { return base.ToString() + ";name={0};dt={1}".FormatStr(Name, CreationTime == default(DateTime) ? "" : CreationTime.ToString("yyyyMMddHHmmss")); }
    }

    public class MessageData
    {
        public string Name = "";
        public Stream Stream { get; private set; }
        public MessageData(string name, Stream stream) { Name = name; Stream = stream; }
        public void Close() { if (Stream != null) try { Stream.Close(); } catch { } }
    }

    public abstract class WriteTelecom : IDisposable
    {
        protected readonly TaskInfo TaskInfo;
        public WriteTelecom(TaskInfo taskInfo) { TaskInfo = taskInfo; }
        public abstract void WriteMessage(MessageInfo messageInfo, List<MessageData> data);
        public virtual void Close() { }
        public void Dispose() { try { Close(); } catch (Exception ex) { AppContext.Log.HandleException(ex); } }
    }

    public abstract class ReadTelecom : IDisposable
    {
        protected readonly TaskInfo TaskInfo;
        public ReadTelecom(TaskInfo taskInfo) { TaskInfo = taskInfo; }
        public virtual IEnumerable<MessageInfo> GetMessageList() { return new[] { new MessageInfo() { Name = TaskInfo["name"].FormatStr(TaskInfo), CreationTime = DateTime.Now }}; } 
        public abstract List<MessageData> GetMessageData(MessageInfo message);
        public virtual void DeleteMessage(MessageInfo message) { }
        public virtual void Close() { }
        public void Dispose() { try { Close(); } catch (Exception ex) { AppContext.Log.HandleException(ex); } }
    }


    public enum TelecomTaskStatus
    {
        Read, Write, Delete, IgnoreRead, IgnoreWrite
    }

    [Serializable]
    public class TelecomTaskInfo
    {
        public TelecomTaskStatus Status;
        public int RetryCount;

        public TelecomTaskInfo(int retryCount)
        {
            Status = TelecomTaskStatus.Read;
            RetryCount = Math.Max(1, retryCount);
        }
    }

    [Plugin("telecomtask")]
    public class TelecomTask : ActionTask
    {
        private readonly TaskInfo readTask = new TaskInfo();
        private readonly TaskInfo writeTask = new TaskInfo();

        private Dictionary<string, TelecomTaskInfo> taskQueue = new Dictionary<string, TelecomTaskInfo>();
        private string TaskQueuePath = "";
        private Log _LOG;
        
        public event Action<TaskInfo> OnStart;
        public event Action<TaskInfo> OnStop;
        public event Action<TaskInfo> OnList;
        public event Action<TaskInfo, Exception> OnListError;
        public event Action<TaskInfo, MessageInfo, List<MessageData>, TelecomTaskInfo> OnRead;
        public event Action<TaskInfo, MessageInfo, List<MessageData>, TelecomTaskInfo, Exception> OnReadError;
        public event Action<TaskInfo, MessageInfo, MessageInfo, List<MessageData>, TelecomTaskInfo> OnWrite;
        public event Action<TaskInfo, MessageInfo, MessageInfo, List<MessageData>, TelecomTaskInfo, Exception> OnWriteError;
        public event Action<TaskInfo, MessageInfo, TelecomTaskInfo> OnDelete;
        public event Action<TaskInfo, MessageInfo, TelecomTaskInfo, Exception> OnDeleteError;

        public TelecomTask(XPathNavigator nav, TaskWorker worker) : base(nav, worker)
        {
            _LOG = worker._LOG;
            OnStart += (t) => _LOG.Trace("Start: " + t);
            OnStop += (t) => _LOG.Trace("Stop: " + t);
            OnList += (t) => _LOG.Trace("List: " + t);
            OnListError += (t, e) => _LOG.HandleException(e, "List: " + t);
            OnRead += (t, m, msg, ti) => _LOG.Trace("Read: " + t + m + " msgCount=" + (msg == null ? 0 : msg.Count));
            OnReadError += (t, m, msg, ti, e) => _LOG.HandleException(e, "Read: '" + ti.Status + "' " + t + m);
            OnWrite += (t1, m1, m2, msg, ti) => _LOG.Trace("Write: " + t1 + m1 + " >> " + m2);//msg
            OnWriteError += (t1, m1, m2, msg, ti, e) => _LOG.HandleException(e, "Write: '" + ti.Status + "' " + t1 + m1 + " >> " + m2);//msg
            OnDelete += (t, m, ti) => _LOG.Trace("Delete: " + t + m);
            OnDeleteError += (t, m, ti, e) => _LOG.HandleException(e, "Delete: " + t + m);

            TaskQueuePath = Path.Combine(AppContext.AppDir, TaskInfo["Name"] + "TaskQueue.dat");
            var v = nav.GetAllChildAttributes();
            foreach (var x in v["read"]) readTask[x.Key] = x.Value;
            foreach (var x in v["write"]) writeTask[x.Key] = x.Value;
        }

        private WriteTelecom CreateWriteTelecom()
        {
            return AppContext.PluginManager.CreateInstance<WriteTelecom>(writeTask.Get("type", "filewritetelecom"), writeTask);
        }

        private ReadTelecom CreateReadTelecom()
        {
            return AppContext.PluginManager.CreateInstance<ReadTelecom>(readTask.Get("type", "filereadtelecom"), readTask);
        }
        protected virtual void SaveTaskQueue(Dictionary<string, TelecomTaskInfo> taskQueue)
        {
            using (var fs = new FileStream(TaskQueuePath, FileMode.OpenOrCreate))
            {
                new BinaryFormatter().Serialize(fs, taskQueue);
            }
        }

        protected virtual Dictionary<string, TelecomTaskInfo> LoadTaskQueue(Dictionary<string, TelecomTaskInfo> taskQueue)
        {
            if (!File.Exists(TaskQueuePath)) return taskQueue;
            using (var fs = new FileStream(TaskQueuePath, FileMode.Open))
            {
                return (Dictionary<string, TelecomTaskInfo>)new BinaryFormatter().Deserialize(fs);
            }
        }

        private void SaveTaskQueue()
        {
            try
            {
                SaveTaskQueue(taskQueue);
            }
            catch (Exception e)//SerializationException
            {
                _LOG.Error("SaveTaskQueue Failed. Reason: " + e.Message);
            }
        }

        private void LoadTaskQueue()
        {
            try
            {
                taskQueue = LoadTaskQueue(taskQueue);
            }
            catch (Exception e)//SerializationException
            {
                _LOG.Error("LoadTaskQueue Failed. Reason: " + e.Message);
            }
        }

        protected virtual string GetTaskId(MessageInfo addr)
        {
            return "" + readTask + addr + "#" + writeTask;
        }

        public override bool Processing()
        {

            bool goToSleep = true;
            LoadTaskQueue();
            OnStart(readTask);
            using (var reader = CreateReadTelecom())
            {
                try
                {
                    foreach (var addr in reader.GetMessageList())
                    {
                        OnList(readTask);
                        worker.ShowActivity();

                        var taskId = GetTaskId(addr);

                        var taskInfo = taskQueue.GetValue(taskId, new TelecomTaskInfo(readTask.Get("RetryCount", 3)), true);
                        if (taskInfo.Status == TelecomTaskStatus.IgnoreRead || taskInfo.Status == TelecomTaskStatus.IgnoreWrite) continue;
                        if (taskInfo.Status == TelecomTaskStatus.Delete)
                        {
                            try
                            {
                                reader.DeleteMessage(addr);
                                OnDelete(readTask, addr, taskInfo);
                                taskQueue.Remove(taskId);
                            }
                            catch (Exception ex) { if (worker.IsInterrupt(ex)) throw; }
                            continue;
                        }
                        goToSleep = false;

                        List<MessageData> msg = null;
                        var writeMessageInfo = new MessageInfo();
                        try
                        {
                            msg = reader.GetMessageData(addr);
                            OnRead(readTask, addr, msg, taskInfo);
                            if (msg == null || msg.Count == 0) continue; //for no_attach_mail (повторное чтение не оч затратно, так как сообщение без attach)

                            if (taskInfo.Status == TelecomTaskStatus.Read)
                            {
                                taskInfo.Status = TelecomTaskStatus.Write;
                                taskInfo.RetryCount = writeTask.Get("RetryCount", 3);
                            }

                            try
                            {
                                foreach (var m in msg) m.Name = writeTask.Get("name", "{Name}").FormatStrEx(new { Name = m.Name, Read = readTask, Address = addr, Write = writeTask });

                                foreach (var v in writeTask.Parameters) writeMessageInfo[v.Key] = v.Value.FormatStrEx(new { Read = readTask, Write = writeTask, Address = addr, Name = addr.Name});
                                writeMessageInfo.CreationTime = addr.CreationTime;
                                //
                                CreateWriteTelecom().WriteMessage(writeMessageInfo, msg);
                                OnWrite(readTask, addr, writeMessageInfo, msg, taskInfo);
                                taskInfo.Status = TelecomTaskStatus.Delete;
                                SaveTaskQueue(); //раньше сохранять статус не имеет смысла
                                                 //закрываем потоки для последующего удаления 
                                ReleaseMessageData(msg);
                                msg = null;
                                //
                                try
                                {
                                    reader.DeleteMessage(addr);
                                    OnDelete(readTask, addr, taskInfo);
                                    taskQueue.Remove(taskId);
                                }
                                catch (Exception deleteEx)
                                {
                                    if (worker.IsInterrupt(deleteEx)) throw;
                                    OnDeleteError(readTask, addr, taskInfo, deleteEx);
                                }
                            }
                            catch (Exception writeEx)
                            {
                                //if (IsInterrupt(writeEx) && Status == WinServiceManager.ServiceWorkerStatus.StopRequested && IsWorkingThread) throw;   //останавливается сервис, а не просто прерывается длительная операция

                                taskInfo.Status = (taskInfo.RetryCount--) < 1 ? TelecomTaskStatus.IgnoreWrite : TelecomTaskStatus.Write;
                                SaveTaskQueue();//safe
                                OnWriteError(readTask, addr, writeMessageInfo, msg, taskInfo, writeEx);

                                if (worker.IsInterrupt(writeEx)) throw;
                            }
                        }
                        catch (Exception readEx)
                        {
                            // if (IsInterrupt(readEx) && Status == WinServiceManager.ServiceWorkerStatus.StopRequested && IsWorkingThread) throw;    //останавливается сервис, а не просто прерывается длительная операция

                            if (taskInfo.Status == TelecomTaskStatus.Read) taskInfo.Status = (taskInfo.RetryCount--) < 1 ? TelecomTaskStatus.IgnoreRead : TelecomTaskStatus.Read;
                            SaveTaskQueue();//safe
                            OnReadError(readTask, addr, msg, taskInfo, readEx);

                            if (worker.IsInterrupt(readEx)) throw;
                        }
                        finally
                        {
                            ReleaseMessageData(msg);
                        }
                    }
                }
                catch (Exception listEx)
                {
                    if (worker.IsInterrupt(listEx)) throw;
                    OnListError(readTask, listEx);
                }
            }
            SaveTaskQueue();
            OnStop(readTask);
            return goToSleep;
        }

        private void ReleaseMessageData(List<MessageData> msg)
        {
            if (msg != null) foreach (var m in msg) { try { m.Stream.Close(); } catch { } }
        }
    }
}
