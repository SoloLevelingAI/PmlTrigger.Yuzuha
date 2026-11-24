using Core30.Core;
using Core30.Knowledge;

return CoreCommandLine.Run(args, [new("knowledge", "知识库", "资料", static () => KnowledgeFeatures.All())],
    name: "AVEVA 工具箱 · 知识库");
