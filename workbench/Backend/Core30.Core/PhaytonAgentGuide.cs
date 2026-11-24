namespace Core30.Core;

public sealed record PhaytonAgentGuide(string Name, string Instructions, string Documentation)
{
    public static IReadOnlyList<PhaytonAgentGuide> Clients { get; } =
    [
        new("Codex", "在本机工作区调用 CLI。项目级 Skill 放在 .agents/skills/yuzuha-toolkit，个人级使用用户的 .agents/skills 目录。",
            "https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills"),
        new("WorkBuddy", "在桌面本地任务中调用 CLI。也可在「专家·技能·连接器」上传 Skill 包。命令执行按工作区权限确认。",
            "https://cloud.tencent.com/document/product/1831/134432"),
        new("Claude Code", "在本机项目中运行。项目级 Skill 放在 .claude/skills/yuzuha-toolkit，个人目录用 ~/.claude/skills/yuzuha-toolkit。",
            "https://code.claude.com/docs/en/skills"),
        new("DeepSeek Harness", "使用本机工作区。项目级 Skill 可放在 .dsh/skills 或 .agents/skills。旧版 Phayton Skill 若仍使用 MCP，请更新后再验证。",
            "https://github.com/deepseek-ai/deepseek-harness/blob/master/docs/subsystems/skills.md#local-discovery-priority")
    ];

    public static string ReadOnlyCommand(string executable)
    {
        var quoted = executable.Replace("'", "''", StringComparison.Ordinal);
        return $"$cli = '{quoted}'\n& $cli op list\n& $cli op invoke aveva.sessions.list";
    }
}
