using System.Diagnostics;
using MintyCore;

// ReSharper disable once RedundantAssignment
bool debug = false;
#if DEBUG
debug = true;
#endif

var processFile = new FileInfo(Environment.ProcessPath!);

var solutionFolder = processFile.Directory.Parent.Parent.Parent.Parent;

if(Environment.GetCommandLineArgs().All(x => !x.Contains("skipCompile")))
{

//Compile the project
    var compileProcess = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "nuke",
            Arguments = $"--configuration {(debug ? "Debug" : "Release")}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
            WorkingDirectory = solutionFolder.FullName,
            StandardOutputEncoding = Console.OutputEncoding,
            StandardErrorEncoding = Console.OutputEncoding
        }
    };
    compileProcess.ErrorDataReceived += (sender, e) =>
    {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
        }
    };
    compileProcess.OutputDataReceived += (sender, e) =>
    {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
        }
    };
    compileProcess.Start();
    compileProcess.BeginOutputReadLine();
    compileProcess.BeginErrorReadLine();
    compileProcess.WaitForExit();
    var exitCode = compileProcess.ExitCode;

    if (exitCode != 0)
    {
        Console.WriteLine("Compilation failed");
        Environment.Exit(exitCode);
    }
}

var projectFolder = solutionFolder.EnumerateDirectories("Techardry", SearchOption.TopDirectoryOnly).FirstOrDefault();
var buildFolder = projectFolder.EnumerateDirectories("bin", SearchOption.TopDirectoryOnly).FirstOrDefault();
buildFolder = buildFolder.EnumerateDirectories(debug ? "Debug" : "Release", SearchOption.TopDirectoryOnly)
    .FirstOrDefault();

//Console.Clear();

MintyCore.Program.Main([$"-addModDir={buildFolder.FullName}", "-testingModeActive"]);