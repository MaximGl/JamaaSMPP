
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// EXTERNAL NUGET TOOLS
//////////////////////////////////////////////////////////////////////

#tool nuget:?package=GitVersion.CommandLine&version=5.1.1

//////////////////////////////////////////////////////////////////////
// EXTERNAL NUGET LIBRARIES
//////////////////////////////////////////////////////////////////////

#addin "Cake.FileHelpers"
#addin nuget:?package=Cake.Json
#addin nuget:?package=Newtonsoft.Json&version=11.0.2
//#addin "Cake.ExtendedNuGet"


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var outputDir = Directory("./output");
var libDir = outputDir + Directory("lib");

// Define Variables
var slnPath = "./jamaasmpp.sln";

// NuGet
var nuspecFilename = "./JamaaSMPP.nuspec";
var nupkgDestDir = outputDir;// + Directory("nuget-package");
var nuspecDestFile = outputDir + File(nuspecFilename);

// Gitversion
var gitVersionPath = ToolsExePath("GitVersion.exe");
Dictionary<string, object> gitVersionOutput;

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("__UpdateAssemblyVersionInformation")
    .Does(() =>
{
    var gitVersionSettings = new ProcessSettings()
        .SetRedirectStandardOutput(true);

    IEnumerable<string> outputLines;
    StartProcess(gitVersionPath, gitVersionSettings, out outputLines);

    var output = string.Join("\n", outputLines);
    gitVersionOutput = DeserializeJson<Dictionary<string, object>>(output);

    Information("Updated GlobalAssemblyInfo");
    Information("AssemblyVersion -> {0}", gitVersionOutput["AssemblySemVer"]);
    Information("AssemblyFileVersion -> {0}", gitVersionOutput["MajorMinorPatch"]);
    Information("AssemblyInformationalVersion -> {0}", gitVersionOutput["InformationalVersion"]);
});

Task("Clean")
    .Does(() =>
{
    CleanDirectory(outputDir);
	EnsureDirectoryExists(nupkgDestDir);
	EnsureDirectoryExists(libDir);

	// Use MSBuild to clean solution
      MSBuild(slnPath, settings =>
        settings.SetConfiguration(configuration)
				.WithTarget("Clean"));
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(slnPath);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
      // Use MSBuild
      //MSBuild(slnPath, settings =>
       // settings.SetConfiguration(configuration));    

	Information("Building solution...");
    MSBuild(slnPath, settings =>
        settings            
            //.UseToolVersion(MSBuildToolVersion.VS2017)
            .WithProperty("TreatWarningsAsErrors","true")
            .SetVerbosity(Verbosity.Quiet)
            .WithTarget("Build")
            .SetConfiguration(configuration));
});

Task("Export-Files")
    .IsDependentOn("Build")
    .Does(() =>
{
	CopyFile(nuspecFilename, nuspecDestFile);
    CopyFiles("./**/bin/" + configuration + "/*.*", libDir);
});


Task("CreateNugetPackage")
	.IsDependentOn("__UpdateAssemblyVersionInformation")
	.IsDependentOn("Export-Files")
    .Does(() =>
{
    var nugetVersion = gitVersionOutput["NuGetVersion"].ToString();
    var packageName = "JamaaSMPP";

    Information("Building {0}.{1}.nupkg", packageName, nugetVersion);

    var nuGetPackSettings = new NuGetPackSettings {
        Id = packageName,
        Title = packageName,  
        Version = nugetVersion,      
        OutputDirectory = nupkgDestDir
    };
	
    NuGetPack(nuspecDestFile, nuGetPackSettings);
});

Task("Default")    
    .IsDependentOn("Build");

RunTarget(target);

//////////////////////////////////////////////////////////////////////
// HELPER FUNCTIONS
//////////////////////////////////////////////////////////////////////

string ToolsExePath(string exeFileName) {
    var exePath = System.IO.Directory.GetFiles(@".\Tools", exeFileName, SearchOption.AllDirectories).FirstOrDefault();
    return exePath;
}
