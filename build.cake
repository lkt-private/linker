#tool nuget:?package=xunit.runner.console&version=2.2.0
#tool nuget:?package=OpenCover&version=4.6.519
#tool nuget:?package=ReportGenerator&version=2.5.8
#tool nuget:?package=GitVersion.CommandLine&version=3.6.5

#load build/paths.cake

var target = Argument("target", "Test");
var configuration = Argument("Configuration", "Release");
var codeCoverageReportPath = Argument<FilePath>("CodeCoverageReportPath", "coverage.zip");

var packageVersion = "0.1.0";

Task("Restore")
	.Does(()=> {
		NuGetRestore(Paths.SolutionFile);
	});

Task("Build")
	.IsDependentOn("Restore")
	.Does(()=> {
		MSBuild(
		  Paths.SolutionFile,
		  settings => settings.SetConfiguration(configuration).WithTarget("Build"));
	});

Task("Test")
	.IsDependentOn("Build")
	.Does(()=> {
		OpenCover(
		  tool => tool.XUnit2(
			$"**/bin/{configuration}/*Tests.dll",
			new XUnit2Settings {
				ShadowCopy = false
			}),
		  Paths.CodeCoverageResultFile,
		  new OpenCoverSettings()
			.WithFilter("+[Linker.*]*")
			.WithFilter("-[Linker.*Tests*]*")
		);
	});

Task("Report-Coverage")	
	.IsDependentOn("Test")
	.Does(()=> {
		ReportGenerator(
			Paths.CodeCoverageResultFile.FullPath,
			Paths.CodeCoverageReportDirectory,
			new ReportGeneratorSettings {
				ReportTypes = new[] { ReportGeneratorReportType.Html }
			}
		);
		Zip(Paths.CodeCoverageReportDirectory, MakeAbsolute(codeCoverageReportPath));
	});

Task("Version")
	.Does(()=> {
		var version = GitVersion();
		Information($"Calculated semantic version {version.SemVer}");

		packageVersion = version.NuGetVersion;
		Information($"Corresponding package version {packageVersion}");

		if (!BuildSystem.IsLocalBuild) {
			GitVersion(new GitVersionSettings {
				OutputType = GitVersionOutput.BuildServer,
				UpdateAssemblyInfo = true
			});
		}
	});

RunTarget(target);