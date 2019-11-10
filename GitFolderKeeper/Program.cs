using GlobExpressions;
using LibGit2Sharp;
using Serilog;
using Serilog.Sinks.SystemConsole;
using Serilog.Configuration;
using System.Diagnostics;
using System.Linq;

namespace GitFolderKeeper
{
    class Program
    {
        // imagesharpdrawing repo saving set
        static string[] paths = new[] {
                "/.git*",
                "/.github/**/*",
                "/.editorconfig",
                "/*.sln",
                "/*.md",
                "/build.*",
                "/run-tests.ps1",
                "/LICENSE",
                "/tests/CodeCoverage/**/*",
                "/CodeCoverage.runsettings",
                "/codecov.yml",
                "/shared-infrastructure",
                "/src/ImageSharp.Drawing/**/*",
                "**/Directory.Build.props",
                "**/Directory.Build.targets",
                "/tests/ImageSharp.Tests/*.cs",
                "/tests/ImageSharp.Tests/*.csproj",
                "/tests/ImageSharp.Tests/Drawing/**/*",
            };
        static void Main(string[] args)
        {
            var Logger = new Serilog.LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            // list of pathprefixes to keep

            using (var repo = new Repository(@"C:\Source\temp\ImageSharp"))
            {
                var count = 0;
                var stopwatch = Stopwatch.StartNew();

                var branchToRewrite = repo.Branches["master"];
                var commits = branchToRewrite.Commits.ToArray();
                Logger.Information("Found {commits} commits", commits.Length);
                Logger.Information("Rewrite history started");

                var rewriter = new HistoryRewriter(repo, new[] { branchToRewrite }, new RewriteHistoryOptions
                {
                    OnError = (ex) => Logger.Error(ex, "Error rewriting history"),
                    OnSucceeding = () => Logger.Information("Succeeded rewiting history"),
                    PruneEmptyCommits = true,
                    CommitTreeRewriter = commit =>
                    {
                        Logger.Information("Commit {0}/{1}", ++count, commits.Length);
                        var treeDef = TreeDefinition.From(commit);
                        static void ProcessTree(TreeDefinition treeDef, Tree tree)
                        {
                            foreach (var treeEntry in tree)
                            {
                                switch (treeEntry.TargetType)
                                {
                                    case TreeEntryTargetType.GitLink:
                                    case TreeEntryTargetType.Blob:
                                        if (!paths.Any(c => Glob.IsMatch("/" + treeEntry.Path, c)))
                                        {
                                            treeDef.Remove(treeEntry.Path);
                                        }
                                        break;
                                    case TreeEntryTargetType.Tree:
                                        ProcessTree(treeDef, treeEntry.Target as Tree);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        ProcessTree(treeDef, commit.Tree);
                        return treeDef;
                    }
                });

                rewriter.Execute();
                Logger.Information("Rewrite history ended. Took {elapsed}", stopwatch.Elapsed);
                repo.Reset(ResetMode.Hard);
            }
        }
    }
}
