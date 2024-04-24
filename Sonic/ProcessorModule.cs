using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using static Sonic.ISysModule.SysCallCtrl;
using static Sonic.ISysModule.SysCallNum;

namespace Sonic;

public interface IProcessorModule
{
    void SetCurrThrdCpuAffinity(int cpuId);
    long LogicalCpuCnt();
}

public sealed class ProcessorModule : IProcessorModule
{
    private static readonly IntPtr MaskSize;

    static ProcessorModule()
    {
        MaskSize = Marshal.SizeOf<cpu_set_t>();
    }

    private readonly ILogger<SonicModule> _logger;
    private readonly ISysModule _sysModule;

    public ProcessorModule(ILogger<SonicModule> logger, ISysModule sysModule)
    {
        _logger = logger;
        _sysModule = sysModule;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrThrdCpuAffinity(int cpuId)
    {
        var mask = new cpu_set_t();
        IntPtr maskPtr;
        unsafe
        {
            maskPtr = (IntPtr)(&mask);
        }


        var res = _sysModule.SysCall(
            sched_getaffinity,
            CURRENT_THREAD_CONTROL_PID,
            MaskSize,
            maskPtr
        );

        if (res != 0)
        {
            _logger.LogWarning("Cannot set affinity for cpu {}", cpuId);
            return;
        }

        // Check if the CPU is set in the current affinity
        if (!CpuIsSet(cpuId, ref mask))
        {
            _logger.LogWarning("Cannot set affinity for CPU {}", cpuId);
            return;
        }

        var ctrlMask = new cpu_set_t();
        CpuSet(cpuId, ref ctrlMask);
        IntPtr ctrlMaskPtr;
        unsafe
        {
            ctrlMaskPtr = (IntPtr)(&ctrlMaskPtr);
        }

        res = _sysModule.SysCall(sched_setaffinity, MaskSize, ctrlMaskPtr);
        if (res != 0)
        {
            _logger.LogWarning("Error setting CPU affinity for CPU {}", cpuId);
        }

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool CpuIsSet(int cpuNum, ref cpu_set_t set)
        {
            var chunkIndex = cpuNum / Consts.PtrWidthInBits;
            var chunkOffset = cpuNum % Consts.PtrWidthInBits;
            return (set.Data[chunkIndex] & (1UL << chunkOffset)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CpuSet(int cpuNum, ref cpu_set_t set)
        {
            var chunkIndex = cpuNum / Consts.PtrWidthInBits;
            var chunkOffset = cpuNum % Consts.PtrWidthInBits;
            set.Data[chunkIndex] |= 1UL << chunkOffset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long LogicalCpuCnt()
    {
        var cpuCnt = sysconf(_SC_NPROCESSORS_ONLN);
        if (cpuCnt > 0) return cpuCnt;

        _logger.LogWarning(
            "Cannot determine the number of logical cpus with sysconf, " +
            "performance will be severely impacted"
        );
        return 1;

        // TODO: Find the system call number for sysconf and use ISysModule.SysCall instead.
        [DllImport("libc")]
        static extern IntPtr sysconf(int name);
    }
}