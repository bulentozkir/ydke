using System.Runtime.InteropServices;

namespace YDKE_Windows;

internal static class ProcessEfficiency
{
    private const uint BelowNormalPriorityClass = 0x00004000;
    private const uint PowerThrottlingExecutionSpeed = 0x00000001;
    private const uint PowerThrottlingIgnoreTimerResolution = 0x00000004;
    private const uint PowerThrottlingCurrentVersion = 1;

    public static void Apply()
    {
        var process = GetCurrentProcess();
        _ = SetPriorityClass(process, BelowNormalPriorityClass);

        var flags = PowerThrottlingExecutionSpeed | PowerThrottlingIgnoreTimerResolution;
        var state = new ProcessPowerThrottlingState
        {
            Version = PowerThrottlingCurrentVersion,
            ControlMask = flags,
            StateMask = flags,
        };

        if (!SetProcessInformation(
                process,
                ProcessInformationClass.ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
        {
            state.ControlMask = PowerThrottlingExecutionSpeed;
            state.StateMask = PowerThrottlingExecutionSpeed;
            _ = SetProcessInformation(
                process,
                ProcessInformationClass.ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
        }
    }

    private enum ProcessInformationClass
    {
        ProcessMemoryPriority,
        ProcessMemoryExhaustionInfo,
        ProcessAppMemoryInfo,
        ProcessInPrivateInfo,
        ProcessPowerThrottling,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetPriorityClass(nint process, uint priorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        nint process,
        ProcessInformationClass informationClass,
        ref ProcessPowerThrottlingState information,
        uint informationSize);
}