/*using System;

namespace ETACO.CommonUtils
{
    /// <summary> Класс помошник. Помогает избежать множественных проверок на равенство null промежуточного результата  </summary>
    /// <code>int repository = Maybe.From(log).Select(l => l.LoggerList).Select(l => l.Length, 300);</code> 
    public static class Maybe
    {
        /// <summary> Возвращает имплементацию класса обёртки для передаваемого параметра </summary>
        public static Maybe<T> From<T>(T value) where T : class
        {
            return new Maybe<T>(value);
        }
    }

    /// <summary> Класс обёртка. Помогает избежать множественных проверок на равенство null промежуточного результата  </summary>
    public struct Maybe<T> where T : class
    {
        private readonly T _value;

        public Maybe(T value)
        {
            _value = value;
        }
        
        /// <summary> Возвращает имплементацию класса обёртки для промежуточного результата </summary>
        public Maybe<TResult> Select<TResult>(Func<T, TResult> getter) where TResult : class
        {
            return new Maybe<TResult>((_value == null) ? null : getter(_value));
        }

        /// <summary> Возвращает результат вычислений или значение по умолчанию </summary>
        public TResult Select<TResult>(Func<T, TResult> getter, TResult alternative)
        {
            return (_value == null) ? alternative : getter(_value);
        }

        /// <summary> Вызывает делегат над обектом содержащимся в классе обёртке </summary>
        public void Do(Action<T> action)
        {
            if (_value != null)
                action(_value);
        }
    }
}*/