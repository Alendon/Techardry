using System.Diagnostics;
using MintyCore;

// ReSharper disable once RedundantAssignment
bool debug = false;
#if DEBUG
debug = true;
#endif

var processFile = new FileInfo(Environment.ProcessPath!);

var solutionFolder = processFile.Directory.Parent.Parent.Parent.Parent;
var projectFolder = solutionFolder.EnumerateDirectories("Techardry", SearchOption.TopDirectoryOnly).FirstOrDefault();

//Compile the project
var compileProcess = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"build {projectFolder.FullName} -c {(debug ? "Debug" : "Release")}",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = false,
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

var buildFolder = projectFolder.EnumerateDirectories("bin", SearchOption.TopDirectoryOnly).FirstOrDefault();
buildFolder = buildFolder.EnumerateDirectories(debug ? "Debug" : "Release", SearchOption.TopDirectoryOnly)
    .FirstOrDefault();
var modFolder = buildFolder.EnumerateDirectories("net6.0", SearchOption.TopDirectoryOnly).FirstOrDefault();

Engine.Main(new[] {$"-addModDir={modFolder.FullName}"});