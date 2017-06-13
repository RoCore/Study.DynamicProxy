using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
#if(NETSTANDARD1_6)
using Microsoft.Extensions.Caching.Memory;
#else
using System.Collections.Concurrent;
#endif
namespace FastProxy
{
    public static class DynamicTypeBuilder
    {
        private const string ProxyPrefix = "Proxy";
        private const string DecoratorName = "_decorator";
        private const string ProxyInvoker = "_proxyInvoker";
        private const string DynamicAssemblyName = "DynamicAssembly";
        private static readonly ReaderWriterLockSlim BuildersSlimLock = new ReaderWriterLockSlim();

        private static AssemblyBuilder _builder;
        private static ModuleBuilder _moduleBuilder;

#if (NETSTANDARD1_6)
        private static readonly IMemoryCache MemoryCache = new MemoryCache(new MemoryCacheOptions());

        private static TypeBuilder GetCacheValue<TBase, T, TInterceptor>(string key, ModuleBuilder builders)
            where TInterceptor : IInterceptor, new()
        {
            return MemoryCache.GetOrCreate(key, a =>
            {
                a.SlidingExpiration = TimeSpan.FromMinutes(10);
                return builders.CreateType<TBase, T, TInterceptor>();
            });
        }
#else
        private static readonly ConcurrentDictionary<string, TypeBuilder> Cache = new ConcurrentDictionary<string, TypeBuilder>();

        private static TypeBuilder GetCacheValue<TBase, T, TInterceptor>(string key, ModuleBuilder builders)
            where TInterceptor : IInterceptor, new()
        {
            return Cache.GetOrAdd(key, a => builders.CreateType<TBase, T, TInterceptor>());
        }
#endif

        public static T Build<T, TInterceptor>()
            where TInterceptor : IInterceptor, new()
        {
            var builders = CreateBuilders();
            var type = GetCacheValue<T, T, TInterceptor>(typeof(T).FullName, builders);
            return (T)Activator.CreateInstance(type.CreateTypeInfo().AsType());
        }

        //public static TBase CreateProxy<TBase, T>()
        //    where T : TBase
        //{

        //}

        private static ModuleBuilder CreateBuilders()
        {
            try
            {
                BuildersSlimLock.EnterWriteLock();
                if (_builder == null)
                {
                    AssemblyBuilderAccess access = AssemblyBuilderAccess.Run;
                    _builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(DynamicAssemblyName), access);
                    _moduleBuilder = _builder.DefineDynamicModule("test");
                }
            }
            finally
            {
                BuildersSlimLock.ExitWriteLock();
            }
            return _moduleBuilder;
        }

