using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using FastProxy.Definitions;
using FastProxy.Helpers;
using static FastProxy.Extensions;

namespace FastProxy
{
    public sealed class ProxyMethodBuilder : IProxyMethodBuilder
    {
        #region statics
        private static MethodInfo EmptyTaskCall { get; } = typeof(Task).GetMethod(nameof(Task.FromResult), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object));
        private static ConstructorInfo TaskWithResult { get; } = typeof(Task<object>).GetConstructor(new[] { typeof(Func<object, object>), typeof(object) });
        private static ConstructorInfo AnonymousFuncForTask { get; } = typeof(Func<object, object>).GetConstructor(new[] { typeof(object), typeof(IntPtr) });

        private static Type InterceptorValuesType { get; } = typeof(InterceptorValues);
        private static ConstructorInfo InterceptorValuesConstructor { get; } = typeof(InterceptorValues).GetConstructor(new[] { typeof(object), typeof(object), typeof(string), typeof(object[]), typeof(Task<object>) });
        private static MethodInfo InvokeInterceptor { get; } = typeof(IInterceptor).GetMethod(nameof(IInterceptor.Invoke));
        #endregion

        public void Create(MethodInfo methodInfo, ProxyMethodBuilderTransientParameters transient)
        {
            MethodAttributes attributes = methodInfo.Attributes;
            if (transient.TypeBuilder.IsInterface)
            {
                attributes = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
            }
            else if (methodInfo.IsAbstract)
            {
                attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
            }
            var parameters = methodInfo.GetParameters();
            var method = transient.TypeBuilder.DefineMethod(methodInfo.Name, attributes, methodInfo.CallingConvention, methodInfo.ReturnType, parameters.Select(a => a.ParameterType).ToArray());
            MethodBuilder taskMethod = null;
            if ((methodInfo.IsAbstract || transient.TypeBuilder.IsInterface) == false)
            {
                taskMethod = CreateBaseCallForTask(method, parameters, transient);
            }
            if (methodInfo.ContainsGenericParameters)
            {
                //TODO
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

            foreach (var item in transient.PreInit)
            {
                item.Execute(transient.TypeBuilder, transient.SealedTypeDecorator, transient.InterceptorDecorator, methodInfo, transient.PreInvoke, transient.PostInvoke);
            }

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

            foreach (var item in transient.PreInvoke)
            {
                item.Execute(transient.TypeBuilder, transient.SealedTypeDecorator, transient.InterceptorDecorator, methodInfo, transient.PostInvoke);
            }

            //interceptorValues = new InterceptorValues(this, [null|decorator], "[MethodName]", items, task);
            generator.Emit(OpCodes.Ldarg_0);
            if (transient.InterceptorDecorator != null)
            {
                generator.Emit(OpCodes.Ldfld, transient.InterceptorDecorator);
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
                generator.Emit(OpCodes.Ldfld, transient.InterceptorDecorator);
                generator.Emit(OpCodes.Ldloc_2);
                generator.Emit(OpCodes.Callvirt, InvokeInterceptor);
                OpCode casting = method.ReturnType.GetTypeInfo().IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass;
                generator.Emit(casting, method.ReturnType);
            }

            foreach (var item in transient.PostInvoke)
            {
                item.Execute(transient.TypeBuilder, transient.SealedTypeDecorator, transient.InterceptorDecorator, methodInfo);
            }

            generator.Emit(OpCodes.Ret);
            transient.MethodCreationCounter++;
        }

        private MethodBuilder CreateBaseCallForTask(MethodInfo baseMethod, ParameterInfo[] parameters, ProxyMethodBuilderTransientParameters transient)
        {
            var result = transient.TypeBuilder.DefineMethod(string.Concat(baseMethod.Name, "_Execute_", transient.MethodCreationCounter), MethodAttributes.Private | MethodAttributes.HideBySig, typeof(object), new[] { typeof(object) });

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
            if (transient.SealedTypeDecorator != null)
            {
                methodCall = OpCodes.Callvirt;
                generator.Emit(OpCodes.Ldfld, transient.SealedTypeDecorator); //_decorator
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
    }
}
