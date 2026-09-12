using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace VoidFall.Tests.PlayMode
{
    internal static class RuntimeTestReflection
    {
        public static object Invoke(object target, string name, params object[] arguments)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo selected = null;
            foreach (var method in target.GetType().GetMethods(flags))
            {
                if (method.Name != name) continue;
                var parameters = method.GetParameters(); if (parameters.Length < arguments.Length) continue;
                var matches = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i >= arguments.Length) { if (!parameters[i].IsOptional) matches = false; continue; }
                    var type = parameters[i].ParameterType; var nullable = Nullable.GetUnderlyingType(type);
                    if (arguments[i] == null) { if (type.IsValueType && nullable == null) matches = false; }
                    else if (!type.IsInstanceOfType(arguments[i]) && !(nullable?.IsInstanceOfType(arguments[i]) ?? false)) matches = false;
                }
                if (!matches) continue;
                if (selected == null || parameters.Length < selected.GetParameters().Length) selected = method;
            }
            if (selected == null) throw new MissingMethodException(target.GetType().Name, name);
            var expanded = new object[selected.GetParameters().Length]; Array.Copy(arguments, expanded, arguments.Length);
            for (var i = arguments.Length; i < expanded.Length; i++) expanded[i] = Type.Missing;
            try { return selected.Invoke(target, expanded); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw(); throw; }
        }
    }
}
