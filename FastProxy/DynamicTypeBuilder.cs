using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using FastProxy.Definitions;
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
            var abstractType = typeof(TBase);
            var concreteType = typeof(T);
            var typeInfoImplemented = concreteType.GetTypeInfo();
            if (typeInfoImplemented.IsClass && typeInfoImplemented.IsSealed && abstractType == concreteType)
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
                    result = moduleBuilder.DefineType(string.Concat(ProxyPrefix, abstractType.Name), TypeAttributes.Public | TypeAttributes.Class, abstractType);
                    decorator = result.DefineField(DecoratorName, abstractType, FieldAttributes.InitOnly);

                    var constructor = result.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                    var ilGenerator = constructor.GetILGenerator();
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Newobj, abstractType.GetConstructor(Type.EmptyTypes));
                    ilGenerator.Emit(OpCodes.Stfld, decorator);
                    interceptorInvoker = result.CreateProxyInvokerInConstuctor<TInterceptor>(ilGenerator);
                    ilGenerator.Emit(OpCodes.Ret);
                    defaultConstructorImplemented = true;
                }
                else
                {
                    result = moduleBuilder.DefineType(string.Concat(ProxyPrefix, abstractType.Name), TypeAttributes.Public | TypeAttributes.Class, concreteType);
                    result.SetParent(abstractType);
                }
                methods = abstractType.GetMethods(BindingFlags.Instance);
                properties = abstractType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            }
            else if (typeInfoImplemented.IsInterface)
            {
                result = moduleBuilder.DefineType(string.Concat(ProxyPrefix, abstractType.Name), TypeAttributes.Public | TypeAttributes.Class);
                result.AddInterfaceImplementation(abstractType);
                methods = abstractType.GetMethods();
                properties = abstractType.GetProperties();
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
        private static ConstructorInfo InterceptorValuesConstructor { get; } = typeof(InterceptorValues).GetConstructor(new[] { typeof(object), typeof(object), typeof(string), typeof(object[]), typeof(Task<object>) });
        private static MethodInfo InvokeInterceptor { get; } = typeof(IInterceptor).GetMethod(nameof(IInterceptor.Invoke));


        public static MethodBuilder CreateBaseCallForTask(this TypeBuilder builder, MethodInfo baseMethod, long counter, ParameterInfo[] parameters, FieldBuilder decorator)
        {
            var result = builder.DefineMethod(string.Concat(baseMethod.Name, "_Execute_", counter), MethodAttributes.Private | MethodAttributes.HideBySig, typeof(object), new[] { typeof(object) });

            var generator = result.GetILGenerator();

            var items = generator.DeclareLocal(typeof(object[]));
            if (parameters.Length > 0)
            {
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Castclass, items.LocalType);
                generator.Emit(OpCodes.Stloc_0);
            }
            var methodCall = OpCodes.Call;
            generator.Emit(OpCodes.Ldarg_0); //base.
            if (decorator != null)
            {
                methodCall = OpCodes.Callvirt;
                generator.Emit(OpCodes.Ldfld, decorator); //_decorator
            }
            generator.Emit(OpCodes.Ldloc_0); //items

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
            generator.Emit(methodCall, baseMethod);
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
                    taskMethod = builder.CreateBaseCallForTask(method, methodCounter, parameters, decorator);
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
                generator.DeclareLocal(listType);
                //Task<object> task; {1}
                generator.DeclareLocal(typeof(Task<object>));
                //InterceptorValues interceptorValues; {2}
                generator.DeclareLocal(InterceptorValuesType);

                generator.Emit(OpCodes.Ldc_I4, parameters.Length);
                generator.Emit(OpCodes.Newarr, typeof(object));
                generator.Emit(OpCodes.Stloc_0);
                for (int i = 0; i < parameters.Length; i++)
                {
                    generator.Emit(OpCodes.Ldloc_0); //items.
                    generator.Emit(OpCodes.Ldc_I4, i);
                    generator.Emit(OpCodes.Ldarg, i + 1); // method arg by index i + 1 since ldarg_0 == this
                    generator.Emit(OpCodes.Stelem_Ref); // items[x] = X;
                }
                if (taskMethod == null)
                {
                    EmitDefaultValue(method.ReturnType, generator);
                    generator.Emit(OpCodes.Box, method.ReturnType);
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
                generator.Emit(OpCodes.Stloc_1);

                //interceptorValues = new InterceptorValues(this, [null|decorator], "[MethodName]", items, task);
                generator.Emit(OpCodes.Ldarg_0);
                if (decorator != null)
                {
                    generator.Emit(OpCodes.Ldfld, decorator);
                }
                else
                {
                    generator.Emit(OpCodes.Ldnull);
                }
                generator.Emit(OpCodes.Ldstr, method.Name);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldloc_1);
                generator.Emit(OpCodes.Newobj, InterceptorValuesConstructor);
                generator.Emit(OpCodes.Stloc_2);

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


        private static readonly HashSet<Type> IntegerInitializable = new HashSet<Type>
        {
            typeof(bool),
            typeof(byte),
            typeof(char),
            typeof(int),
            typeof(sbyte),
            typeof(short),
            typeof(uint),
            typeof(ushort)
        };

        private static readonly HashSet<Type> LongInitializable = new HashSet<Type>
        {
            typeof(long),
            typeof(ulong)
        };


        private static void EmitDefaultValue(Type type, ILGenerator generator)
        {
            if (IntegerInitializable.Contains(type) || type.GetTypeInfo().IsEnum)
            {
                generator.Emit(OpCodes.Ldc_I4_0);
            }
            else if (LongInitializable.Contains(type))
            {
                generator.Emit(OpCodes.Ldc_I8, 0L);
            }
            else if (type == typeof(float))
            {
                generator.Emit(OpCodes.Ldc_R4, 0f);
            }
            else if (type == typeof(double))
            {
                generator.Emit(OpCodes.Ldc_R8, 0D);
            }
            else if (type.GetTypeInfo().IsValueType)
            {
                //custom structs and other non nummeric
                generator.Emit(OpCodes.Initobj, type);
            }
            else
            {
                //class
                generator.Emit(OpCodes.Ldnull);
            }
        }
    }
}
