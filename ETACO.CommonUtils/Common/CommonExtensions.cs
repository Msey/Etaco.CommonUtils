using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с базовыми типами </summary>
    public static class CommonExtension
    {
        /// <summary> Пытается выполнить 'action' 'count' раз. В случае успеха сразу возвращает управление иначе ждёт delay и повторяет попытку.</summary>
        public static void TryInvoke(this int count, Action action, int delay = 1000)//Operator '.' cannot be applied to operand of type 'lambda expression'
        {
            TryInvoke(count, () => { action(); return 0; }, delay);
        }

        public static T TryInvoke<T>(this int count, Func<T> action, int delay = 1000)
        {
            while ((count--) > 0)
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    if (count == 0) if (ex is TargetInvocationException) throw ex.InnerException; else throw;
                    Thread.Sleep(Math.Max(delay, 0));
                }
            }
            return default(T);
        }
        public static long Pow(this int num, int i)
        {
            if (i == 0) return 1;
            if (i == 1) return num;
            long v = 1;
            long x = num;//int может переполниться
            while (i > 0)
            {
                if ((i & 1) == 1) v *= x;//if(i%2 == 1)
                i >>= 1;
                x *= x;
            }
            return v;
        }

        /// <summary> Если объект не пуст, то выполнить для него делегат, иначе вернуть null </summary>
        public static TOut IfNotNull<TIn, TOut>(this TIn v, Func<TIn, TOut> f) where TIn : class where TOut : class
        {
            return (v == null) ? null : f(v);
        }

        /// <summary> Вызов метода/свойства/поля у объекта (в том чисте и статического)</summary>
        /// <remarks> Для работы со статическими классами нужно использовать подход typeof(MyClass)._InvokeMember</remarks>
        public static object _InvokeMember(this object obj, string member, BindingFlags flags, Binder binder,  params object[] args)
        {
            if (obj == null) throw new Exception("Object for InvokeMember is null");
            if (args == null) args = new object[] { null };
            try
            {
                var t = obj as Type;    //as+if быстрее, чем is+cast
				return t == null ? obj.GetType().InvokeMember(member, flags, binder, obj, args) : t.InvokeMember(member, flags, binder, null, args);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        /// <summary> Вызов метода у объекта </summary>
        /// <remarks> При множественном вызове лучше кешировать MethodInfo и вызывать его (быстрее раза в 2)</remarks>
        public static object _InvokeMethod(this object obj, string member,  params object[] args)
        {
            return _InvokeMember(obj, member, BindingFlags.InvokeMethod, Type.DefaultBinder, args);
        }

        /// <summary> Вызов generic метода (если obj is Type - то ищется статический метод)</summary>
        public static object _InvokeGenericMethod(this object obj, string member, Type[] genericTypes, params object[] args)
        {
            if (obj == null) throw new Exception("Object for InvokeGenericMethod is null");
            if (genericTypes == null || genericTypes.Length == 0) throw new Exception("GenericTypes for InvokeGenericMethod is empty.");
            if (args == null) args = new object[] { null };
            try
            {
                //var types = new Type[args.Length];
                //for (int i = 0; i < args.Length; i++) types[i] = args[i].GetType(); //ifNull???
                var t = obj as Type;    //as+if быстрее, чем is+cast
				return t==null ? obj.GetType().GetMethod(member/*, types*/).MakeGenericMethod(genericTypes).Invoke(obj, args) : t.GetMethod(member/*, types*/).MakeGenericMethod(genericTypes).Invoke(null, args);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        /// <summary> Получение значения свойства </summary>
        public static object _GetProperty(this object obj, string property, params object[] args)
        {
            var result = obj;
            Array.ForEach(property.Split(new []{'.'}, StringSplitOptions.RemoveEmptyEntries), p => result = _InvokeMember(result, p, BindingFlags.GetProperty | BindingFlags.GetField, Type.DefaultBinder));
            return  args.Length == 0 ? result : result._InvokeMember("Item", BindingFlags.GetProperty, Type.DefaultBinder, args);
        }

        /// <summary> Установка значения свойства </summary>
        /// <remarks> Для установки свойства индексатора нужно использовать property = "Item" (AppContext.Cache.SetPropertyValue("Item", "x", 42))</remarks>
        public static object _SetProperty(this object obj, string property, params object[] args)
        {
            var result = obj;
            var path = property.IfEmpty("Item").Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < path.Length - 1; i++) result = _InvokeMember(result, path[i], BindingFlags.GetProperty | BindingFlags.GetField, Type.DefaultBinder);
            return _InvokeMember(result, path[path.Length - 1], BindingFlags.SetProperty | BindingFlags.SetField, Type.DefaultBinder, args);
        }

        /// <summary> Получение значения по умолчанию для данного типа</summary>
        private const string nullable = "Nullable`1";//performance
        public static object GetDefault(this Type type)
        {//type !=typeof(string) для ускорения, type.IsValueType - не быстрый
            return type !=typeof(string)&&type.Name!= nullable&&type.IsValueType? Activator.CreateInstance(type) : null;
        }

        ///<summary> Создание generic типа</summary>
        ///<remarks> если у конструктора параметров нет то нужно use  args = new object[0]</remarks>
        public static object CreateGeneric(this Type type, Type[] genericTypes, object[] args)
        {
            return Activator.CreateInstance(type.MakeGenericType(genericTypes), args);
        }

        public static bool IsAnonymous(this Type type)
        {
            return type.Namespace == null && !type.IsPublic&& type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0 && type.FullName.Contains("AnonymousType");
        }

        public static object _Invoke(this object obj, string code, object args = null)
        {
            return AppContext.JSEval.Eval(code, obj, args);
        }

        public static Func<object[], T> GetActivator<T>(this ConstructorInfo ctor)
        {
            var paramsInfo = ctor.GetParameters();
            var param = Expression.Parameter(typeof(object[]), "args");
            var argsExp = new Expression[paramsInfo.Length];

            for (int i = 0; i < paramsInfo.Length; i++) argsExp[i] = Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(i)), paramsInfo[i].ParameterType);
            return (Func<object[], T>)Expression.Lambda(typeof(Func<object[], T>), Expression.New(ctor, argsExp), param).Compile();
        }

        // var call = fEval.GetType().GetMethod("func").GetAction();
        // call(fEval, new[] { row });
        public static Action<object, object[]> GetAction(this MethodInfo method)
        {
            int index = 0;
            var p1 = Expression.Parameter(typeof(object), "instance");
            var p2 = Expression.Parameter(typeof(object[]), "parameters");
            var parameters = from p in method.GetParameters()
                             select Expression.Convert(Expression.ArrayAccess(p2,Expression.Constant(index++)),p.ParameterType);

            Expression instanceCheck = null;
            Expression call = null;
            if (method.IsStatic)//если метод статический то проверка на object!=null
            {
                instanceCheck = Expression.IfThen(Expression.NotEqual(p1,Expression.Constant(null)),
                                Expression.Throw(Expression.New(typeof(ArgumentException).GetConstructor(
                                    new Type[] {typeof(string),typeof(string) }),Expression.Constant("Argument must be null for a static method call."),
                                    Expression.Constant("instance"))));
                call = Expression.Call(method,parameters);
            }
            else
            {
                instanceCheck = Expression.IfThen(Expression.Equal(p1,Expression.Constant(null)),
                                Expression.Throw(Expression.New(typeof(ArgumentNullException).GetConstructor(
                                    new Type[] { typeof(string) }), Expression.Constant("instance"))));
                call = Expression.Call(Expression.Convert(p1, method.DeclaringType),method,parameters);
            }
            //собираем вместе проверку и сам вызов в одном выражении
            return Expression.Lambda<Action<object, object[]>>(Expression.Block(instanceCheck, call), p1, p2).Compile();
        }

        ///<summary> Попытка выполнить код для обьекта, если он является экземпляром класса нужного типа</summary>
        ///<remarks> obj.ApplyFor<T1>(x=>x.a()).ApplyFor<T2>(x=>x.y())</remarks>
        public static T ApplyFor<T, TExpected>(this T target, Action<TExpected> action)
        {
            if (target is TExpected) action((TExpected)(object)target);//class + struct (иначе можно было бы использовать as)
            return target;
        }

        public static bool IsNumeric(this Type type)
        {
            if (type == null) return false;
            
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                case TypeCode.Object: return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) ? IsNumeric(Nullable.GetUnderlyingType(type)) : false;
            }
            return false;
        }

        public static bool IsCommon(this Type type)
        {
            var v = Type.GetTypeCode(type);
            return !(v == TypeCode.Empty || (v == TypeCode.Object && type != typeof(object)));
        }

        public static string[] GetConstructorsDescription(this Type type, bool useNamespace = true, BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)
        {
            return type.GetConstructors(bf).Select(mi => GetMethodDescription(mi, useNamespace)).ToArray();
        }

        public static string GetMethodDescription(this MethodBase mi, bool useNamespace = true)
        {
            var retType = mi is MethodInfo ? ((MethodInfo)mi).ReturnType.GetFriendlyName(useNamespace)+" ": "";
            var name = mi.IsConstructor ? mi.ReflectedType.Name : mi.Name;
            return mi.IsGenericMethod? "{0}{1}<{2}>({3})".FormatStr(retType, name, string.Join(", ", mi.GetGenericArguments().Select(x => x.GetFriendlyName(useNamespace))), string.Join(", ", mi.GetParameters().Select(x => x.ParameterType.GetFriendlyName(useNamespace) + " " + x.Name).ToArray()))
            :"{0}{1}({2})".FormatStr(retType, name, string.Join(", ", mi.GetParameters().Select(x => x.ParameterType.GetFriendlyName(useNamespace) + " " + x.Name)));
        }

        public static string[] GetMethodsDescription(this Type type, bool useNamespace = true, BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)
        {
            return type.GetMethods(bf).Where(mi => !mi.IsSpecialName).Select(mi => GetMethodDescription(mi, useNamespace)).ToArray();
        }

        public static List<string> GetPropertiesDescription(this Type type, bool useNamespace = true, BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)
        {
            var v = new List<string>();
            foreach (var m in type.GetProperties(bf).Where(p=>!p.IsSpecialName))
            {
                var s = "{0} {1}".FormatStr(m.PropertyType.GetFriendlyName(useNamespace), m.Name);
                var indx = m.GetIndexParameters();
                v.Add((indx.Length == 0 ? s : s + "[" + string.Join(", ", indx.Select(i => i.ParameterType.GetFriendlyName(useNamespace))) + "]") + "{" + (m.CanRead? "get;":"") + (m.CanWrite?"set;":"") + "}");
            }
            return v;
        }

        public static List<string> GetMethodsName(this Type type, BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)
        {
            return type.GetMethods(bf).Where(m => !m.IsSpecialName).Select(m => m.Name).ToList();
        }

        public static List<string> GetPropertiesName(this Type type, BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)
        {
            return type.GetProperties(bf).Where(p => !p.IsSpecialName).Select(p => p.Name).ToList();
        }

        public static List<string> GetFieldsName(this Type type, BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)
        {
            return type.GetFields(bf).Where(f => !f.IsSpecialName).Select(p => p.Name).ToList();
        }

        public static string GetFriendlyName(this Type type, bool useNamespace = true)
        {
            if (!type.IsGenericType) return type.FullName == null||!useNamespace ? type.Name : type.FullName;//for T in List<T>
            var v = type.FullName ?? type.Name;//FullName может и не быть
            v = v.Remove(v.IndexOf('`')) + "<";
            if (!useNamespace && v.LastIndexOf(".") > 0) v = v.Substring(v.LastIndexOf(".")+1);//FullName to short form
            var tParams = type.GetGenericArguments();
            for (int i = 0; i < tParams.Length; ++i) v += (i == 0 ? "" : ",") + tParams[i].GetFriendlyName(useNamespace);
            return (useNamespace?type.Namespace+".":"") + v + ">";
        }

        public static IEnumerable<Type> GetBaseTypes(this Type type, bool includeThis = false, Type stopType = null)
        {
            for (Type t = includeThis?type:type.BaseType, prev = type; t != null&&prev!= stopType; prev = t, t = t.BaseType) yield return t;
        }

        public static T[] AsArray<T>(this object v) { return v == null || v == DBNull.Value ? Array<T>.Empty : (T[])v; }
        public static long GetMemSize(this object v)
        {
            var x = new ObjSizer();
            new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(x, v);
            return x.Length;
        }

        private class ObjSizer : System.IO.Stream
        {
            private long totalSize;
            public override void Write(byte[] buffer, int offset, int count){totalSize += count;}
            public override bool CanRead{get { return false; }}
            public override bool CanSeek{get { return false; }}
            public override bool CanWrite{ get { return true; }}
            public override void Flush() { }
            public override long Length { get { return totalSize; }}
            public override long Position {get{throw new NotImplementedException();}set { throw new NotImplementedException();}}
            public override int Read(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }
            public override long Seek(long offset, System.IO.SeekOrigin origin) { throw new NotImplementedException(); }
            public override void SetLength(long value) { throw new NotImplementedException();}
        }

        public static string GetFullExceptionText(this Exception ex)
        {
            if (ex == null) return "";
            if (ex.InnerException == null) return ex.Message;
            var v = GetFullExceptionText(ex.InnerException);
            return v.StartsWith(ex.Message, StringComparison.Ordinal) ? v : ex.Message + Environment.NewLine + v;
        }
    }
}
