using System;
using System.Collections.Generic;
using System.Threading;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService
{
    public enum ServiceWorkerStatus { Running, StopRequested, PingStopRequested, Stopped };

    public abstract class ServiceWorker<T> : ServiceWorker
    {
        public ServiceWorker(string name = "") : base(name) { }
        /// <summary> Получение списка заданий </summary>
        protected abstract IEnumerable<T> GetTaskList();

        /// <summary> Выполнение задания </summary>
        /// <returns> true - нужно ждать WaitTimeout перед следующим вызовом, иначе - false</returns>
        protected abstract bool Processing(T taskInfo);

        protected override bool DoWork()
        {
            var goToSleep = true;
            var v = GetTaskList();
            if (v == null) return goToSleep;
            Exception lastEx = null;

            foreach (T task in v)
            {
                ShowActivity();
                try { goToSleep &= Processing(task); }
                catch (Exception ex)//чтобы первая ошибка не останавливала выполнения последующих задач
                {
                    if (IsInterrupt(ex)) throw;
                    lastEx = ex;
                    _LOG.HandleException(ex);
                }
            }
            if (lastEx != null) throw lastEx; //для поддержки логики в случае возникновения ошибки в DoWork
            return goToSleep || GetParameter("forceGoToSleep", false);
        }
    }

    public abstract class ServiceWorker
    {
        private Thread _thread = null;          //рабочий поток
        private bool _activity = true;          //флаг наличия активности с момента последнего опроса этого флага
        private bool _isWaitingMode = false;    //флаг перехода в режим ожидания по WaitTimeout

        public readonly Log _LOG = AppContext.Log;
        public readonly Config _CFG = AppContext.Config;

        /// <summary> Текущий статус сервиса </summary>
        public ServiceWorkerStatus Status { get; private set; }
        /// <summary> Время очредного запуска</summary>
        public DateTime StartWorkTime { get; private set; }
        /// <summary> Время предыдущей запуска</summary>
        public DateTime PrevStartWorkTime { get; private set; }

        /// <summary> Имя сервиса </summary>
        public string Name { get; private set; }

        /// <summary> Проверка наличия активности в потоке (с момента предыдущей проверки)</summary>        
        private bool HasActivity() { var v = _activity; _activity = false; return v; }

        /// <summary> Отмечает активность </summary>
        /// <param name="canInterrupt"> Можно ли в данном месте прерывать поток </param>
        public void ShowActivity(bool canInterrupt = true) { _activity = true; if (canInterrupt) Thread.Sleep(0); }

        /// <summary> Метод выполняющий основную работу </summary>
        /// <returns> true - нужно ждать WaitTimeout перед следующим вызовом, иначе - false</returns>
        protected abstract bool DoWork();

        /// <summary> Метод дополнительно инициирующий процесс остановки </summary>
        protected virtual void DoStop() { Status = Status == ServiceWorkerStatus.Running ? ServiceWorkerStatus.StopRequested : Status; }

        /// <summary> Освобождение ресурсов (должен поддерживать множественный вызов)</summary>
        protected virtual void Deinit() { }

        /// <summary> Возвращает таймаут для проверяющего потока </summary>
        protected virtual int PingTimeout { get { return GetParameter("pingtimeout", -1); } }

        /// <summary> Возвращает таймаут между вызовами DoWork </summary>
        protected virtual int WaitTimeout
        {
            get
            {
                var v = GetParameter("starttime", "");
                if (v.IsEmpty()) return GetParameter("waittimeout", 0);
                DateTime d;
                if (DateTime.TryParseExact(v, "HH:mm", null, System.Globalization.DateTimeStyles.None, out d)) return Convert.ToInt32(((d < DateTime.Now ? d.AddDays(1) : d) - DateTime.Now).TotalMilliseconds);
                _LOG.Error("starttime incorrect format ('HH:mm'): " + v);
                return GetParameter("waittimeout", 0);
            }
        }

        /// <summary> Является ли данное исключение прерывающим поток </summary>
        public bool IsInterrupt(Exception e) { return (e is ThreadAbortException || e is ThreadInterruptedException); }

        /// <summary> Вызывающий поток является рабочим для данного ServiceWorker</summary>
        protected bool IsWorkingThread { get { return _thread != null && _thread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId; } }

        /// <summary> Возвращает параметр для данного сервиса из файла конфигурации </summary>
        protected T GetParameter<T>(string paramName, T defaultValue) where T : IConvertible { return _CFG.GetParameter(Name, paramName, defaultValue); }

        public ServiceWorker(string name = "")
        {
            Status = ServiceWorkerStatus.Stopped;

            if (name.IsEmpty())
            {
                var pa = (PluginAttribute)Attribute.GetCustomAttribute(GetType(), typeof(PluginAttribute), true);
                name = (pa == null ? GetType().Name : pa.Name);
            }
            Name = name;

            var timeout = PingTimeout;
            if (timeout > 0)
            {
                var pingThread = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(timeout);
                        if ((_thread != null) && _thread.IsAlive && !_isWaitingMode && !HasActivity())
                        {
                            try
                            {
                                Stop(true);
                                Start();
                            }
                            catch (Exception ex)//на всякий случай
                            {
                                _LOG.Error("Thread restart exception: thread='{0}' exception='{1}'".FormatStr(Name, ex.Message));
                            }
                        }
                    }
                });
                pingThread.IsBackground = true;
                pingThread.Start();
            }
        }

        internal void Stop(bool pingStop = false)
        {
            Status = pingStop ? ServiceWorkerStatus.PingStopRequested : ServiceWorkerStatus.StopRequested;
            try { DoStop(); } catch (Exception ex) { _LOG.HandleException(ex, Name + " DoStop error:" + ex.Message); }
            if ((_thread != null) && _thread.IsAlive)
            {
                LogStatus("stopping");
                try
                {
                    if (_thread.ThreadState == ThreadState.WaitSleepJoin || !_thread.Join(3000)) //если поток подвис то неважно сколько ждать, если останавливаем сервис то 3000 должно хватить => HKEY_LOCAL_MACHINE\System \CurrentControlSet\Control\WaitToKillServiceTimeout
                    {
                        _thread.Interrupt();
                        if (!_thread.Join(1000))
                        {
                            _thread.Abort();
                            if (!_thread.Join(1000))
                            {
                                _thread.IsBackground = true;
                                _LOG.Info(Name + ":\taborting.ID={2} ThreadState={0} ThreadCount={1}".FormatStr(_thread.ThreadState, System.Diagnostics.Process.GetCurrentProcess().Threads.Count, _thread.ManagedThreadId));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _LOG.Error("Thread stop exception: thread='{0}' exception='{1}'".FormatStr(Name, ex.Message));
                    _LOG.HandleException(ex);
                    try
                    {
                        _thread.IsBackground = true;
                    }
                    catch (ThreadStateException)
                    {
                        _LOG.Info("The thread '{0}' is dead.".FormatStr(Name));
                    }
                    catch (Exception fEx)//на всякий случай
                    {
                        _LOG.HandleException(fEx);
                    }
                }
            }
            _thread = null;
            try { Deinit(); } catch (Exception ex) { _LOG.HandleException(ex, Name + " deinit error:" + ex.Message); }
            LogStatus("stoped");
            Status = ServiceWorkerStatus.Stopped;
        }

        internal void Start()
        {
            if (Status != ServiceWorkerStatus.Stopped) return; ////если статус StopRequested или PingStopRequested, то запуск невозможен
            _thread = new Thread(() =>
            {
                while (Status == ServiceWorkerStatus.Running)
                {
                    try
                    {
                        ShowActivity();
                        StartWorkTime = DateTime.Now;
                        if (DoWork() && Status == ServiceWorkerStatus.Running)
                        {
                            PrevStartWorkTime = StartWorkTime;
                            try
                            {
                                _isWaitingMode = true;
                                Thread.Sleep(Math.Max(0, WaitTimeout));
                            }
                            finally
                            {
                                _isWaitingMode = false;
                            }
                        }
                        if(PrevStartWorkTime != StartWorkTime) PrevStartWorkTime = StartWorkTime;
                    }
                    catch (ThreadInterruptedException) { LogStatus("interrupted"); break; }      //генерируется только при ThreadState.WaitSleepJoin
                    catch (ThreadAbortException) { LogStatus("aborted ID=" + Thread.CurrentThread.ManagedThreadId); Thread.ResetAbort(); break; }//Thread.ResetAbort() иначе генерируется повторное исключение
                    catch (Exception ex)
                    {
                        _LOG.HandleException(ex);
                        ShowActivity();
                        Thread.Sleep(Math.Max(0, WaitTimeout));
                    }
                }
                LogStatus("finished");
                Status = ServiceWorkerStatus.Stopped;
            });
            try
            {
                Status = ServiceWorkerStatus.Running;
                _thread.Start();
                LogStatus("started");
            }
            catch (Exception ex)
            {
                _LOG.Error("Thread start exception: thread='{0}' exception='{1}'".FormatStr(Name, ex.Message));
                _LOG.HandleException(ex);
                Status = ServiceWorkerStatus.Stopped;
            }
        }

        private void LogStatus(string status)
        {
            _LOG.Info("{0,-36}{1}".FormatStr(Name, status));
        }

        protected bool CheckTimeMask(string mask)//M:D:H:m
        {
            if (mask.IsEmpty()) return true;
            if (PrevStartWorkTime == default(DateTime)) return false;
            var result = false;
            foreach (var m in mask.Split('|'))
            {
                if (m.IsEmpty()) continue;
                var v = m.Split(':');
                var month = v.Length >= 4 ? int.Parse(v[v.Length - 4]) : 0;
                var day = v.Length >= 3 ? int.Parse(v[v.Length - 3]) : 0;
                var hour = v.Length >= 2 ? int.Parse(v[v.Length - 2]) : -1;
                var minute = int.Parse(v[v.Length - 1]);

                var xDate = new DateTime(PrevStartWorkTime.Year, month > 0 ? month : PrevStartWorkTime.Month, day > 0 ? day : PrevStartWorkTime.Day, hour > -1 ? hour : PrevStartWorkTime.Hour, minute, 0);
                if (xDate < PrevStartWorkTime)
                {
                    if (month > 0) xDate = xDate.AddYears(1);
                    else if (day > 0) xDate = xDate.AddMonths(1);
                    else if (hour > -1) xDate = xDate.AddDays(1);
                    else if (minute > 0) xDate = xDate.AddHours(1);
                }
                result |= PrevStartWorkTime <= xDate && xDate <= StartWorkTime;
            }
            return result;
        }
    }
}