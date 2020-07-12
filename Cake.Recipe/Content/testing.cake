///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

BuildParameters.Tasks.InstallReportGeneratorTask = Task("Install-ReportGenerator")
    .Does(() => RequireTool(BuildParameters.IsDotNetCoreBuild ? ToolSettings.ReportGeneratorGlobalTool : ToolSettings.ReportGeneratorTool, () => {
    }));

BuildParameters.Tasks.InstallOpenCoverTask = Task("Install-OpenCover")
    .WithCriteria(() => BuildParameters.BuildAgentOperatingSystem == PlatformFamily.Windows, "Not running on windows")
    .Does(() => RequireTool(ToolSettings.OpenCoverTool, () => {
    }));

BuildParameters.Tasks.TestNUnitTask = Task("Test-NUnit")
    .IsDependentOn("Install-OpenCover")
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.PublishedNUnitTests), "No published NUnit tests")
    .Does(() => RequireTool(ToolSettings.NUnitTool, () => {
        EnsureDirectoryExists(BuildParameters.Paths.Directories.NUnitTestResults);

        if (BuildParameters.BuildAgentOperatingSystem == PlatformFamily.Windows)
        {
            OpenCover(tool => {
                tool.NUnit3(GetFiles(BuildParameters.Paths.Directories.PublishedNUnitTests + (BuildParameters.TestFilePattern ?? "/**/*Tests.dll")), new NUnit3Settings {
                    NoResults = true
                });
            },
            BuildParameters.Paths.Files.TestCoverageOutputFilePath,
            new OpenCoverSettings
            {
                OldStyle = true,
                ReturnTargetCodeOffset = 0
            }
                .WithFilter(ToolSettings.TestCoverageFilter)
                .ExcludeByAttribute(ToolSettings.TestCoverageExcludeByAttribute)
                .ExcludeByFile(ToolSettings.TestCoverageExcludeByFile));

            // TODO: Need to think about how to bring this out in a generic way for all Test Frameworks
            var settings = new ReportGeneratorSettings();
            if (BuildParameters.BuildAgentOperatingSystem != PlatformFamily.Windows)
            {
                // Workaround until 0.38.5+ version of cake is released
                // https://github.com/cake-build/cake/pull/2824
                settings.ToolPath = Context.Tools.Resolve("reportgenerator");
            }
            ReportGenerator(BuildParameters.Paths.Files.TestCoverageOutputFilePath, BuildParameters.Paths.Directories.TestCoverage, settings);
        }
    })
);

BuildParameters.Tasks.TestxUnitTask = Task("Test-xUnit")
    .IsDependentOn("Install-OpenCover")
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.PublishedxUnitTests), "No published xUnit tests")
    .Does(() => RequireTool(ToolSettings.XUnitTool, () => {
    EnsureDirectoryExists(BuildParameters.Paths.Directories.xUnitTestResults);

        if (BuildParameters.BuildAgentOperatingSystem == PlatformFamily.Windows)
        {
            OpenCover(tool => {
                tool.XUnit2(GetFiles(BuildParameters.Paths.Directories.PublishedxUnitTests + (BuildParameters.TestFilePattern ?? "/**/*Tests.dll")), new XUnit2Settings {
                    OutputDirectory = BuildParameters.Paths.Directories.xUnitTestResults,
                    XmlReport = true,
                    NoAppDomain = true
                });
            },
            BuildParameters.Paths.Files.TestCoverageOutputFilePath,
            new OpenCoverSettings
            {
                OldStyle = true,
                ReturnTargetCodeOffset = 0
            }
                .WithFilter(ToolSettings.TestCoverageFilter)
                .ExcludeByAttribute(ToolSettings.TestCoverageExcludeByAttribute)
                .ExcludeByFile(ToolSettings.TestCoverageExcludeByFile));

            // TODO: Need to think about how to bring this out in a generic way for all Test Frameworks
            var settings = new ReportGeneratorSettings();
            if (BuildParameters.BuildAgentOperatingSystem != PlatformFamily.Windows)
            {
                // Workaround until 0.38.5+ version of cake is released
                // https://github.com/cake-build/cake/pull/2824
                settings.ToolPath = Context.Tools.Resolve("reportgenerator");
            }
            ReportGenerator(BuildParameters.Paths.Files.TestCoverageOutputFilePath, BuildParameters.Paths.Directories.TestCoverage, settings);
        }
    })
);

