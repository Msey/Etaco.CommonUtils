using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.ServiceProcess;
using System.Windows.Forms;
using ETACO.CommonUtils.Script;
using ETACO.CommonUtils.WinService;

namespace ETACO.CommonUtils
{
    public static class AppContext
    {
        private static Assembly _entryAssembly = Assembly.GetEntryAssembly();//== null when a managed assembly has been loaded from an unmanaged application (COM) or another domain
        /// <summary> Глобальный кэш для хранения и передачи параметров </summary>
        public static readonly Dictionary<string, object> Cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public static readonly int ProcessId;
        /// <summary> Выполняется ли код в режиме редактора графических компонент</summary>
        public static readonly bool IsDesignMode;
        public static Log Log { get { return Singleton<Log>.Instance; } }
        public static Config Config { get { return Singleton<Config>.Instance; } set { Singleton<Config>.Instance = value; } }
        public static JSEval JSEval { get { return Singleton<JSEval>.Instance; } set { Singleton<JSEval>.Instance = value; } }
        public static PluginManager PluginManager { get { return Singleton<PluginManager>.Instance; } set { Singleton<PluginManager>.Instance = value; } }
        public static OraDataAccess ORA { get { return Singleton<OraDataAccess>.Instance; } set { Singleton<OraDataAccess>.Instance = value; } }
        public static MSSQLDataAccess MsSQL { get { return Singleton<MSSQLDataAccess>.Instance; } set { Singleton<MSSQLDataAccess>.Instance = value; } }
        public static MySQLDataAccess MySQL { get { return Singleton<MySQLDataAccess>.Instance; } set { Singleton<MySQLDataAccess>.Instance = value; } }
        public static OleDBDataAccess OLEDB { get { return Singleton<OleDBDataAccess>.Instance; } set { Singleton<OleDBDataAccess>.Instance = value; } }
        public static T GetService<T>()  where T : class, new() { return Singleton<T>.Instance; }//generic property Instance<T> not supported 

        static AppContext() //доступ к глобальным сервисам только через xApp (ответсвенность за инициализацию только глобальных сервисов)  
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { Log.HandleException((Exception)e.ExceptionObject); if (e.IsTerminating) Environment.Exit(0); };
            Application.ThreadException += (s, e) => Log.HandleException(e.Exception); //при возниконвении ошибки приложение не завершится
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");//по умолчанию ставим так, при необходимости переопределим потом
            //System.Threading.Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator = ".";
            AppDomain.CurrentDomain.AssemblyLoad += (s, e) => Log.Trace("AssemblyLoad => " + e.LoadedAssembly.FullName);
            ProcessId = Process.GetCurrentProcess().Id;
            IsDesignMode = Process.GetCurrentProcess().ProcessName.ToLower().Contains("devenv");
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {//!!! необходимо для загрузки plugin из некорневой папки и отслеживания из зависимостей!!! не удолять  !!!!
                Log.Trace("AssemblyResolve => " + e.Name);
                var a = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == e.Name);
                if (a != null)
                {
                    Log.Trace("AssemblyResolve (assembly already loaded) => " + e.Name);
                    return a;
                }
                
