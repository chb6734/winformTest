using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using E2ETest.Core.Api;

namespace E2ETest.Scripting
{
    /// <summary>테스트 어셈블리(.dll)를 로드하여 [E2ETest] 속성이 붙은 메서드를 실행.</summary>
    public sealed class AssemblyTestLoader
    {
        public List<TestMethod> Discover(string assemblyPath)
        {
            var asm = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
            var result = new List<TestMethod>();
            foreach (var type in asm.GetTypes())
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var attr = m.GetCustomAttributes(false).FirstOrDefault(a => a.GetType().Name == "E2ETestAttribute");
                    if (attr != null) result.Add(new TestMethod(type, m, attr));
                }
            }
            return result;
        }

        public void Run(TestMethod test, IApp app)
        {
            object instance = test.Method.IsStatic ? null : Activator.CreateInstance(test.DeclaringType);
            var parms = test.Method.GetParameters();
            if (parms.Length == 0) { test.Method.Invoke(instance, null); return; }
            if (parms.Length == 1 && typeof(IApp).IsAssignableFrom(parms[0].ParameterType))
            {
                test.Method.Invoke(instance, new object[] { app });
                return;
            }
            throw new InvalidOperationException("test method signature not supported: " + test.Method.Name);
        }
    }

    public sealed class TestMethod
    {
        public Type DeclaringType { get; private set; }
        public MethodInfo Method { get; private set; }
        public object AttributeInstance { get; private set; }
        public string DisplayName { get { return DeclaringType.FullName + "." + Method.Name; } }

        public TestMethod(Type t, MethodInfo m, object attr)
        {
            DeclaringType = t; Method = m; AttributeInstance = attr;
        }
    }
}