BuildParameters.Tasks.TestMSTestTask = Task("Test-MSTest")
    .IsDependentOn("Install-OpenCover")
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.PublishedMSTestTests), "No published MSTest tests")
    .Does(() =>
{
    EnsureDirectoryExists(BuildParameters.Paths.Directories.MSTestTestResults);

    // TODO: Need to add OpenCover here
    MSTest(GetFiles(BuildParameters.Paths.Directories.PublishedMSTestTests + (BuildParameters.TestFilePattern ?? "/**/*Tests.dll")), new MSTestSettings() {
        NoIsolation = false
    });
});

BuildParameters.Tasks.TestVSTestTask = Task("Test-VSTest")
    .IsDependentOn("Install-OpenCover")
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.PublishedVSTestTests), "No published VSTest tests")
    .Does(() =>
{
    EnsureDirectoryExists(BuildParameters.Paths.Directories.VSTestTestResults);

    var vsTestSettings = new VSTestSettings()
    {
        InIsolation = true
    };

    if (AppVeyor.IsRunningOnAppVeyor)
    {
        vsTestSettings.WithAppVeyorLogger();
    }

    if (BuildParameters.BuildAgentOperatingSystem == PlatformFamily.Windows)
    {
        OpenCover(
            tool => { tool.VSTest(GetFiles(BuildParameters.Paths.Directories.PublishedVSTestTests + (BuildParameters.TestFilePattern ?? "/**/*Tests.dll")), vsTestSettings); },
            BuildParameters.Paths.Files.TestCoverageOutputFilePath,
            new OpenCoverSettings
            {
                OldStyle = true,
                ReturnTargetCodeOffset = 0
            }
                .WithFilter(ToolSettings.TestCoverageFilter)
                .ExcludeByAttribute(ToolSettings.TestCoverageExcludeByAttribute)
                .ExcludeByFile(ToolSettings.TestCoverageExcludeByFile));

        // TODO: Need to think about how to bring this out in a generic way for all Test Frameworks
            var settings = new ReportGeneratorSettings();
            if (BuildParameters.BuildAgentOperatingSystem != PlatformFamily.Windows)
            {
                // Workaround until 0.38.5+ version of cake is released
                // https://github.com/cake-build/cake/pull/2824
                settings.ToolPath = Context.Tools.Resolve("reportgenerator");
            }
            ReportGenerator(BuildParameters.Paths.Files.TestCoverageOutputFilePath, BuildParameters.Paths.Directories.TestCoverage, settings);
    }
});

