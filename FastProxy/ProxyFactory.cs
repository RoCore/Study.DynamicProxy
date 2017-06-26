using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using FastProxy.Definitions;
using FastProxy.Helpers;

namespace FastProxy
{
    /// <summary>
    /// Allow to generate new dynamic proxy and an concrete or abstract definition
    /// </summary>
    public class ProxyFactory
    {
        private const string DynamicAssemblyName = "FastProxyDynamicAssembly";
        private const string DynamicModuleName = "FastProxyDynamicModule";

        private static readonly ReaderWriterLockSlim BuildersSlimLock = new ReaderWriterLockSlim();
        private static ProxyFactory _default;

        public static ProxyFactory Default
        {
            get
            {
                LockExecuteRelease(CreateDefault);
                return _default;
            }
        }

        /// <summary>
        /// we have only one method builder that can contain complex subsequences
        /// </summary>
        private readonly IProxyMethodBuilder _methodBuilder;
        /// <summary>
        /// Proxy type constructor
        /// </summary>
        private readonly IProxyTypeBuilder _typeBuilder;

        private readonly List<IBeforeCollectInterceptorInformation> _preInit = new List<IBeforeCollectInterceptorInformation>();
        private readonly List<IBeforeInvokeInterceptor> _preInvoke = new List<IBeforeInvokeInterceptor>();
        private readonly List<IAfterInvokeInterceptor> _postInvoke = new List<IAfterInvokeInterceptor>();
        private readonly Dictionary<int, TypeInfo> _cache = new Dictionary<int, TypeInfo>();


        private static AssemblyBuilder _builder;
        private static ModuleBuilder _moduleBuilder;



        public ProxyFactory(IProxyTypeBuilder typeBuilder, IProxyMethodBuilder methodBuilder)
        {
            _methodBuilder = methodBuilder;
            _typeBuilder = typeBuilder;
        }

        private int GetUniquePostfix<TAbstract, TConcrete, TInterceptor>()
        {
            int result = typeof(TAbstract).FullName.GetHashCode() | typeof(TConcrete).FullName.GetHashCode() | typeof(TInterceptor).FullName.GetHashCode();

            void GenerateHashcode<T>(T item)
            {
                result |= item.GetHashCode();
            }
            _preInit.ForEach(GenerateHashcode);
            _preInvoke.ForEach(GenerateHashcode);
            _postInvoke.ForEach(GenerateHashcode);
            return result;
        }

        /// <summary>
        /// Create new Proxy Type (not instance)
        /// </summary>
        /// <typeparam name="TAbstract">Abstract or concrete definition of the proxy type</typeparam>
        /// <typeparam name="TInterceptor">Interceptor type</typeparam>
        /// <typeparam name="TConcrete">Concrete Type of the implementation</typeparam>
        /// <returns>Type of the new Proxy object</returns>
        public TypeInfo CreateProxy<TAbstract, TConcrete, TInterceptor>()
            where TInterceptor : IInterceptor, new()
        {
            TypeInfo result = null;
            void Create()
            {
                CreateBuilders();
                var abstractType = typeof(TAbstract);
                var concreteType = typeof(TConcrete);
                var interceptorType = typeof(TInterceptor);

                var uniquePostfix = GetUniquePostfix<TAbstract, TConcrete, TInterceptor>();
                if (_cache.TryGetValue(uniquePostfix, out result) == false)
                {
                    var type = _typeBuilder.Create(abstractType, concreteType, interceptorType, _moduleBuilder, $"_{uniquePostfix}");

                    ProxyMethodBuilderTransientParameters input = new ProxyMethodBuilderTransientParameters
                    {
                        TypeBuilder = type.ProxyType,
                        SealedTypeDecorator = type.Decorator,
                        InterceptorDecorator = type.InterceptorInvoker,
                        MethodCreationCounter = 0,
                        PreInit = new List<IBeforeCollectInterceptorInformation>(_preInit),
                        PreInvoke = new List<IBeforeInvokeInterceptor>(_preInvoke),
                        PostInvoke = new List<IAfterInvokeInterceptor>(_postInvoke)
                    };

                    foreach (var item in type.Methods)
                    {
                        _methodBuilder.Create(item, input);
                    }

                    result = type.ProxyType.CreateTypeInfo();
                    _cache.Add(uniquePostfix, result);
                }
            }

            LockExecuteRelease(Create);

            return result;
        }

        private static void LockExecuteRelease(Action exec)
        {
            try
            {
                BuildersSlimLock.EnterWriteLock();
                exec();
            }
            finally
            {
                BuildersSlimLock.ExitWriteLock();
            }
        }

        private static void CreateBuilders()
        {
            if (_builder == null)
            {
                AssemblyBuilderAccess access = AssemblyBuilderAccess.Run;
                _builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(DynamicAssemblyName), access);
                _moduleBuilder = _builder.DefineDynamicModule(DynamicModuleName);
            }
        }

        private static void CreateDefault()
        {
            if (_default == null)
            {
                _default = new ProxyFactory(new ProxyTypeBuilder(), new ProxyMethodBuilder());
            }
        }
    }
}
