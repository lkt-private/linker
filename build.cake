#tool nuget:?package=xunit.runner.console&version=2.2.0
#tool nuget:?package=OpenCover&version=4.6.519
#tool nuget:?package=ReportGenerator&version=2.5.8
#tool nuget:?package=GitVersion.CommandLine&version=3.6.5
#tool nuget:?package=OctopusTools&version=4.21.0

#addin nuget:?package=Cake.WebDeploy&version=0.2.4

#load build/paths.cake
#load build/urls.cake

var target = Argument("target", "Test");
var configuration = Argument("Configuration", "Release");
var codeCoverageReportPath = Argument<FilePath>("CodeCoverageReportPath", "coverage.zip");
var packageOutputPath = Argument<DirectoryPath>("PackageOutputPath", "packages");

var packageVersion = "0.1.0";
var packagePath = File("Linker.zip").Path;

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

Task("Clean-Packages")
	.Does(()=> {
        if (DirectoryExists(packageOutputPath)) {
            DeleteDirectory(packageOutputPath, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
	});

Task("Package-NuGet")
	.IsDependentOn("Test")
	.IsDependentOn("Version")
	.IsDependentOn("Clean-Packages")
	.Does(()=> {
		EnsureDirectoryExists(packageOutputPath);

		NuGetPack(Paths.WebNuspecFile,
			new NuGetPackSettings {
				Version = packageVersion,
				OutputDirectory = packageOutputPath,
				NoPackageAnalysis = true
			});
	});

Task("Package-WebDeploy")
	.IsDependentOn("Test")
	.IsDependentOn("Version")
	.IsDependentOn("Clean-Packages")
	.Does(()=> {
		EnsureDirectoryExists(packageOutputPath);
		packagePath = Combine(MakeAbsolute(packageOutputPath), $"Linker.{packageVersion}.zip");

		MSBuild(Paths.WebProjectFile,
			settings => settings.SetConfiguration(configuration)
								.WithTarget("Package")
								.WithProperty("PackageLocation", packagePath.FullPath));
	});

Task("Deploy-OctopusDeploy")
	.IsDependentOn("Package-NuGet")
	.Does(()=> {
		OctoPush(
			Urls.OctopusServerUrl,
			EnvironmentVariable("OctopusApiKey"),
			GetFiles($"{packageOutputPath}/*.nupkg"),
			new OctopusPushSettings { ReplaceExisting = true });

		OctoCreateRelease(
			"Linker",
			new CreateReleaseSettings { 
				Server = Urls.OctopusServerUrl,
				ApiKey = EnvironmentVariable("OctopusApiKey"),
				ReleaseNumber = packageVersion,
				DefaultPackageVersion = packageVersion,
				DeployTo = "Test",
				WaitForDeployment = true
			});
	});

Task("Deploy-WebDeploy")
	.IsDependentOn("Package-WebDeploy")
	.Does(()=> {
		DeployWebsite(new DeploySettings()
			.SetPublishUrl(Urls.WbDeployPublishUrl)
			.FromSourcePath(packagePath.FullPath)
			.ToDestinationPath("site/wwwroot/Linker")
			.UseSiteName("Linker-Demo")
			.AddParameter("IIS Web Application Name", "Linker-Demo")
			.UseUsername(EnvironmentVariable("DeploymentUser"))
			.UsePassword(EnvironmentVariable("DeploymentPassword")));
	});

RunTarget(target);