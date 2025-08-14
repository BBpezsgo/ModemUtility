using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ModemUtility;

static class Priviliges
{
    [DllImport("libc")]
    static extern uint geteuid();

    public static bool IsCurrentProcessElevated()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // https://github.com/dotnet/sdk/blob/v6.0.100/src/Cli/dotnet/Installer/Windows/WindowsUtils.cs#L38
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // https://github.com/dotnet/maintenance-packages/blob/62823150914410d43a3fd9de246d882f2a21d5ef/src/Common/tests/TestUtilities/System/PlatformDetection.Unix.cs#L58
        // 0 is the ID of the root user
        return geteuid() == 0;
    }

    public static async Task StartElevatedAsync(string[] args, CancellationToken cancellationToken)
    {
        string currentProcessPath = Environment.ProcessPath ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.ChangeExtension(typeof(Program).Assembly.Location, "exe")
            : Path.ChangeExtension(typeof(Program).Assembly.Location, null));

        ProcessStartInfo processStartInfo = CreateProcessStartInfo(currentProcessPath, args);

        using Process process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Could not start process.");

        await process.WaitForExitAsync(cancellationToken);
    }

    public static void StartElevated(string[] args)
    {
        string currentProcessPath = Environment.ProcessPath ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.ChangeExtension(typeof(Program).Assembly.Location, "exe")
            : Path.ChangeExtension(typeof(Program).Assembly.Location, null));

        ProcessStartInfo processStartInfo = CreateProcessStartInfo(currentProcessPath, args);

        using Process process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Could not start process.");

        process.WaitForExit();
    }

    static ProcessStartInfo CreateProcessStartInfo(string processPath, string[] args)
    {
        ProcessStartInfo startInfo = new()
        {
            UseShellExecute = true,
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ConfigureProcessStartInfoForWindows(startInfo, processPath, args);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ConfigureProcessStartInfoForMacOS(startInfo, processPath, args);
        }
        else // Unix platforms
        {
            ConfigureProcessStartInfoForLinux(startInfo, processPath, args);
        }

        return startInfo;
    }

    static void ConfigureProcessStartInfoForWindows(ProcessStartInfo startInfo, string processPath, string[] args)
    {
        startInfo.Verb = "runas";
        startInfo.FileName = processPath;

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
    }

    static void ConfigureProcessStartInfoForLinux(ProcessStartInfo startInfo, string processPath, string[] args)
    {
        startInfo.FileName = "sudo";
        startInfo.ArgumentList.Add(processPath);

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
    }

    static void ConfigureProcessStartInfoForMacOS(ProcessStartInfo startInfo, string processPath, string[] args)
    {
        startInfo.FileName = "osascript";
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"do shell script \"{processPath} {string.Join(' ', args)}\" with prompt \"MyProgram\" with administrator privileges");
    }
}