BuildParameters.Tasks.DotNetCoreTestTask = Task("DotNetCore-Test")
    .IsDependentOn("Install-OpenCover")
    .Does<DotNetCoreMSBuildSettings>((context, msBuildSettings) => {

    var projects = GetFiles(BuildParameters.TestDirectoryPath + (BuildParameters.TestFilePattern ?? "/**/*Tests.csproj"));
    // We create the coverlet settings here so we don't have to create the filters several times
    var coverletSettings = new CoverletSettings
    {
        CollectCoverage         = true,
        // It is problematic to merge the reports into one, as such we use a custom directory for coverage results
        CoverletOutputDirectory = BuildParameters.Paths.Directories.TestCoverage.Combine("coverlet"),
        CoverletOutputFormat    = CoverletOutputFormat.opencover,
        ExcludeByFile           = ToolSettings.TestCoverageExcludeByFile.Split(';').ToList(),
        ExcludeByAttribute      = ToolSettings.TestCoverageExcludeByAttribute.Split(';').ToList()
    };

    foreach (var filter in ToolSettings.TestCoverageFilter.Split(' '))
    {
        if (filter[0] == '+')
        {
            coverletSettings.WithInclusion(filter.TrimStart('+'));
        }
        else if (filter[0] == '-')
        {
            coverletSettings.WithFilter(filter.TrimStart('-'));
        }
    }
    var settings = new DotNetCoreTestSettings
    {
        Configuration = BuildParameters.Configuration,
        NoBuild = true
    };

    foreach (var project in projects)
    {
        Action<ICakeContext> testAction = tool =>
        {
            tool.DotNetCoreTest(project.FullPath, settings);
        };

        var parsedProject = ParseProject(project, BuildParameters.Configuration);
        settings.ArgumentCustomization = args => {
            args.AppendMSBuildSettings(msBuildSettings, context.Environment);
            // NOTE: This currently causes an exception during the build on AppVeyor, and is
            // related to this issue:
            // https://github.com/coverlet-coverage/coverlet/issues/882
            // Once this issue is fixed, would be good to come back here to re-enable this.
            //if (parsedProject.HasPackage("Microsoft.SourceLink.GitHub"))
            //{
            //    settings.ArgumentCustomization = args => args.Append("/p:UseSourceLink=true");
            //}
            return args;
        };

        if (parsedProject.IsNetCore && parsedProject.HasPackage("coverlet.msbuild"))
        {
            coverletSettings.CoverletOutputName = parsedProject.RootNameSpace.Replace('.', '-');
            DotNetCoreTest(project.FullPath, settings, coverletSettings);
        }
        else if (BuildParameters.BuildAgentOperatingSystem != PlatformFamily.Windows)
        {
            testAction(Context);
        }
        else
        {
            if (BuildParameters.BuildAgentOperatingSystem == PlatformFamily.Windows)
            {
                // We can not use msbuild properties together with opencover
                settings.ArgumentCustomization = null;
                OpenCover(testAction,
                    BuildParameters.Paths.Files.TestCoverageOutputFilePath,
                    new OpenCoverSettings {
                        ReturnTargetCodeOffset = 0,
                        OldStyle = true,
                        Register = "user",
                        MergeOutput = FileExists(BuildParameters.Paths.Files.TestCoverageOutputFilePath)
                    }
                    .WithFilter(ToolSettings.TestCoverageFilter)
                    .ExcludeByAttribute(ToolSettings.TestCoverageExcludeByAttribute)
                    .ExcludeByFile(ToolSettings.TestCoverageExcludeByFile));
            }
        }
    }

    var coverageFiles = GetFiles(BuildParameters.Paths.Directories.TestCoverage + "/coverlet/*.xml");
    if (FileExists(BuildParameters.Paths.Files.TestCoverageOutputFilePath))
    {
        coverageFiles += BuildParameters.Paths.Files.TestCoverageOutputFilePath;
    }

    if (coverageFiles.Any())
    {
        // TODO: Need to think about how to bring this out in a generic way for all Test Frameworks
        var rpSettings = new ReportGeneratorSettings();
        if (BuildParameters.BuildAgentOperatingSystem != PlatformFamily.Windows)
        {
            // Workaround until 0.38.5+ version of cake is released
            // https://github.com/cake-build/cake/pull/2824
            rpSettings.ToolPath = Context.Tools.Resolve("reportgenerator");
        }
        ReportGenerator(coverageFiles, BuildParameters.Paths.Directories.TestCoverage, rpSettings);
    }
});

BuildParameters.Tasks.IntegrationTestTask = Task("Run-Integration-Tests")
    .WithCriteria(() => BuildParameters.ShouldRunIntegrationTests, "Cake script integration tests have been disabled")
    .IsDependentOn("Default")
    .Does(() =>
    {
            CakeExecuteScript(BuildParameters.IntegrationTestScriptPath,
                new CakeSettings
                {
                    Arguments = new Dictionary<string, string>
                    {
                        { "verbosity", Context.Log.Verbosity.ToString("F") }
                    }
                });
    });

BuildParameters.Tasks.GenerateFriendlyTestReportTask = Task("Generate-FriendlyTestReport")
    .IsDependentOn("Test-VSTest")
    .IsDependentOn("Test-xUnit")
    .WithCriteria(() => BuildParameters.BuildAgentOperatingSystem == PlatformFamily.Windows, "Skipping due to not running on Windows")
    .Does(() => RequireTool(ToolSettings.ReportUnitTool, () =>
    {
        var possibleDirectories = new[] {
            BuildParameters.Paths.Directories.xUnitTestResults,
            BuildParameters.Paths.Directories.VSTestTestResults,
        };

        foreach (var directory in possibleDirectories.Where((d) => DirectoryExists(d)))
        {
            ReportUnit(directory, directory, new ReportUnitSettings());
        }
    })
);

BuildParameters.Tasks.TestTask = Task("Test");
