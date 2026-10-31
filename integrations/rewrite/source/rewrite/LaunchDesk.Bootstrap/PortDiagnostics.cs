using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace LaunchDesk.Bootstrap;

/// <summary>可选契约诊断：仅记名称和次数，不记参数及返回数据。</summary>
public sealed class PortDiagnostics
{
    private readonly ConcurrentDictionary<string, long> calls = new();
    private readonly ConcurrentDictionary<string, long> implementationCalls = new();
    private readonly ConcurrentDictionary<string, PortBinding> bindings = new();

    /// <summary>为接口装配可选的有限计数代理，转发时保留原调用结果与异常。</summary>
    /// <include file="../Comments.xml" path="doc/T/*"/>
    /// <param name="implementation">具体实现的类型名称</param>
    /// <returns>实现相同接口的转发代理；只记录类型、方法名称和调用次数。</returns>
    public T Bind<T>(T implementation) where T : class
    {
        var contract = typeof(T);
        var map = implementation.GetType().GetInterfaceMap(contract);
        bindings[contract.Name + "/" + implementation.GetType().FullName] = new(contract.Name, implementation.GetType().FullName!,
            map.InterfaceMethods.Select((method, index) => new PortMethod(method.Name,
                map.TargetMethods[index].DeclaringType!.FullName + "." + map.TargetMethods[index].Name,
                method.GetParameters().Select(p => p.ParameterType.Name).ToArray())).ToArray());
        var proxy = DispatchProxy.Create<T, ContractProxy>();
        ((ContractProxy)(object)proxy).Configure(implementation, method =>
        {
            calls.AddOrUpdate(contract.Name + "." + method, 1, (_, count) => count + 1);
            implementationCalls.AddOrUpdate(contract.Name + "/" + implementation.GetType().FullName + "." + method, 1, (_, count) => count + 1);
        });
        return proxy;
    }

    /// <summary>读取指定接口方法的累计调用次数，不读取调用参数。</summary>
    /// <param name="method">用于有限诊断记录的方法名称。</param>
    /// <returns>指定接口方法的累计次数，尚无调用时为 0。</returns>
    public long Count(string method) => calls.GetValueOrDefault(method);
    /// <summary>返回按名称排序的接口装配与逐实现调用次数，不捕获参数或返回数据。</summary>
    /// <returns>含接口装配、方法调用和具体实现调用计数的独立投影。</returns>
    public object Snapshot() => new
    {
        bindings = bindings.Values.OrderBy(binding => binding.Contract).ToArray(),
        calls = calls.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value),
        implementationCalls = implementationCalls.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value)
    };

    /// <summary>一个接口到具体实现的反射装配信息。</summary>
    /// <param name="Contract">接口类型名称</param>
    /// <param name="Implementation">具体实现的类型名称</param>
    /// <param name="Methods">接口方法到实现的映射</param>
    public sealed record PortBinding(string Contract, string Implementation, PortMethod[] Methods);
    /// <summary>接口方法、实现目标及参数类型名称。</summary>
    /// <param name="Name">显示名或配置名</param>
    /// <param name="Target">探测目标或配置目标路径</param>
    /// <param name="ParameterTypes">参数类型名称，不含参数值</param>
    public sealed record PortMethod(string Name, string Target, string[] ParameterTypes);

    /// <summary>在转发接口调用时记录不含参数与返回数据的诊断计数。</summary>
    public class ContractProxy : DispatchProxy
    {
        private object target = null!;
        private Action<string> record = null!;
        internal void Configure(object target, Action<string> record) { this.target = target; this.record = record; }
        /// <summary>记录不含参数的调用计数并转发到真实实现，重新抛出真实实现的原始异常。</summary>
        /// <param name="method">代理当前调用的接口方法；空值会被拒绝。</param>
        /// <param name="arguments">仅传递给真实实现的原始参数，不进入诊断记录。</param>
        /// <returns>真实实现的原始返回值，包括其异步任务对象。</returns>
        protected override object? Invoke(MethodInfo? method, object?[]? arguments)
        {
            if (method is null) throw new InvalidOperationException("Missing contract method.");
            record(method.Name);
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
    }
}
