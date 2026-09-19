# Built-in priority and optional references

Both NET10 executables must be published as Native AOT. Installation requires no .NET 10 SDK/runtime; NET35/NET48 remain AVEVA-loaded Framework hosts. AOT is a build requirement, not an instruction to compile on the user's machine. Host/profile prerequisites still apply.

The project Agent maintains docs/builtin/*.md against the packaged PML source. Each file is a complete usage unit embedded at publish time into both servers. No automatic chunking or SQLite rebuild may alter these guides. Update source docs and republish to change the runtime guide; do not edit generated indices as the source of truth.

get_builtin_usage works on the execution MCP without the Knowledge server. search_knowledge_layers returns these guides first; includeSupplemental opts into reference results. No local database is needed to discover the built-in methods. A custom method replaces a built-in only by explicit user choice, never by retrieval ranking. This priority does not override errors, function trust warnings or user instructions.

Optional official/custom PMLLIB/PMLUI/WebHelp use the existing deterministic syntax/heading chunkers and FTS5. They supplement implementation and version research. Product/version should be reflected in the source name. project.sqlite3 remains a disposable package-source index; register_knowledge_source(role=project) now maps to custom-<name> for compatibility. All databases are preserved without rebuilding by updates; --refresh-project remains available for explicitly requested package-source indexing. Direct search_knowledge supports explicitly selected reference databases.

No changes to PML implementations, EVAR or NET35/NET48 Host contracts are needed for this policy.
