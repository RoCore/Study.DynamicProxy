using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FastProxy.Test
{
    [TestClass]
    public class UnitTest1
    {
        public interface Test1
        {
            void Test(string t, object value, object va, object sd, object sdsd, object asda);
            void TestX();
            void TestY();
        }

        public abstract class Test2 : Test1
        {
            private readonly Interceptor _proxyInterceptor;

            public Test2()
            {
                _proxyInterceptor = new Interceptor();
            }

            public abstract void TestX();
            public void TestY()
            {
                throw new NotImplementedException();
            }

            public virtual void Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                var list = new List<object>();
                list.Add(t);
                list.Add(value);
                list.Add(va);
                list.Add(sd);
                list.Add(sdsd);
                list.Add(asda);
                _proxyInterceptor.InterceptorInvokeAsync(new InterceptorValues(this, "Test", list));
            }
        }

        public class Test3 : Test2
        {
            public override void Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                base.Test(t, value, va, sd, sdsd, asda);
            }
            public override void TestX()
            {
                throw new NotImplementedException();
            }
        }

        public class Interceptor : IInterceptor
        {
            public object InterceptorInvokeAsync(InterceptorValues values)
            {
                return null;
            }
        }

        [DataRow(1)]
        [DataRow(1000)]
        [DataRow(100000)]
        [DataRow(10000000)]
        [TestMethod]
        public void CreateProxy(int value)
        {
            var watch = new Stopwatch();
            watch.Reset();
            while (value-- > 0)
            {
                watch.Start();
                var result = DynamicTypeBuilder.Build<Test1, Interceptor>();
                result.Test("sds", null, 1, 2, 3, 4);
                //var result2 = DynamicTypeBuilder.Build<Test2, Interceptor>();
                watch.Stop();
                Assert.IsNotNull(result);
                //Assert.IsNotNull(result2);
            }
        }
    }
}
