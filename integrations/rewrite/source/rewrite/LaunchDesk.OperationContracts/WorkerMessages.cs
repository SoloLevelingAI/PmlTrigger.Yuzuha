namespace LaunchDesk.OperationContracts;

/// <summary>父进程与 Worker 交换的有限协议消息。</summary>
/// <param name="Kind">定义、入口、来源或操作的有限类别</param>
/// <param name="Nonce">本次执行端握手的随机 nonce</param>
/// <param name="Envelope">首次 Worker 握手传输的冻结请求与父进程身份。</param>
/// <param name="Fact">当前单步执行准备报告的脱敏事实。</param>
/// <param name="Receipt">Worker 或单次交付的有限执行回执。</param>
/// <param name="Error">失败时的脱敏错误</param>
/// <param name="Snapshot"><include file="../Comments.xml" path="doc/Snapshot/*"/></param>
public sealed record WorkerMessage(
    string Kind,
    string Nonce,
    WorkerEnvelope? Envelope = null,
    OperationFact? Fact = null,
    WorkerReceipt? Receipt = null,
    Error? Error = null,
    RunSnapshot? Snapshot = null);