        private static FieldBuilder CreateProxyInvokerInConstuctor<TInterceptor>(this TypeBuilder typeBuilder, ILGenerator generator)
            where TInterceptor : IInterceptor, new()
        {
            var interceptorType = typeof(TInterceptor);
            var invoker = typeBuilder.DefineField(ProxyInvoker, interceptorType, FieldAttributes.InitOnly);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Newobj, interceptorType.GetConstructor(Type.EmptyTypes));
            generator.Emit(OpCodes.Stfld, invoker);
            return invoker;
        }

        private static TypeBuilder CreateType<TBase, T, TInterceptor>(this ModuleBuilder moduleBuilder)
            where TInterceptor : IInterceptor, new()
        {
            var baseType = typeof(TBase);
            var implentedType = typeof(T);
            var typeInfoImplemented = implentedType.GetTypeInfo();
            if (typeInfoImplemented.IsClass && typeInfoImplemented.IsSealed && baseType == implentedType)
            {
                throw new InvalidOperationException("Not possible to create a proxy if base type is sealed");
            }
            FieldBuilder decorator = null;
            FieldBuilder interceptorInvoker = null;
            TypeBuilder result;
            PropertyInfo[] properties;
            MethodInfo[] methods;
            bool defaultConstructorImplemented = false;
            if (typeInfoImplemented.IsClass)
            {
                if (typeInfoImplemented.IsSealed)
                {
                    result = moduleBuilder.DefineType(string.Concat(ProxyPrefix, baseType.Name), TypeAttributes.Public | TypeAttributes.Class, baseType);
                    decorator = result.DefineField(DecoratorName, baseType, FieldAttributes.InitOnly);

                    var constructor = result.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                    var ilGenerator = constructor.GetILGenerator();
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Newobj, baseType.GetConstructor(Type.EmptyTypes));
                    ilGenerator.Emit(OpCodes.Stfld, decorator);
                    interceptorInvoker = result.CreateProxyInvokerInConstuctor<TInterceptor>(ilGenerator);
                    ilGenerator.Emit(OpCodes.Ret);
                    defaultConstructorImplemented = true;
                }
                else
                {
                    result = moduleBuilder.DefineType(string.Concat(ProxyPrefix, baseType.Name), TypeAttributes.Public | TypeAttributes.Class, implentedType);
                    result.SetParent(baseType);
                }
                methods = baseType.GetMethods(BindingFlags.Instance);
                properties = baseType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            }
            else if (typeInfoImplemented.IsInterface)
            {
                result = moduleBuilder.DefineType(string.Concat(ProxyPrefix, baseType.Name), TypeAttributes.Public | TypeAttributes.Class);
                result.AddInterfaceImplementation(baseType);
                methods = baseType.GetMethods();
                properties = baseType.GetProperties();
            }
            else
            {
                //TODO struct
                throw new NotImplementedException();
            }

            if (defaultConstructorImplemented == false)
            {
                var constructor = result.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                var ilGenerator = constructor.GetILGenerator();
                interceptorInvoker = result.CreateProxyInvokerInConstuctor<TInterceptor>(ilGenerator);
                ilGenerator.Emit(OpCodes.Ret);
            }
            result.CreateProperties(properties, decorator, typeInfoImplemented.IsInterface);
            result.CreateMethods<TInterceptor>(methods, decorator, typeInfoImplemented.IsInterface, interceptorInvoker);
            return result;
        }

        private static void CreateProperties(this TypeBuilder builder, PropertyInfo[] properties, FieldBuilder decorator, bool isInterfaceType)
        {

        }

        private static MethodInfo EmptyTaskCall { get; } = typeof(Task).GetMethod(nameof(Task.FromResult), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object));
        private static ConstructorInfo TaskWithResult { get; } = typeof(Task<object>).GetConstructor(new[] { typeof(Func<object, object>), typeof(object) });
        private static ConstructorInfo AnonymousFuncForTask { get; } = typeof(Func<object, object>).GetConstructor(new[] { typeof(object), typeof(IntPtr) });

        private static Type InterceptorValuesType { get; } = typeof(InterceptorValues);
        private static ConstructorInfo InterceptorValuesConstructor { get; } = typeof(InterceptorValues).GetConstructor(new[] { typeof(object), typeof(string), typeof(IEnumerable), typeof(Task<object>) });
        private static MethodInfo InvokeInterceptor { get; } = typeof(IInterceptor).GetMethod(nameof(IInterceptor.Invoke));


        private static MethodBuilder CreateBaseCallForTask(this TypeBuilder builder, MethodInfo baseMethod, long counter, ParameterInfo[] parameters)
        {
            var result = builder.DefineMethod(string.Concat(baseMethod.Name, "_Execute_", counter), MethodAttributes.Private | MethodAttributes.HideBySig, typeof(object), new[] { typeof(object) });

            var generator = result.GetILGenerator();

            var items = generator.DeclareLocal(typeof(object[]));
            if (parameters.Length > 0)
            {
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Castclass, items.LocalType);
                generator.Emit(OpCodes.Stloc_0);
                generator.Emit(OpCodes.Ldarg_0); //this.
                generator.Emit(OpCodes.Ldloc_0); //items
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Ldelem_Ref);
                if (parameters[i].ParameterType != typeof(object))
                {
                    OpCode casting = parameters[i].ParameterType.GetTypeInfo().IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass;
                    generator.Emit(casting, parameters[i].ParameterType);
                }
            }
            //(params ...)
            generator.Emit(OpCodes.Call, baseMethod);
            generator.Emit(OpCodes.Ret);

            return result;
        }

        private static void CreateMethods<TInterceptor>(this TypeBuilder builder, MethodInfo[] methods, FieldBuilder decorator, bool isInterfaceType, FieldBuilder interceptorInvoker)
            where TInterceptor : IInterceptor, new()
        {
            if (interceptorInvoker == null)
            {
                throw new ArgumentNullException(nameof(interceptorInvoker));
            }
            long methodCounter = 0;
            foreach (var item in methods)
            {
                MethodAttributes attributes = item.Attributes;
                if (isInterfaceType)
                {
                    attributes = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
                }
                else if (item.IsAbstract)
                {
                    attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
                }
                var parameters = item.GetParameters();
                var method = builder.DefineMethod(item.Name, attributes, item.CallingConvention, item.ReturnType, parameters.Select(a => a.ParameterType).ToArray());
                MethodBuilder taskMethod = null;
                if ((item.IsAbstract || isInterfaceType) == false)
                {
                    taskMethod = builder.CreateBaseCallForTask(method, methodCounter, parameters);
                }
                if (item.ContainsGenericParameters)
                {
                    //TODO
                    //method.DefineGenericParameters
                    throw new NotImplementedException();
                }
                var generator = method.GetILGenerator();

                var listType = typeof(object[]);

                //object[] items; {0}
                var items = generator.DeclareLocal(listType);
                //Task<object> task; {1}
                var taskOfObject = generator.DeclareLocal(typeof(Task<object>));
                //InterceptorValues interceptorValues; {2}
                var interceptorValues = generator.DeclareLocal(InterceptorValuesType);

                generator.Emit(OpCodes.Ldc_I4, parameters.Length);
                generator.Emit(OpCodes.Newarr, typeof(object));
                generator.Emit(OpCodes.Stloc_0);
                for (int i = 0; i < parameters.Length; i++)
                {
                    generator.Emit(OpCodes.Ldloc_0); //items.
                    generator.Emit(OpCodes.Ldc_I4, i);
                    generator.Emit(OpCodes.Ldarg, i + 1); // method arg by index i + 1
                    generator.Emit(OpCodes.Stelem_Ref); // items[x] = X;
                }
                if (taskMethod == null)
                {
                    //Task.FromResult<object>(null);
                    generator.Emit(OpCodes.Ldnull);
                    generator.Emit(OpCodes.Call, EmptyTaskCall);
                }
                else
                {
                    // new Task<object>([proxyMethod], items);
                    generator.Emit(OpCodes.Ldarg_0); //this
                    generator.Emit(OpCodes.Ldftn, taskMethod);
                    generator.Emit(OpCodes.Newobj, AnonymousFuncForTask);
                    generator.Emit(OpCodes.Ldloc_0); // load items
                    generator.Emit(OpCodes.Newobj, TaskWithResult);
                }
                //task = {see above}
                generator.Emit(OpCodes.Stloc_1, taskOfObject);

                //interceptorValues = new InterceptorValues(this, "[MethodName]", items, task);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldstr, method.Name);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldloc_1);
                generator.Emit(OpCodes.Newobj, InterceptorValuesConstructor);
                generator.Emit(OpCodes.Stloc_2, interceptorValues);

                if (method.ReturnType != typeof(void))
                {
                    generator.Emit(OpCodes.Ldarg_0);
                    generator.Emit(OpCodes.Ldfld, interceptorInvoker);
                    generator.Emit(OpCodes.Ldloc_2);
                    generator.Emit(OpCodes.Callvirt, InvokeInterceptor);
                    OpCode casting = method.ReturnType.GetTypeInfo().IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass;
                    generator.Emit(casting, method.ReturnType);
                }
                generator.Emit(OpCodes.Ret);
                methodCounter++;
            }
        }
    }
}
