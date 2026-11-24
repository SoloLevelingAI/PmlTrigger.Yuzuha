namespace Core30.PmlTwin;

/// <summary>Searches local paths with a directory budget and keeps coverage separate from product evidence.</summary>
internal sealed class AvevaEnvironmentScanner(IAvevaEnvironmentInventory inventory)
{
    public AvevaEnvironmentSnapshot Scan(EnvironmentScanOptions options)
    {
        var clues = inventory.Collect(options.Mode);
        var roots = options.ExtraRoots.Select(path => new ScanRoot(path, "custom")).Concat(clues.Roots);
        return new DirectorySurvey(options, roots, clues).Run();
    }

    private sealed class DirectorySurvey
    {
        private const int IssueLimit = 32;
        private readonly EnvironmentScanOptions _options;
        private readonly EnvironmentClues _clues;
        private readonly RootProgress[] _roots;
        private readonly Queue<SearchFolder> _pending = new();
        private readonly Dictionary<string, int> _visitedDepth = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AvevaInstallation> _installations = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ScanIssue> _issues;
        private int _visited;
        private int _unreadable;
        private int _links;
        private int _depthLimited;
        private bool _budgetReached;

        public DirectorySurvey(EnvironmentScanOptions options, IEnumerable<ScanRoot> roots, EnvironmentClues clues)
        {
            _options = options;
            _clues = clues;
            _roots = roots.DistinctBy(root => (root.Path.ToUpperInvariant(), root.Source)).Select(root => new RootProgress(root)).ToArray();
            _issues = clues.Issues.Take(IssueLimit).ToList();
        }

        public AvevaEnvironmentSnapshot Run()
        {
            foreach (var root in _roots)
                ScheduleRoot(root);
            WalkDirectories();
            var results = _installations.Values.OrderBy(item => item.Product).ThenBy(item => item.Directory).ToArray();
            var coverage = new ScanCoverage(_visited, _unreadable, _links, _depthLimited, _budgetReached,
                _clues.RegistryViews, _roots.Select(root => root.Describe()).ToArray(), _issues.ToArray());
            return new AvevaEnvironmentSnapshot(results, _clues.Processes, coverage);
        }

        private void ScheduleRoot(RootProgress root)
        {
            try
            {
                CheckRoot(root.Root.Path);
                root.State = "queued";
                _pending.Enqueue(new SearchFolder(root, root.Root.Path, _options.MaxDepth));
            }
            catch (Exception error) when (IsDirectoryError(error))
            {
                RecordRootProblem(root, error);
            }
        }

        private void WalkDirectories()
        {
            // Breadth-first traversal lets both fixed disks and the high-confidence roots contribute before the limit.
            while (_pending.TryDequeue(out var folder))
            {
                if (AlreadyCovered(folder))
                    continue;
                if (_visited >= _options.MaxDirectories)
                {
                    _budgetReached = true;
                    return;
                }
                InspectFolder(folder);
            }
        }

        private bool AlreadyCovered(SearchFolder folder)
        {
            if (!_visitedDepth.TryGetValue(folder.Path, out var depth) || depth < folder.RemainingDepth)
                return false;
            if (folder.Root.State == "queued")
                folder.Root.State = "covered-by-other-root";
            return true;
        }

        private void InspectFolder(SearchFolder folder)
        {
            _visitedDepth[folder.Path] = folder.RemainingDepth;
            _visited++;
            folder.Root.Visited++;
            try
            {
                var evidence = EnumerateFolder(folder);
                AddInstallation(folder, evidence);
                folder.Root.State = "visited";
            }
            catch (Exception error) when (IsDirectoryError(error))
            {
                _unreadable++;
                if (folder.Root.Root.Path == folder.Path)
                    folder.Root.State = "unreadable";
                AddIssue(folder.Root.Root.Source, folder.Path, error.GetType().Name);
            }
        }

        private Dictionary<string, string> EnumerateFolder(SearchFolder folder)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in new DirectoryInfo(folder.Path).EnumerateFileSystemInfos("*", SearchEntries))
                AddEntry(folder, item, files);
            return files;
        }

        private void AddEntry(SearchFolder parent, FileSystemInfo item, Dictionary<string, string> files)
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                _links++;
                return;
            }
            if (item is DirectoryInfo)
                ScheduleChild(parent, item.FullName);
            else if (AvevaInstallationEvidence.IsEvidenceFile(item.Name))
                files.TryAdd(item.Name, item.FullName);
        }

        private void ScheduleChild(SearchFolder parent, string child)
        {
            if (parent.RemainingDepth == 0)
            {
                _depthLimited++;
                return;
            }
            _pending.Enqueue(new SearchFolder(parent.Root, child, parent.RemainingDepth - 1));
        }

        private void AddInstallation(SearchFolder folder, Dictionary<string, string> files)
        {
            if (files.Count == 0 || _installations.ContainsKey(folder.Path))
                return;
            var installation = AvevaInstallationEvidence.Identify(folder.Path, files, folder.Root.Root);
            if (installation is not null)
                _installations.Add(folder.Path, installation);
        }

        private void RecordRootProblem(RootProgress root, Exception error)
        {
            root.State = error switch
            {
                FileNotFoundException or DirectoryNotFoundException => "missing",
                LinkedRootException => "reparse-skipped",
                _ => "unreadable"
            };
            if (root.State == "missing")
                return;
            if (root.State == "reparse-skipped")
                _links++;
            else
                _unreadable++;
            AddIssue(root.Root.Source, root.Root.Path, error.GetType().Name);
        }

        private void AddIssue(string source, string path, string reason)
        {
            if (_issues.Count < IssueLimit)
                _issues.Add(new ScanIssue(source, path, reason));
        }

        private static void CheckRoot(string path)
        {
            for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            {
                var attributes = File.GetAttributes(directory.FullName);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new LinkedRootException();
                if ((attributes & FileAttributes.Directory) == 0)
                    throw new DirectoryNotFoundException();
            }
        }

        private static bool IsDirectoryError(Exception error) => error is IOException or UnauthorizedAccessException
            or System.Security.SecurityException;

        private static readonly EnumerationOptions SearchEntries = new()
        {
            AttributesToSkip = 0, IgnoreInaccessible = false
        };

        private sealed class RootProgress(ScanRoot root)
        {
            public ScanRoot Root { get; } = root;
            public string State { get; set; } = "pending";
            public int Visited { get; set; }

            public RootCoverage Describe() => new(Root.Path, Root.Source, State, Visited);
        }

        private sealed record SearchFolder(RootProgress Root, string Path, int RemainingDepth);
        private sealed class LinkedRootException : IOException;
    }
}
