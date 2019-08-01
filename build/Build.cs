using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.NUnit;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.GitHub.ChangeLogExtensions;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
  public static int Main() => Execute<Build>(x => x.Compile);

  [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
  readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

  [Solution] readonly Solution Solution;
  [GitRepository] readonly GitRepository GitRepository;
  [GitVersion] readonly GitVersion GitVersion;

  AbsolutePath SourceDirectory => RootDirectory / "source";
  AbsolutePath TestsDirectory => RootDirectory / "tests";
  AbsolutePath OutputDirectory => RootDirectory / "output";
  AbsolutePath TestResultDirectory => OutputDirectory / "test-result";
  AbsolutePath PackDirectory => OutputDirectory / "dist";

  string ChangeLogFile => RootDirectory / "CHANGELOG.md";

  Target Clean => _ => _
      .Before(Restore)
      .Executes(() =>
      {
        SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
        TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
        EnsureCleanDirectory(OutputDirectory);
      });

  Target CleanTestResult => _ => _
      .Executes(() =>
      {
        OutputDirectory.GlobDirectories("**/test-result").ForEach(DeleteFile);
      });

  Target Restore => _ => _
      .Executes(() =>
      {
        DotNetRestore(s => s
              .SetProjectFile(Solution));
      });

  Target Compile => _ => _
      .DependsOn(Restore)
      .Executes(() =>
      {
        DotNetBuild(s => s
              .SetProjectFile(Solution)
              .SetConfiguration(Configuration)
              .SetAssemblyVersion(GitVersion.GetNormalizedAssemblyVersion())
              .SetFileVersion(GitVersion.GetNormalizedFileVersion())
              .SetInformationalVersion(GitVersion.InformationalVersion)
              .EnableNoRestore());
      });

  Target Pack => _ => _
      .DependsOn(Compile)
      .Executes(() =>
      {
        var changeLog = GetCompleteChangeLog(ChangeLogFile)
          .EscapeStringPropertyForMsBuild();

        DotNetPack(x => x
              .SetConfiguration(Configuration)
              .SetPackageReleaseNotes(changeLog)
              .EnableNoBuild()
              .SetOutputDirectory(PackDirectory)
              .SetVersion(GitVersion.NuGetVersion));
      });

  Target Test => _ => _
    .DependsOn(Compile)
    .DependsOn(CleanTestResult)
    .Executes(() =>
    {
      var testProjects = GlobFiles(RootDirectory / "tests", "**/*.csproj");
      var testRun = 1;
      foreach (var testProject in testProjects)
      {
        var projectDirectory = Path.GetDirectoryName(testProject);
        var current = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        DotNetTest(x => x
                .SetNoBuild(true)
                .SetProjectFile(testProject)
                .SetTestAdapterPath(".")
                .SetResultsDirectory(TestResultDirectory)
                .SetLogger($"trx;LogFileName={$"test_{testRun++}_{current}.xml"}"));
      }
    });
}
