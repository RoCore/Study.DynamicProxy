using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
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
#else
        private static readonly ConcurrentDictionary<string, TypeBuilder> Cache = new ConcurrentDictionary<string, TypeBuilder>();
#endif
        private static TypeBuilder GetCacheValue<TBase, T, TInterceptor>(string key, ModuleBuilder builders)
            where TInterceptor : IInterceptor, new()
        {
#if (NETSTANDARD1_6)
            return MemoryCache.GetOrCreate(typeof(T).FullName, a =>
            {
                a.SlidingExpiration = TimeSpan.FromMinutes(10);
                return builders.CreateType<TBase, T, TInterceptor>();
            });
#elif(NETCLASSIC)
            return Cache.GetOrAdd(key, a => builders.CreateType<TBase, T, TInterceptor>());
#else
            throw new NotImplementedException();
#endif
        }

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

        private static void CreateMethods<TInterceptor>(this TypeBuilder builder, MethodInfo[] methods, FieldBuilder decorator, bool isInterfaceType, FieldBuilder interceptorInvoker)
            where TInterceptor : IInterceptor, new()
        {
            if (interceptorInvoker == null)
            {
                throw new ArgumentNullException(nameof(interceptorInvoker));
            }
            var interecptor = typeof(TInterceptor).GetMethod(nameof(IInterceptor.InterceptorInvokeAsync));

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
                if (item.ContainsGenericParameters)
                {
                    //TODO
                    //method.DefineGenericParameters
                    throw new NotImplementedException();
                }
                var generator = method.GetILGenerator();
                if (decorator != null)
                {

                }
                if (method.ReturnType == typeof(void))
                {
                    if (isInterfaceType)
                    {
                        var values = typeof(InterceptorValues);
                        var constructor = values.GetConstructor(new[] { typeof(object), typeof(string), typeof(IEnumerable) });
                        var listType = typeof(List<object>);
                        var addMethod = listType.GetMethod(nameof(List<object>.Add));
                        var list = generator.DeclareLocal(listType);
                        //var list = new List<object>();
                        generator.Emit(OpCodes.Newobj, listType.GetConstructor(Type.EmptyTypes));
                        generator.Emit(OpCodes.Stloc_0, list);
                        generator.Emit(OpCodes.Nop);
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            OpCode code;
                            switch (i)
                            {
                                case 0:
                                    code = OpCodes.Ldarg_0; break;
                                case 1:
                                    code = OpCodes.Ldarg_1; break;
                                case 2:
                                    code = OpCodes.Ldarg_2; break;
                                case 3:
                                    code = OpCodes.Ldarg_3; break;
                                default:
                                    code = OpCodes.Ldarg_S; break;
                            }
                            //list.Add(argumentX);
                            generator.Emit(OpCodes.Ldloc_0);
                            generator.Emit(OpCodes.Ldarg_S, i);
                            generator.Emit(OpCodes.Callvirt, addMethod);
                            generator.Emit(OpCodes.Nop);
                        }

                        //this._proxyInvoker(new InterceptorValues(this, "MethodName", list));
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldfld, interceptorInvoker);
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldstr, method.Name);
                        generator.Emit(OpCodes.Ldloc_0);
                        generator.Emit(OpCodes.Newobj, constructor);
                        generator.Emit(OpCodes.Callvirt, interecptor);
                        generator.Emit(OpCodes.Pop);
                        generator.Emit(OpCodes.Ret);
                    }
                }
                else
                {

                }
            }
        }
    }
}
