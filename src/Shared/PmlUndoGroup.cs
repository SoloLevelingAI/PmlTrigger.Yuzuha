#nullable disable
using System;

namespace YuzuhaToolkit.Pml
{
    /// <summary>同一 Host 主线程调用中的撤销分组回执；不表示数据库已保存或已回滚。</summary>
    public sealed class PmlUndoGroupResult<T>
    {
        /// <summary>本次分组的唯一编号。</summary>
        public string Id { get; set; }
        /// <summary>MARKDB 已明确成功。</summary>
        public bool Started { get; set; }
        /// <summary>用户代码已经进入执行。</summary>
        public bool BodyStarted { get; set; }
        /// <summary>ENDMARKDB 已明确成功。</summary>
        public bool Ended { get; set; }
        /// <summary>用户代码返回的结果。</summary>
        public T Value { get; set; }
        /// <summary>分组建立、用户代码或分组结束的错误。</summary>
        public string Error { get; set; }
    }

    /// <summary>兼容 NET35 的单次撤销分组；建立失败不执行正文，任何阶段均不重试。</summary>
    public static class PmlUndoGroup
    {
        /// <summary>依次建立 MARKDB、执行正文、结束分组；保留正文和结束失败信息。</summary>
        public static PmlUndoGroupResult<T> Run<T>(Func<string, bool> command, Func<T> body)
        {
            PmlUndoGroupResult<T> result = new PmlUndoGroupResult<T>();
            result.Id = Guid.NewGuid().ToString("N");
            try
            {
                result.Started = command("MARKDB 'Yuzuha " + result.Id + "'");
                if (!result.Started) { result.Error = "MARKDB_FAILED: user code was not dispatched."; return result; }
                try { result.BodyStarted = true; result.Value = body(); }
                catch (Exception error) { result.Error = "PML_BODY_EXCEPTION: " + error.Message; }
                finally
                {
                    try
                    {
                        result.Ended = command("ENDMARKDB");
                        if (!result.Ended) result.Error = (result.Error ?? "") + " ENDMARKDB_FAILED: inspect the host before further execution.";
                    }
                    catch (Exception error) { result.Error = (result.Error ?? "") + " ENDMARKDB_EXCEPTION: " + error.Message; }
                }
            }
            catch (Exception error) { result.Error = "MARKDB_EXCEPTION: " + error.Message + "; user code was not dispatched."; }
            return result;
        }
    }
}
