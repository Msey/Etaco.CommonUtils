using System;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace ETACO.CommonUtils
{//Async.DoAsync(() => { return System.Threading.Thread.CurrentThread.IsThreadPoolThread; }).GetResult() => true;  
    public class AsyncState {public bool Canceled { get; set; }}
    public class Async : IDisposable
    {
        protected AsyncState state;
        protected Thread thread;
        //public Type ResultType { get { return ((Delegate)AsyncResult.AsyncDelegate).Method.ReturnType; } }
        /// <summary>Внутренний дескриптор задачи(use AsyncResult.EndInvokeCalled/AsyncResult.IsCompleted и т.д.)</summary>
        public AsyncResult AsyncResult { get; protected set; }
        public bool IsCompleted { get { return AsyncResult.IsCompleted; }}
        /// <summary>Ожидаем завершение выполнения асинхронного делегата</summary>
        public bool Cancel(int timeout = -1) { if (state == null) return false; if(AsyncResult.IsCompleted) return true; state.Canceled = true; return Wait(timeout); }
        public bool Interrupt(int timeout = -1) { if (AsyncResult.IsCompleted) return true; thread.Interrupt(); return Wait(timeout); }
        public bool Abort(int timeout = -1) { if (AsyncResult.IsCompleted) return true; thread.Abort(); return Wait(timeout); }
        public void Dispose() { AsyncResult.AsyncWaitHandle.Close(); }
        public bool Wait(int timeout = -1, params Async[] others)
        {
            if (others == null || others.Length == 0) return AsyncResult.AsyncWaitHandle.WaitOne(timeout);
            else if (timeout == -1) { foreach (var o in others) o.Wait(); return AsyncResult.AsyncWaitHandle.WaitOne(); }
            else
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                if (!AsyncResult.AsyncWaitHandle.WaitOne(timeout)) return false;
                sw.Stop();
                foreach (var o in others)
                {
                    if ((timeout -= (int)sw.ElapsedMilliseconds) < 0) return false;
                    sw.Start();
                    if (!o.Wait(timeout)) return false;
                    sw.Stop();
                }
                return true;//т.к. все Wait вернули true
            }
        }
        public static Async<bool> DoAsync(Action action, Action<Async<bool>> onResult = null) { return DoAsync(() => { action(); return true; }, onResult); }
        public static Async<bool> DoAsync(Action<AsyncState> action, Action<Async<bool>> onResult = null) { return DoAsync(v => { action(v); return true; }, onResult); }
        public static Async<T> DoAsync<T>(Func<T> func, Action<Async<T>> onResult = null) { return new Async<T>(func, onResult); }
        public static Async<T> DoAsync<T>(Func<AsyncState, T> func, Action<Async<T>> onResult = null) { return new Async<T>(func, onResult); }
        public static bool SetMaxThreads(int tCount, int iotCount = 0) { return ThreadPool.SetMaxThreads(tCount, iotCount == 0?GetMaxThreads(true):iotCount); }
        public static int GetMaxThreads(bool io = false) { int i, j; ThreadPool.GetMaxThreads(out i, out j); return io?j:i; }
        public static int GetAvailableThreads(bool io = false) { int i, j; ThreadPool.GetAvailableThreads(out i, out j); return io ? j : i; }
        /*public static async System.Threading.Tasks.Task<string> useAwait(string name)
        {
            var t = new System.Net.WebClient().DownloadStringTaskAsync("http://www.etaco.ru/");//return Task<string>
            Console.Write("/"+name +": do somthing");
            var x = await t;//string
            Console.Write("/" + name + ":finish: " + x.Length);
            string result = await System.Threading.Tasks.Task<string>.Factory.StartNew(() => { Thread.Sleep(5000); return name + ":OK";});
            return result;
            
            //Async.useAwait(); Console.WriteLine("!");Thread.Sleep(5000);Console.WriteLine("!!");=>//do somthing//!//finish: 135116//!!//OK
            //если useAsync что-то возвращает (не void), то при вызове Async.useAwait("x"); - будет выдваться warning, что нужно использовать await (но без него работает)
            //Async.useAwait("x"); Console.Write("/!"); Console.Write("/?" + (await Async.useAwait("y"))); Console.Write("/!!");
            //x: do somthing/!/y: do somthing/x:finish: 142848/y:finish: 142849/?y:OK/!!
            //var x = Async.useAwait("x"); Console.Write("/!"); Console.Write("/?" + (await Async.useAwait("y"))); Console.Write("/??" + await x); Console.Write("/!!");
            //x: do somthing/!/y: do somthing/y:finish: 142849/x:finish: 143027/?y:OK/??x:OK/!!
        }*/
        //
        /*var eventList = GetIncomingMessages();
        if (eventList.Any()) return eventList;
        var token = new CancellationTokenSource();
        tokenStore.Add(clientId, token);            
        token.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
        return GetIncomingMessages();
        // 
        srvBrk.OnMessage(msg=>tokenStore[msg.clientId].Cancel());
        //
         var task = Task.Run(() =>{token.Token.Register(() => result += "<ActionOnCancel>"); result += token.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)) ? "<onCancel>" : "<onTimeout>";});*/
    }

    public class Async<T> : Async 
    {
        private object _lock = new object();
        private Func<T> func;
        private T result;
        public Async(Func<T> func, Action<Async<T>> onResult)
        {
            this.func = () => { thread = Thread.CurrentThread; return func(); };
            SetAsync(onResult);
        }
        public Async(Func<AsyncState, T> func, Action<Async<T>> onResult)
        {
            this.func = () => { thread = Thread.CurrentThread; state = new AsyncState(); return func(state); };
            SetAsync(onResult);
        }
        private void SetAsync(Action<Async<T>> onResult)
        {
            AsyncResult = (AsyncResult)(onResult == null ? func.BeginInvoke(null, null) : func.BeginInvoke(v => { try { onResult(this); } catch (Exception e) { AppContext.Log.HandleException(e); } }, null));
        }
        /// <summary>Возвращает результат и проверяет на наличие Exception при вызове делегата (ждёт завершения делегата)</summary>
        public T GetResult()
        {
            lock (_lock) { return AsyncResult.EndInvokeCalled ? result : result = func.EndInvoke(AsyncResult); }
        }
    }
}
 