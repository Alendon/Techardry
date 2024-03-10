using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Xml;
using JetBrains.Annotations;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;

class Build : NukeBuild
{
    [Solution] readonly Solution? Solution;

    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.BuildModFile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter] readonly string? ModProject;

    [Parameter] readonly string[] ShaderInputFolders = Array.Empty<string>();
    [Parameter] readonly string[] ShaderOutputFolders = Array.Empty<string>();
    [Parameter] readonly bool ShaderDebugging;
    [Parameter] readonly string? ResourceFolder;

    Target Clean => _ => _
        .Executes(() =>
        {
            Solution.NotNull();

            var project = Solution!.GetProject(ModProject);
            var msBuildProject = project.GetMSBuildProject();

            var references = msBuildProject.GetItems("PackageReference")
                .Concat(msBuildProject.GetItems("ProjectReference"));

            msBuildProject.RemoveItems(
                references.Where(reference => reference.HasMetadata("AutoGenerated"))
            );

            msBuildProject.Save();

            string[] pattern = { "*" };

            var objFolder = project.Directory / "obj";
            foreach (var globDirectory in objFolder.GlobDirectories(pattern))
                FileSystemTasks.DeleteDirectory(globDirectory);

            var binFolder = project.Directory / "bin";

            binFolder.GlobDirectories(pattern).SelectMany(x => x.GlobDirectories(pattern))
                .ForEach(FileSystemTasks.DeleteDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            Solution.NotNull();

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null);
            var packageSourceProvider = new NuGet.Configuration.PackageSourceProvider(settings);

            var packageSources = packageSourceProvider.LoadPackageSources();

            var project = Solution!.GetProject(ModProject);
            var msBuildProject = project.GetMSBuildProject();

            var sourceRepositories = packageSources
                .Select(x => new SourceRepository(x, Repository.Provider.GetCoreV3())).ToList();
            var packageMetadataResources = sourceRepositories.Select(x => x.GetResource<PackageMetadataResource>());

            foreach (var (packageName, targetVersion) in GetModDependencyPackageNames(project))
            {
                var projectDependency = Solution.Projects.FirstOrDefault(x => x.Name == packageName);


                if (projectDependency is not null)
                {
                    msBuildProject.AddItem("ProjectReference",
                        project.Directory.GetRelativePathTo(projectDependency.Path), new Dictionary<string, string>()
                        {
                            { "AutoGenerated", "true" }
                        });
                    continue;
                }

                NuGetVersion? versionToUse = targetVersion;

                if (targetVersion is null)
                    foreach (var metadataResource in packageMetadataResources)
                    {
                        var packageMetadata = metadataResource.GetMetadataAsync(packageName, true, true,
                            new SourceCacheContext(), NullLogger.Instance,
                            default);
                        packageMetadata.Wait();

                        var version = packageMetadata.Result.Max(p => p.Identity.Version);
                        versionToUse = versionToUse is null || version > versionToUse ? version : versionToUse;
                    }

                msBuildProject.AddItem("PackageReference", packageName, new Dictionary<string, string>()
                {
                    { "AutoGenerated", "true" }, { "Version", versionToUse.ToString() }
                });
            }

            msBuildProject.Save();

            DotNetTasks.DotNetRestore(_ =>
                DotNetRestoreSettingsExtensions.SetProjectFile<DotNetRestoreSettings>(_, msBuildProject.FullPath)
            );
        });

    string DotnetVersion(Project project)
    {
        return project.GetTargetFrameworks().First();
    }

    AbsolutePath BuildPath(Project project)
    {
        return project.Directory / "bin" / Configuration / DotnetVersion(project);
    }

    Target CompileShaders => _ => _
        .Executes(() =>
        {
            Solution.NotNull();

            var project = Solution!.GetProject(ModProject);
            project.NotNull($"Failed to find project with name {ModProject}");


            Assert.True(ShaderInputFolders.Length == ShaderOutputFolders.Length,
                "ShaderInputFolders and ShaderOutputFolders must have the same length");

            for (var i = 0; i < ShaderInputFolders.Length; i++)
            {
                var input = new DirectoryInfo(project.Directory / ShaderInputFolders[i]);
                var output = new DirectoryInfo(project.Directory / ResourceFolder / ShaderOutputFolders[i]);

                Assert.True(input.Exists, $"Shader input folder {input.FullName} does not exist");
                Assert.True(output.Exists, $"Shader output folder {output.FullName} does not exist");

                ShaderCompiler.ShaderCompiler.CompileShaders(input, output, ShaderDebugging);
            }
        });

    Target RawBuild => _ => _
        .DependsOn(Restore, CompileShaders)
        .Executes(() =>
        {
            Solution.NotNull();

            var project = Solution!.GetProject(ModProject);
            var msBuildProject = project.GetMSBuildProject();

            DotNetTasks.DotNetBuild(_ => DotNetBuildSettingsExtensions
                .SetProjectFile<DotNetBuildSettings>(_, msBuildProject.FullPath)
                .SetConfiguration(Configuration)
                .SetProperty("EnableDynamicLoading", "true")
            );
        });

    Target BuildModFile => _ => _
        .DependsOn(RawBuild)
        .Executes(() =>
        {
            Solution.NotNull();
            ModProject.NotNull("ModProject parameter needs to be set");

            var project = Solution!.GetProject(ModProject);

            project.NotNull($"Failed to find project with name {ModProject}");

            var modManifest = ReadManifest(project);

            var buildPath = BuildPath(project);

            var jsonDepsPath = buildPath / $"{project.Name}.deps.json";

            var jsonDeps = JsonDocument.Parse(File.ReadAllText(jsonDepsPath));

            var resolver = new DependencyResolver(jsonDeps, ModProject!, GetModDependencyPackageNames(project));
            modManifest.ExternalDependencies = resolver.GetDependencies();

            ValidateManifest(modManifest);

            var tempPath = buildPath / "temp";

            FileSystemTasks.EnsureCleanDirectory(tempPath);

            var dllFileName = $"{project.Name}.dll";
            FileSystemTasks.CopyFile(buildPath / dllFileName, tempPath / dllFileName);

            var pdbFileName = $"{project.Name}.pdb";
            var pdbPath = buildPath / pdbFileName;
            if (pdbPath.FileExists())
                FileSystemTasks.CopyFile(pdbPath, tempPath / pdbFileName);

            using (var stream = new FileStream(tempPath / "manifest.json", FileMode.Create))
            {
                JsonSerializer.Serialize(stream, modManifest);
            }

            var resourceSourcePath = project.Directory / ResourceFolder;
            var resourceTargetPath = tempPath / "resources";
            if (resourceSourcePath.Exists("dir"))
            {
                FileSystemTasks.CopyDirectoryRecursively(resourceSourcePath, resourceTargetPath);
            }

            var libPath = tempPath / "lib";

            foreach (var dependency in modManifest.ExternalDependencies)
            {
                var dependencyPath = buildPath / dependency.DllName;
                FileSystemTasks.CopyFileToDirectory(dependencyPath, libPath);
            }

            var modFile = buildPath.Parent / $"{modManifest.Identifier}-{modManifest.Version}.mcmod";
            CompressionTasks.CompressZip(tempPath, modFile, compressionLevel: CompressionLevel.Optimal,
                fileMode: FileMode.Create);
        });

    List<(string packageName, NuGetVersion? version)> GetModDependencyPackageNames(Project project)
    {
        var modInfoNode = ModInfoXmlNode(project);

        modInfoNode.NotNull("ProjectExtension/MintyCoreMod tag is missing from project file.");

        var dependencyNodes = modInfoNode!.SelectNodes("Dependency")?.Cast<XmlNode>();

        if (dependencyNodes is null)
        {
            Log.Warning("No mod dependencies found at ProjectExtensions/MintyCoreMod/Dependency");
            return new List<(string packageName, NuGetVersion? version)>();
        }

        var packageNames = new List<(string packageName, NuGetVersion? version)>();

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var dependencyNode in dependencyNodes)
        {
            var packageName = dependencyNode.Attributes?["PackageName"]?.Value;
            var packageVersionString = dependencyNode.Attributes?["Version"]?.Value;
            NuGetVersion.TryParse(packageVersionString, out NuGetVersion? version);
            if (version is null)
                Log.Warning("No valid version ({VersionString}) found for package {PackageName}", packageVersionString,
                    packageName);

            if (packageName is not null)
                packageNames.Add((packageName, version));
        }

        return packageNames;
    }

    Target PackToNuget => _ => _
        .DependsOn(RawBuild)
        .Executes(() =>
        {
            Solution.NotNull();

            var project = Solution!.GetProject(ModProject);

            DotNetTasks.DotNetPack(_ => DotNetPackSettingsExtensions.EnableNoBuild<DotNetPackSettings>(_)
                .SetProject(project)
                .SetConfiguration(Configuration)
            );
        });

    void ValidateManifest(ModManifest manifest)
    {
        //check mod name is set
        Assert.False(string.IsNullOrWhiteSpace(manifest.Name),
            "Mod Name is required. Add a name in the project file at \"ProjectExtensions/MintyCoreMod/Name\"");

        //check version is set
        manifest.Version.NotNull(
            "Mod Version is required. Add a version in the project file at \"ProjectExtensions/MintyCoreMod/Version\"");

        //check mod id is set
        Assert.False(string.IsNullOrWhiteSpace(manifest.Identifier),
            "Mod Id is required. Add an id in the project file at \"ProjectExtensions/MintyCoreMod/Id\"");

        //check mod id contains only letters and underscores
        Assert.True(manifest.Identifier.All(x => char.IsLetterOrDigit(x) || x == '_'),
            "Mod Id contains invalid characters. Add a id in the project file at \"ProjectExtensions/MintyCoreMod/Id\"");

        //check mod has a description (warning)
        if (string.IsNullOrWhiteSpace(manifest.Description))
            Log.Warning(
                "Mod Description is not set. Add a description in the project file at \"ProjectExtensions/MintyCoreMod/Description\"");

        //check mod has a author (warning)
        if (manifest.Authors.Length == 0)
            Log.Warning(
                "Mod Author is not set. Add a author in the project file at \"ProjectExtensions/MintyCoreMod/Author\"");

        //check mod has at least one dependency (warning)
        if (manifest.ModDependencies.Count == 0)
            Log.Warning(
                "Mod has no dependencies. Add a dependency in the project file at \"ProjectExtensions/MintyCoreMod/Dependencies\"");
    }

    ModManifest ReadManifest(Project project)
    {
        var baseNode = ModInfoXmlNode(project);

        var manifest = new ModManifest();


        manifest.Name = baseNode?.SelectSingleNode("Name")?.InnerText ?? String.Empty;
        manifest.Description = baseNode?.SelectSingleNode("Description")?.InnerText ?? String.Empty;

        var versionString = baseNode?.SelectSingleNode("Version")?.InnerText;
        manifest.Version = versionString is not null ? new Version(versionString) : null;
        manifest.Authors = baseNode?.SelectSingleNode("Authors")?.InnerText.Split(';') ?? Array.Empty<string>();
        manifest.Identifier = baseNode?.SelectSingleNode("Id")?.InnerText ?? String.Empty;
        manifest.IsRootMod = baseNode?.SelectSingleNode("IsRootMod")?.InnerText.ToLower() == "true";

        var modDependencies = baseNode?.SelectNodes("Dependency");

        if (modDependencies is null) return manifest;

        foreach (XmlNode modDependency in modDependencies)
        {
            if (modDependency?.Attributes is null) continue;

            var idAttribute = modDependency.Attributes["Id"];
            if (idAttribute is null) continue;

            manifest.ModDependencies.Add(idAttribute.Value);
        }

        return manifest;
    }

    static XmlNode? ModInfoXmlNode(Project project)
    {
        var msBuildProject = project.GetMSBuildProject();

        var rawXml = msBuildProject.Xml.RawXml;

        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(rawXml);


        var baseNode = xmlDocument.SelectSingleNode("Project/ProjectExtensions/MintyCoreMod");
        return baseNode;
    }
}

[PublicAPI]
class ModManifest
{
    public string Name { get; set; } = string.Empty;
    public Version? Version { get; set; } = new();
    public string Identifier { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string[] Authors { get; set; } = Array.Empty<string>();

    //TODO: add support for version ranges
    public HashSet<string> ModDependencies { get; set; } = new();
    public HashSet<ExternalDependency> ExternalDependencies { get; set; } = new();
    public bool IsRootMod { get; set; }
}