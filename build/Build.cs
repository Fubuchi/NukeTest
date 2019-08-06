using System;
using System.Linq;
using System.IO;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.NUnit;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.ReportGenerator;
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

  Target CleanAll => _ => _
      .DependsOn(Clean)
      .DependsOn(CleanTestResult)
      .Executes(() => { });

  Target Clean => _ => _
      .Before(Restore)
      .Executes(() =>
      {
        SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
        TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
      });

  Target CleanTestResult => _ => _
      .Executes(() =>
      {
        TestResultDirectory.GlobFiles("*").ForEach(DeleteFile);
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
      var testProjects = GlobFiles(TestsDirectory, "**/*.csproj");
      testProjects
        .NotEmpty()
        .ForEach(testProject =>
        {
          var projectDirectory = Path.GetDirectoryName(testProject);
          var projectName = Path.GetFileNameWithoutExtension(testProject);
          var current = DateTime.Now.ToString("yyyyMMddHHmmssfff");
          DotNetTest(x => x
            .SetNoBuild(true)
            .SetProjectFile(testProject)
            .SetTestAdapterPath(".")
            .SetResultsDirectory(TestResultDirectory)
            .SetLogger($"trx;LogFileName={$"test_{projectName}_{current}.xml"}"));
        });
    });

  Target Coverage => _ => _
    .DependsOn(Compile)
    .DependsOn(CleanTestResult)
    .Executes(() =>
    {
      var testProjects = GlobFiles(TestsDirectory, "**/*.csproj").ToList();
      var dotnetPath = ToolPathResolver.GetPathExecutable("dotnet");
      testProjects
        .NotEmpty()
        .ForEach(testProject =>
        {
          var projectDirectory = Path.GetDirectoryName(testProject);
          var projectName = Path.GetFileNameWithoutExtension(testProject);
          var dllPath = GlobFiles(projectDirectory, $"**/*/{projectName}.dll").First();
          var current = DateTime.Now.ToString("yyyyMMddHHmmssfff");

          CoverletTasks.Coverlet(s => s
            .SetAssembly(dllPath)
            .SetTarget(dotnetPath)
            .SetTargetArgs(new[]{
              "test",
              projectDirectory,
              "--no-build"
            })
            .SetOutput(TestResultDirectory / $"{projectName}_{current}.xml")
            .SetFormat(CoverletOutputFormat.cobertura));
        });
      ReportGeneratorTasks.ReportGenerator(s => s
            .SetTargetDirectory(TestResultDirectory)
            .SetReports(TestResultDirectory / "*.xml"));
    });


}
