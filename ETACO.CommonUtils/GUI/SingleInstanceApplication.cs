using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ETACO.CommonUtils
{
    /// <example>
    /// static void Main()
    /// {
    ///     using (var sai = new SingleInstanceApplication("MyMegaApplication.Instance"))
    ///        if (sai.FirstInstance)
    ///        {
    ///            Application.EnableVisualStyles();
    ///            Application.SetCompatibleTextRenderingDefault(false);
    ///            Attribute.Run(new MainForm());
    ///        }
    ///        else
    ///            sai.ActivateFirstInstance();
    /// }
    /// </example>
    public class SingleInstanceApplication : IDisposable
    {
        private Mutex mtx = null;
        private bool isFirstInstance = false;
        public string ApplicationName { get; private set; }
        public bool FirstInstance { get { return isFirstInstance; } }


        public SingleInstanceApplication(string ApplicationName)
        {
            this.ApplicationName = ApplicationName;
            mtx = new Mutex(true, ApplicationName, out isFirstInstance);
        }

        public void Dispose()
        {
            if (mtx != null)
                mtx = null;
        }

        public bool ActivateFirstInstance()
        {
            isFirstInstance = !ActivatePreviosInstance();
            return !isFirstInstance;
        }

        public static bool ActivatePreviosInstance()
        {
            var currentProcess = Process.GetCurrentProcess();
            //перебираем все процессы с аналогичным именем 
            foreach (Process process in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                //текущий экземпляр нас не интересует 
                //могут быть разные приложения с одинаковым именем исполняемого файла. Проверяем что-бы это был 'наш' файл 
                if (process.Id != currentProcess.Id && process.MainModule.FileName == currentProcess.MainModule.FileName)
                {
                    if (IsIconic(process.MainWindowHandle))
                        ShowWindow(process.MainWindowHandle, 9); //SW_RESTORE = 9
                    ShowWindow(process.MainWindowHandle, 5); // SW_SHOW = 5
                    SetForegroundWindow(process.MainWindowHandle);
                    return true;
                }
            }
            return false;
        }

        public static bool AlradyRunning(string AppName)
        {
            using (var sai = new SingleInstanceApplication(AppName))
                return !sai.FirstInstance;
        }

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("User32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
