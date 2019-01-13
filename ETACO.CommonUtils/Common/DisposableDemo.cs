//using System;
//using System.ComponentModel;
//using System.Runtime.ConstrainedExecution;

/*
Нужно придерживаться следующих правил:
1. Обычные объекты — не хранят ссылки на Disposable объекты или неуправляемые ресурсы. Для них не нужно реализовывать ни финализатор, ни IDisposable.
2. Объекты, хранящие ссылки на Disposable объекты. Для них следует реализовать IDisposable, но не финализатор. В методе Dispose нужно просто вызвать последовательно методы Dispose всех членов класса,
   реализующих соответствующий интерфейс.
3. Объекты, хранящие ссылку на неуправляемый ресурс. Для них должны быть реализованы как финализатор, так и интерфейс IDisposable. Метод Dispose этого объекта должен освобождать неуправляемый ресурс,
   при этом он должен быть реализован так, что-бы его можно было вызвать МНОГОКРАТНО. При этом в Dispose следует вызвать GC.SuppressFinalize(this), что-бы для объекта не вызывался его финализатор.
   В финализаторе также следует освободить неуправляемый ресурс. Финализатор нужен на тот случай, если пользователь класса не вызвал Dispose.
 */

/*namespace ETACO.CommonUtils
{
    /// <summary> Демо класс для работы с неуправляемыми ресурсами </summary>
    /// <remarks> Данный пример доступен в описании метода GC.SuppressFinalize</remarks>
    /// <remarks> В качестве рабочего примера имеет смысл посмотреть классы SafeHandle and CriticalHandle - наследников которых гаранированно вызвает CLR (см. Рихтер) </remarks>
    class DisposableDemo : IDisposable
    {
        private bool disposed = false;
        // Указатель на неуправляемый ресурс.
        private IntPtr handle;
        // Управляемый ресурс.
        private readonly Component component = new Component();
        
        public DisposableDemo(IntPtr handle) { this.handle = handle; }

        // Нельзя делать этот метод virtual, чтобы в дочерних классах не могли его переопределить
        public void Dispose() { Dispose(true); }

        // обращение из диструктора к полям класса небезопасно, т.к. GC мог уже освободить ресурсы выделенные под них 
        ~DisposableDemo() { Dispose(false);}

        private void Dispose(bool disposing)
        {
            if(disposed) //если ещё не освободили ресурс
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;            
                if (disposing) //если вызываем ручками, то нужно освободить управляемые ресурсы иначе, произошёл вызов finalize из GC и управляемые ресурсы будут освобождены GC
                {
                    component.Dispose();
                    //Выкинуть объект из F-списка, чтобы его финализатор не вызывался, т.к. мы уже освободили ресурс 'руками'
                    GC.SuppressFinalize(this);
                }
            }
            disposed = true;         
        }

        [System.Runtime.InteropServices.DllImport("Kernel32")]
        private extern static Boolean CloseHandle(IntPtr handle);
    }
}*/
