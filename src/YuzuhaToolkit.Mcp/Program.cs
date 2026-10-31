using YuzuhaToolkit.Mcp;

// Compatibility filename; the standalone MCP transport is disabled.
return await NativeCliHost.RunAsync(args, legacyMcp: true, additional: args.SequenceEqual(new[] { "--manager-protocol" }) ? null : LaunchOperationCatalog.Registrations());
