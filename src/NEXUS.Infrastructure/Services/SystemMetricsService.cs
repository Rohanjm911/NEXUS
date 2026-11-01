using System.Diagnostics;
using System.Runtime.InteropServices;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Infrastructure.Services;

public class SystemMetricsService : ISystemMetricsService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private DateTime _lastCpuSampleTime = DateTime.MinValue;
    private double _lastCpuUsage = 0.0;
    private readonly object _cpuLock = new();

    public Task<SystemMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new SystemMetrics();

        // 1. RAM Information via GlobalMemoryStatusEx
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
        {
            metrics.TotalRamMb = memStatus.ullTotalPhys / (1024.0 * 1024.0);
            metrics.AvailableRamMb = memStatus.ullAvailPhys / (1024.0 * 1024.0);
            metrics.RamUsagePercentage = memStatus.dwMemoryLoad;
        }

        // 2. CPU Usage calculation via GetSystemTimes
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCpuSampleTime).TotalMilliseconds >= 250)
            {
                if (GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
                {
                    var idle = ToUInt64(idleFt);
                    var kernel = ToUInt64(kernelFt);
                    var user = ToUInt64(userFt);

                    if (_lastCpuSampleTime != DateTime.MinValue)
                    {
                        var idleDelta = idle - _lastIdle;
                        var totalDelta = (kernel - _lastKernel) + (user - _lastUser);

                        if (totalDelta > 0)
                        {
                            var busy = totalDelta - idleDelta;
                            _lastCpuUsage = Math.Clamp((busy * 100.0) / totalDelta, 0.0, 100.0);
                        }
                    }

                    _lastIdle = idle;
                    _lastKernel = kernel;
                    _lastUser = user;
                    _lastCpuSampleTime = now;
                }
            }

            metrics.CpuUsagePercentage = Math.Round(_lastCpuUsage, 1);
        }

        return Task.FromResult(metrics);
    }

    private static ulong ToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }
}
