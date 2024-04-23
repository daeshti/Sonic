using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using static Eeraan.ISysModule.SysCallCtrl;
using static Eeraan.ISysModule.SysCallNum;

namespace Eeraan;

public interface IProcessorModule
{
    void SetCurrThrdCpuAffinity(i32 cpuId);
    i64 LogicalCpuCnt();
}

public class ProcessorModule : IProcessorModule
{
    private static readonly isize MaskSize;

    static ProcessorModule()
    {
        MaskSize = Marshal.SizeOf<cpu_set_t>();
    }

    private readonly ILogger<EeraanModule> _logger;
    private readonly ISysModule _sysModule;

    public ProcessorModule(ILogger<EeraanModule> logger, ISysModule sysModule)
    {
        _logger = logger;
        _sysModule = sysModule;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCurrThrdCpuAffinity(i32 cpuId)
    {
        var mask = new cpu_set_t();
        isize maskPtr;
        unsafe
        {
            maskPtr = (isize)(&mask);
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
        isize ctrlMaskPtr;
        unsafe
        {
            ctrlMaskPtr = (isize)(&ctrlMaskPtr);
        }

        res = _sysModule.SysCall(sched_setaffinity, MaskSize, ctrlMaskPtr);
        if (res != 0)
        {
            _logger.LogWarning("Error setting CPU affinity for CPU {}", cpuId);
        }

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool CpuIsSet(i32 cpuNum, ref cpu_set_t set)
        {
            var chunkIndex = cpuNum / PtrWidthInBits;
            var chunkOffset = cpuNum % PtrWidthInBits;
            return (set.Data[chunkIndex] & (1UL << chunkOffset)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CpuSet(i32 cpuNum, ref cpu_set_t set)
        {
            var chunkIndex = cpuNum / PtrWidthInBits;
            var chunkOffset = cpuNum % PtrWidthInBits;
            set.Data[chunkIndex] |= 1UL << chunkOffset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public i64 LogicalCpuCnt()
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
        static extern IntPtr sysconf(i32 name);
    }
}