                var name = new AssemblyName(e.Name).Name; 
                var di = new DirectoryInfo(Path.Combine(AppDir, Config["pluginmanager", "path"]));
                if (di.Exists)
                {
                    var files = di.GetFiles(name + ".dll", SearchOption.AllDirectories);
                    if (files.Length == 0) files = di.GetFiles(name + ".exe", SearchOption.AllDirectories);
                    foreach (var lib in files)
                    {
                        try
                        {
                            Log.Trace("AssemblyResolve (use LoadFrom) => " + lib.FullName);
                            return Assembly.LoadFrom(lib.FullName);
                        }
                        catch (BadImageFormatException ex) { Log.Trace("BadImageFormatException => " + lib.FullName + " err:" + ex.Message); } //x86/x64
                    }
                }
                /*using (var resource = new MemoryStream(Properties.Resources.ETACO_CommonUtils))
                using (var deflated = new DeflateStream(resource, CompressionMode.Decompress))
                using (var reader = new BinaryReader(deflated))
                {
                    return Assembly.Load(reader.ReadBytes(1024 * 1024));
                }*/
                return null;
            };
            try
            {
                if(!IsDesignMode) Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            }
            catch (Exception ex)
            {
                throw new Exception(">>> Use ETACO.AppContext before any Controls are created on the thread. <<<", ex);
            }
        }

        public static void Init() { /*только для вызова static AppContext()*/}

        internal const string onCmdEvalErrorText = "\nuse: '\\'-new line\nConsole.Clear()\nEnvironment.Version\nsc.Info()\nsc.Install()\nsc.Uninstall()\nsc.Start()\nsc.Stop()\nnew StringCrypter().EncryptToBase64('x')"
                        + "\nAppContext.Config['db','login']\ncmd('dir')\nDirectory.GetFiles('.')\nFileInfoExtensions.Zip(new FileInfo('xApp.exe.cfg'))\nnew FTPSClient('ftp.com', 'user', 'pass').GetList()"
                        + "\ncolwidth=20\nAssembly.LoadFrom('ETACO.CommonUtils.exe')\nora('select user from dual')\nFile.WriteAllText('x.txt',TableToString(mssql('select user'),42))\ndelete _.x;\nexit";

        [STAThread]
        public static void Main()
        {
            Config.LoadConfigFile();
            Log.LoadFromConfig(Config, m => { if (m.StartsWith("log/messageboxlog", StringComparison.Ordinal)) return new CommonMessageBoxLogger() { Mode = Config.GetParameter(m, "mode", LogMessageType.Error) }.WriteMessage; else return null; });
            var pluginPath = Path.Combine(AppDir, Config["pluginmanager", "path"]);
            PluginManager.LoadFromExecutingAssembly();
            PluginManager.Load(pluginPath);
            //if (pluginPath != AppDir) PluginManager.LoadFromExecutingAssembly(); //иначе ExecutingAssembly уже загруженна

            var regName = Config["winservicemanager", "serviceregistrator"];
            var sc = new ServicesContainer(GetServiceWorker(Config["winservicemanager", "serviceworkers"].TrimMultiline().Split(";"), false))
                .AddRange(GetServiceWorker(Config["winservicemanager", "registeredserviceworkers"].TrimMultiline().Split(";"),false,
                regName.IsEmpty() ? null : PluginManager.GetPlugin<IServiceRegistrator>(regName)))
                .AddRange(GetServiceWorker(Config["winservicemanager", "taskworkers"].TrimMultiline().Split(";"), true));

            if (!UserInteractive)//service mode
            {
                sc.EventLog.Source = Config["eventlog", "source", "EtacoService"];//Log.OnLog += new CommonEventLogLogger(sc.EventLog).WriteMessage;
                Log.Info("Service starting.\tPID = " + ProcessId);
                ServiceBase.Run(sc);
            }
            else //console mode 
            {
                JSEval.Set("sc", sc).Set("colwidth",20);
                JSEval.Eval("_.AppContext=ETACO.CommonUtils.AppContext");//Появился System.AppContext, hack для использования в консоли, в func это не поможет, нужно полное имя (см.ниже)
                JSEval.Eval("_.ora=function(s){return ETACO.CommonUtils.AppContext.ORA.GetQueryResult(new OraCommand(s));};_.mssql=function(s){return ETACO.CommonUtils.AppContext.MsSQL.GetQueryResult(new MSSQLCommand(s));};_.cmd=function(s){return ProcessUtils.ExecuteShell(true, s);}");

                var cc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("Set Console/Properties/Options/QuickEdit Mode=true");
                Console.ForegroundColor = cc;
                Console.WriteLine("=>");

                /*var jss = new JSEval<BaseEval>(true);
                  RunBenchmark(() => jss.Eval("desc(this)"), 100000, 10000, "Fast Eval");*/
                //TableList.TestTableList.Test();

                for (string line = Console.ReadLine().Trim(), cmd = line.ToLowerInvariant(); cmd != "exit"; Console.Write(line.IsEmpty() ? "=>\n" : ""), line += Console.ReadLine().Trim(), cmd = line.ToLowerInvariant())
                {
                    try
                    {
                        if (line.Length > 0 && line[line.Length - 1] == '\\') { line = line.Substring(0, line.Length - 1); continue; }
                        Console.WriteLine("===");
                        var x = JSEval.Eval(File.Exists(line) ? new FileInfo(line).ReadToEnd().GetString() : line);
                        if(x!= null) Console.WriteLine(JSEval.Engine.desc(x));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                        Console.WriteLine(onCmdEvalErrorText);
                    }
                    line = "";
                }
                Console.WriteLine("\nPress any key to exit.");
                Console.ReadKey();
            }
        }

        private static List<ServiceWorker> GetServiceWorker(string[] serviceWorkers, bool taskMode, IServiceRegistrator registrator = null)
        {
            var workers = new List<ServiceWorker>();
            if (serviceWorkers == null) return workers;
            Array.ForEach(serviceWorkers, s =>
            {
                try
                {
                    workers.Add(taskMode?new TaskWorker(s):PluginManager.GetPlugin<ServiceWorker>(s));
                    var msg = "Add service: {0,-22}".FormatStr(s);
                    if (registrator != null)
                    {
                        try
                        {
                            Log.Info(msg + registrator.Register(s).IfEmpty(" registered", " Already registered: {0}"));
                        }
                        catch (Exception ex)
                        {
                            Log.Error(msg + " Can't register service: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Add service: '" + s + "'. Exception: " + ex.Message);
                }
            });
            return workers;
        }

        /// <summary> Поиск типа по его имени в рамках сборок загруженных в данный домен </summary> //Type.GetType ищет только в mscorlib.dll
        public static Type GetType(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
        }

        /// <summary> Возвращает локальный путь к файлу сборки </summary>
        public static string GetAssemblyFullFileName(Assembly assembly)
        {
            return new Uri(assembly.EscapedCodeBase).LocalPath;
        }

        /// <summary> Возвращает все файлы сборок </summary>
        public static List<string> GetAllAssemblies()
        {
            var v = new List<string>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { v.Add(a.Location);}catch{}//Location может выкидывать Exception //(a.GlobalAssemblyCache ? Path.GetFileName(a.Location) : a.Location)
            }
            return v;
        }

        /// <summary> Возвращает все файлы сборок на которые есть ссылки в assembly </summary>
        public static List<string> GetReferencedAssemblies(Assembly assembly = null)
        {
            var v = new List<string>();
            foreach (var name in (assembly ?? Assembly.GetExecutingAssembly()).GetReferencedAssemblies())
            {
                try { v.Add(Assembly.Load(name).Location);} catch { }
            }
            return v;
        }

        /// <summary>  Возвращает assembly процесса в домене приложения по умолчанию либо выполненый через AppDomain.ExecuteAssembly </summary>
        public static Assembly GetEntryAssembly()
        {//Assembly.GetEntryAssembly() == null для сервисов и appDomain.DoCallBack
            if (_entryAssembly != null) return _entryAssembly;
            var v = Process.GetCurrentProcess().MainModule.FileName;
            v = v.EndsWith(".vshost.exe") ? v.Substring(0, v.Length - 10) + "exe" : v; //debug mode
            return _entryAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.Location == v)??Assembly.LoadFrom(v);
        }

        /// <summary>  Создаёт и запускает код в доверенном домене (в исходный домен возвращает false, в доверенный true) </summary>
        /// <remarks>  Main(string[] args) {if (!AppContext.UsingTrustDomain()) return; ...</remarks>
        public static bool UsingTrustDomain(CrossAppDomainDelegate cadd = null)
        {
            if ((cadd == null || Assembly.GetEntryAssembly() == null) && AppDomain.CurrentDomain.FriendlyName == "CUTrustedAppDomain") return true;//"Trusted Domain"
            var appDomain = AppDomain.CreateDomain("CUTrustedAppDomain", null, new AppDomainSetup() { ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase }, new PermissionSet(PermissionState.Unrestricted));
            if (cadd != null) appDomain.DoCallBack(cadd); else appDomain.ExecuteAssembly(AppFullFileName);
            return false;
        }

        public static string[] GetNamespaces(Assembly assembly = null)
        {
            return (assembly ?? Assembly.GetExecutingAssembly()).GetTypes().Select(t => t.Namespace).Where(t => !t.IsEmpty()).Distinct().ToArray();
        }

        /// <summary> Возвращает имя файла выполняемой сборки </summary>
        /// <remarks> Работает только для сборок загруженных с диска черех LoadFrom, для загруженных через Load(byte[]) возвращает загружающую сборку</remarks>
        public static string ExecutingAssemblyFileName
        {
            get { return Path.GetFileName(GetAssemblyFullFileName(Assembly.GetCallingAssembly())); }
        }

        /// <summary> Возвращает имя директории, где расположена выполняемая сборка </summary>
        /// <remarks> Работает только для сборок загруженных с диска черех LoadFrom, для загруженных через Load(byte[]) возвращает загружающую сборку</remarks>
        public static string ExecutingAssemblyDirectoryName
        {
            //при загрузки сборок из byte[], а не из файла происходит следующее
            //Assembly.Location = "", Assembly.CodeBase = "file:///.../ETACO.CommonUtils.exe" - т.е. та сборка откуда происходит загрузка Assembly
            get { return Path.GetDirectoryName(GetAssemblyFullFileName(Assembly.GetCallingAssembly())); }
        }

        /// <summary> Возвращает полное имя файла приложения </summary>
        public static string AppFullFileName
        {
            //Assembly.GetEntryAssembly() == null when a managed assembly has been loaded from an unmanaged application (COM) //This member cannot be used by partially trusted code.
            get { return GetEntryAssembly().Location; /*Application.ExecutablePath;*/ }
        }

        /// <summary> Возвращает имя файла приложения </summary>
        public static string AppFileName
        {
            get { return Path.GetFileName(AppFullFileName); }
        }

        /// <summary> Возвращает имя директории приложения </summary>
        public static string AppDir
        {
            get { return Path.GetDirectoryName(AppFullFileName); }
        }

        public static string UserName
        {
            get { return Environment.UserName; }//System.Security.Principal.WindowsIdentity.GetCurrent().Name; 
        }

        public static string MachineName
        {
            get { return Environment.MachineName; }// Environment.UserName; 
        }

        /// <summary> Возвращает признак указывающий является ли данное приложение "консольным" </summary>
        public static bool IsConsoleApp
        {
            get { return UserInteractive && (Console.In != StreamReader.Null); }
        }

        /// <summary> Возвращает признак указывающий запущенно ли данное приложение с пользовательским интерфейсом</summary>
        public static bool UserInteractive
        {
            get { return Environment.UserInteractive; }
        }

        /// <summary> Возвращает признак указывающий запущенно ли данное приложение в режиме отладки</summary>
        public static bool IsDebugMode
        {
            get { return Debugger.IsAttached; }
        }

        /// <summary> Возвращает описатель текущего(исполняемого) метода </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]//fix bug release ver. inline - StackFrame(2, true) == null, FW4 - use [CallerMemberName] 
        public static MethodBase GetCurrentMethod()
        {
            return new StackFrame(1, true).GetMethod(); //MethodBase.GetCurrentMethod();
        }

        /// <summary> Возвращает описатель метода вызвавшего исполняемый метод </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MethodBase GetCallingMethod()
        {
            return new StackFrame(2, true).GetMethod();
        }

        /// <summary> Возвращает описатель типа вызвавшего исполняемый метод </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Type GetCallingType()
        {
            return new StackFrame(2, true).GetMethod().ReflectedType;
        }

        /// <summary> Возвращает имя переменной, параметра: GetMemberName(()=>x)</summary>
        public static string GetMemberName<T>(Expression<Func<T>> memberExpression)
        {
            return ((MemberExpression)memberExpression.Body).Member.Name;
        }

        private readonly static Random random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);
        public static double GetRandom(double max = 100, double min = 0) { lock(random) { return min+random.NextDouble()*(max-min); } }

        /// <summary> Вызов сборщика мусора (from Jeffrey Richter)</summary>
        public static void GarbageCollect()
        {
            GC.Collect();//all generations  (GC.MaxGeneration)
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary> Возвращает размер выделенной памяти (в байтах) </summary>
        public static long TotalAllocatedMemory
        {
            get { return GC.GetTotalMemory(false); } //если иcпользовать true, то сначала вызовится сборка мусора
        }

        /// <summary> Определяет время выполнения кода (Action) с возможностью прогрева перед запуском </summary>
        public static long RunBenchmark(Action a, int iterationCount = 1, int warmupCount = 0, string name = "", Action<string> onReport = null)
        {
            if (a == null) throw new Exception("Action for Benchmark {0} is null".FormatStr(name));
            iterationCount = Math.Max(1, iterationCount);
            warmupCount = Math.Max(0, warmupCount);
            GarbageCollect();
            var mem = TotalAllocatedMemory;
            var sw = new Stopwatch();
            for (int i = 0; i < warmupCount; i++) a();
            sw.Start();
            for (int i = 0; i < iterationCount; i++)  a();
            sw.Stop();
            (onReport ?? Console.WriteLine)("Benchmark {0}\t:\ttotal = {1:N0} ms.\tavg = {2:N0} ms.\titeration = {3:N0}\tmem = {4:N0}".FormatStr(name, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds / iterationCount, iterationCount, TotalAllocatedMemory - mem));
            return sw.ElapsedMilliseconds;
        }

        //размер объекта не зависит от количества методов, в каждом объекте есть ссылка на MethodTable с описанием типа класса и его методов (служебной информации 8 байт для x86 приложения)
        //размер объекта зависит от количества полей и неявных свойств (get;set без реализации), т.к. они создают скрытие поля (размер поля - это размер ссылки + размер ссылаемого объекта)
        //пустой класс - на x86 - 12 байт, x64 - 24 байта (из-за разницы в размере ссылок)
        //!!!скорость создания объекта зависит ТОЛЬКО от количества полей (+ логика конструктора)
        //!!! простые get,set inline и скорость практически не отличается для свойств и полей (но это для release без VS, для  debug - по другому)
        //Nullable требуют в среднем на 4 байта больше, для хранения bool поля SizeOf(() => new decimal[100000]) vs SizeOf(() => new decimal?[100000])
        public static long SizeOf<T>(Func<T> createInstance)//for release
        {
            if (typeof(T).IsValueType) return Marshal.SizeOf(createInstance());
            var start = GC.GetTotalMemory(true);
            var v = createInstance();//!!! тут ещё добавляется память на создание ссылки v
            var stop = GC.GetTotalMemory(true);
            GC.KeepAlive(v);//чтобы сборщик мусора не собрал объект раньше
            return stop - start;
        }

        /// <summary> Запущенно ли приложение как x64 </summary>
        /// <remarks> Приложение может быть запущенно на платформе x64 как x86</remarks>
        public static bool Is64BitApp //Environment.Version - версия используемого FW
        {
            get { return IntPtr.Size == 8; }
        }

        /// <summary> Является ли OS x64 </summary>
        public static bool Is64BitOS
        {
            get
            {
                if (Is64BitApp) return true;
                var handle = LoadLibrary("kernel32");
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        var fnPtr = GetProcAddress(handle, "IsWow64Process");
                        if (fnPtr != IntPtr.Zero)
                        {
                            bool isWow64;
                            ((IsWow64Process)Marshal.GetDelegateForFunctionPointer(fnPtr, typeof(IsWow64Process))).Invoke(Process.GetCurrentProcess().Handle, out isWow64);
                            return isWow64;
                        }
                    }
                    finally
                    {
                        FreeLibrary(handle);
                    }
                }
                return false;
            }
        }

        public static bool IsRunningOnMono
        {
            get { return Type.GetType("Mono.Runtime") != null; }
        }

        public static bool IsIISHosted
        {
            get { return System.Web.Hosting.HostingEnvironment.IsHosted; }
            //return Path.IsPathRooted(path) ? path : System.Web.Hosting.HostingEnvironment.MapPath(path);  
        }

        [DllImport("kernel32", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        private extern static IntPtr LoadLibrary(string libraryName);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        private extern static IntPtr GetProcAddress(IntPtr hwnd, string procedureName);
        private delegate bool IsWow64Process([In] IntPtr handle, [Out] out bool isWow64Process);
    }
}