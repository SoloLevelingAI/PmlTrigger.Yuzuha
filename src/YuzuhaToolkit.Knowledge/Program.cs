using YuzuhaToolkit.Mcp;

// Knowledge commands use the same native CLI. No MCP server is started.
return await NativeCliHost.RunAsync(args, legacyMcp: true, additional: args.SequenceEqual(new[] { "--manager-protocol" }) ? null : LaunchOperationCatalog.Registrations());
