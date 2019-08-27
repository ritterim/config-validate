#addin nuget:?package=Cake.FileHelpers&version=3.1.0

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var version = FileReadText("./version.txt").Trim();

var packageVersion = version;
if (!AppVeyor.IsRunningOnAppVeyor)
{
    packageVersion += "-dev";
}
else if (AppVeyor.Environment.Repository.Branch != "master")
{
    packageVersion += "-alpha" + AppVeyor.Environment.Build.Number;
}

// Ref. https://github.com/cake-build/cake/issues/1256
const string solutionFolderType = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

var projects = ParseSolution("./ConfigValidate.sln").Projects.Where(x => x.Type != solutionFolderType).ToList();
var tests = projects.Where(x => x.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)).ToList();

Task("Clean")
    .Does(() =>
    {
        foreach (var project in projects) {
            DotNetCoreClean(project.Path.ToString());
        }

        DeleteFiles(GetFiles("./artifacts/*.nupkg"));
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        foreach (var project in projects) {
            DotNetCoreRestore(project.Path.ToString());
        }
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        var buildSettings = new DotNetCoreBuildSettings {
            Configuration = configuration,
            NoRestore = true,
            ArgumentCustomization = args => args
                .Append("/p:TreatWarningsAsErrors=true")
                .Append("/p:Version=" + packageVersion)
        };

        foreach (var project in projects) {
            DotNetCoreBuild(project.Path.ToString(), buildSettings);
        }
    });

Task("Tests")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var buildSettings = new DotNetCoreTestSettings
        {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            ArgumentCustomization = args => args
                .Append("/p:TreatWarningsAsErrors=true")
                .Append("/p:Version=" + packageVersion)
        };

        foreach (var test in tests)
        {
            DotNetCoreTest(test.Path.ToString(), buildSettings);
        }
    });

Task("Package")
    .IsDependentOn("Tests")
    .Does(() =>
    {
        var packageSettings = new DotNetCorePackSettings {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            OutputDirectory = "./artifacts/",
            ArgumentCustomization = args => args.Append("/p:Version=" + packageVersion)
        };

        foreach (var project in projects.Where(x => !tests.Select(y => y.Id).Contains(x.Id))) {
            DotNetCorePack(project.Path.ToString(), packageSettings);
        }
    });

Task("Default")
    .IsDependentOn("Package");

RunTarget(target);
