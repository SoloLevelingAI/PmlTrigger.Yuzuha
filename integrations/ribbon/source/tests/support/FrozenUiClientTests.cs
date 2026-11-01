using System;
using System.IO;
using RibbonStudio;
public static class FrozenUiClientTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static int Main(string[] args)
    {
        try
        {
            using (var api = new ApiClient(args[0]))
            {
                var client = new FrozenDeploymentClient(api); var id = args[2];
                if (args[1] == "locked") { Check(client.Locked && client.UnknownPlanId == id, "pending did not survive process restart"); try { client.EnsureAvailable(); throw new Exception("pending lock bypassed"); } catch (Exception e) { Check(e.Message.Contains(id), "missing original identity"); } }
                else if (args[1] == "ack") { FrozenDeploymentClient.AcknowledgeManualCheck(); Check(!client.Locked, "manual acknowledgment failed"); }
                else if (args[1] == "ack-blocked") { try { FrozenDeploymentClient.AcknowledgeManualCheck(id); throw new Exception("active write unlocked"); } catch (Exception e) { Check(e.Message.Contains("仍在执行"), "active write acknowledgment was not blocked"); } Check(client.Locked, "pending deleted during active write"); }
                else
                {
                    var plan = new FrozenPlan { contractVersion = 2, planId = id, planDigest = new string('a', 64), kind = "file-update" };
                    if (args[1] == "success") { var result = client.Apply("file", plan).GetAwaiter().GetResult(); Check(result.activation.runtimeVerified == false, "loaded claim"); Check(!client.Locked, "completed write kept lock"); try { client.Apply("file", plan).GetAwaiter().GetResult(); throw new Exception("duplicate sent"); } catch (Exception e) { Check(e.Message.Contains("计划已消费"), "duplicate not blocked"); } }
                    else if (args[1] == "storage-failure") { try { client.Apply("file", plan).GetAwaiter().GetResult(); throw new Exception("storage failure sent write"); } catch (Exception e) { Check(e.Message.Contains("未发送写入请求"), "storage error unclear"); } }
                    else { try { if (args[1] == "legacy") client.ApplyLegacy(new { project = "fixture" }).GetAwaiter().GetResult(); else client.Apply("file", plan).GetAwaiter().GetResult(); throw new Exception("uncertain response accepted"); } catch (Exception e) { Check(e.Message.Contains("结果不确定"), e.ToString()); Check(client.Locked, "unknown not locked"); Console.WriteLine(client.UnknownPlanId); }
                        if (args[1] == "stale-local") {
                            const string newer = "00000000-0000-0000-0000-000000000002";
                            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("UIC_UI_STATE_DIR"), "pending-deployment.json"), Json.Write(new FrozenPlan { planId = newer }));
                            Check(client.UnknownPlanId == newer, "local identity masked persistent identity");
                            try { FrozenDeploymentClient.AcknowledgeManualCheck(id); throw new Exception("stale acknowledgment erased newer plan"); } catch (Exception e) { Check(e.Message.Contains("已变化"), "stale acknowledgment not rejected"); }
                            Check(client.UnknownPlanId == newer, "new pending lost"); FrozenDeploymentClient.AcknowledgeManualCheck(newer);
                        }
                    }
                }
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
