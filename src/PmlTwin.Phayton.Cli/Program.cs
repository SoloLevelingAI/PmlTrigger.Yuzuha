using YuzuhaToolkit.Mcp;

return await NativeCliHost.RunAsync(args, additional: args.SequenceEqual(new[] { "--manager-protocol" }) ? null : LaunchOperationCatalog.Registrations());
