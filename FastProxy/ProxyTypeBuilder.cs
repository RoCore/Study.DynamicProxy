using System;
using System.Reflection;
using System.Reflection.Emit;
using FastProxy.Definitions;
using FastProxy.Helpers;

namespace FastProxy
{
    public sealed class ProxyTypeBuilder : IProxyTypeBuilder
    {
        private const string ProxyPrefix = "Proxy";
        private const string DecoratorName = "_decorator";
        private const string ProxyInvoker = "_proxyInvoker";

        public ProxyTypeBuilderTransientParameters Create(Type abstractType, Type concreteType, Type interceptorType, ModuleBuilder moduleBuilder, string postfix)
        {
            var result = new ProxyTypeBuilderTransientParameters();
            if (abstractType == null)
            {
                throw new MissingConstructionInformation(nameof(abstractType), MissingConstructionInformation.TypeDefintion.AbstractType);
            }
            if (concreteType == null)
            {
                throw new MissingConstructionInformation(nameof(concreteType), MissingConstructionInformation.TypeDefintion.ConcreteType);
            }
            if (interceptorType == null)
            {
                throw new MissingConstructionInformation(nameof(interceptorType), MissingConstructionInformation.TypeDefintion.InterceptorType);
            }
            if (moduleBuilder == null)
            {
                throw new MissingConstructionInformation(nameof(moduleBuilder), MissingConstructionInformation.TypeDefintion.ModuleBuilder);
            }

            var typeInfoImplemented = concreteType.GetTypeInfo();
            result.IsInterfaceType = typeInfoImplemented.IsInterface;
            var defaultConstructorImplemented = false;
            if (typeInfoImplemented.IsClass && typeInfoImplemented.IsSealed && abstractType == concreteType)
            {
                throw new InvalidOperationException("Not possible to create a proxy if base type is sealed");
            }
            
            if (typeInfoImplemented.IsClass)
            {
                //TODO: not only empty constructors
                if (typeInfoImplemented.IsSealed)
                {
                    CreateSealedType(abstractType, concreteType, interceptorType, moduleBuilder, result, postfix);
                    defaultConstructorImplemented = true;
                }
                else
                {
                    result.ProxyType = moduleBuilder.DefineType(string.Concat(ProxyPrefix, abstractType.Name, postfix), TypeAttributes.Public | TypeAttributes.Class, concreteType);
                    result.ProxyType.SetParent(abstractType);
                }
                result.Methods = abstractType.GetMethods(BindingFlags.Instance);
                result.Properties = abstractType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            }
            else if (typeInfoImplemented.IsInterface)
            {
                result.ProxyType = moduleBuilder.DefineType(string.Concat(ProxyPrefix, abstractType.Name, postfix), TypeAttributes.Public | TypeAttributes.Class);
                result.ProxyType.AddInterfaceImplementation(abstractType);
                result.Methods = abstractType.GetMethods();
                result.Properties = abstractType.GetProperties();
            }
            else
            {
                //TODO struct
                throw new NotImplementedException();
            }

            if (defaultConstructorImplemented == false)
            {
                var constructor = result.ProxyType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                var ilGenerator = constructor.GetILGenerator();
                CreateProxyInvokerInConstuctor(interceptorType, ilGenerator, result);
                ilGenerator.Emit(OpCodes.Ret);
            }
            return result;
        }

        private static void CreateSealedType(Type abstractType, Type concreteType, Type interceptorType, ModuleBuilder moduleBuilder, ProxyTypeBuilderTransientParameters result, string postfix)
        {
            result.ProxyType = moduleBuilder.DefineType(string.Concat(ProxyPrefix, abstractType.Name, postfix), TypeAttributes.Public | TypeAttributes.Class, abstractType);
            result.Decorator = result.ProxyType.DefineField(DecoratorName, abstractType, FieldAttributes.InitOnly);

            var constructor = result.ProxyType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var ilGenerator = constructor.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Newobj, concreteType.GetConstructor(Type.EmptyTypes));
            ilGenerator.Emit(OpCodes.Stfld, result.Decorator);
            CreateProxyInvokerInConstuctor(interceptorType, ilGenerator, result);
            ilGenerator.Emit(OpCodes.Ret);

            //CreateConstructorWithDecoratorAsParameter(abstractType, interceptorType, result);
        }

        private static void CreateConstructorWithDecoratorAsParameter(Type abstractType, Type interceptorType, ProxyTypeBuilderTransientParameters result)
        {
            var constructorWithDecoratorAsParameter = result.ProxyType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { abstractType });
            var ilGenerator = constructorWithDecoratorAsParameter.GetILGenerator();

            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, result.Decorator);
            CreateProxyInvokerInConstuctor(interceptorType, ilGenerator, result);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void CreateProxyInvokerInConstuctor(Type interceptorType, ILGenerator generator, ProxyTypeBuilderTransientParameters result)
        {
            if (result.InterceptorInvoker == null)
            {
                result.InterceptorInvoker = result.ProxyType.DefineField(ProxyInvoker, typeof(IInterceptor), FieldAttributes.InitOnly);
            }
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Newobj, interceptorType.GetConstructor(Type.EmptyTypes));
            generator.Emit(OpCodes.Stfld, result.InterceptorInvoker);
        }
    }
}
