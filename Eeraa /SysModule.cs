using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Eeraan;


/**
 * Currently, no amount of mortal possessions would make me bind each syscall
 * for P/Invoke (but I'm open to hearing your suggestions), and inlining assembly
 * is not worth the hassle in C# and probably isn't as fast either.
 * So system calls must be called using SysModule.Syscall method overloads
 * and a system call number.
 */
public interface ISysModule
{
    /// <summary>
    /// Call's and returns the result of Linux's syscall function. 
    /// https://letmegooglethat.com/?q=Linux+manual+page+syscall
    /// </summary>
    public isize SysCall(isize number);

    /// <inheritdoc cref="SysModule.SysCall(isize)"/>
    public isize SysCall(isize number, isize arg1);

    /// <inheritdoc cref="SysModule.SysCall(isize)"/>
    public isize SysCall(isize number, isize arg1, isize arg2);

    /// <inheritdoc cref="SysModule.SysCall(isize)"/>
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3);

    /// <inheritdoc cref="SysModule.SysCall(isize)"/>
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3, isize arg4);

    /// <inheritdoc cref="SysModule.SysCall(isize)"/>
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3, isize arg4, isize arg5);

    /// <inheritdoc cref="SysModule.SysCall(isize)"/>
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3, isize arg4, isize arg5, isize arg6);
    
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    
    /**
     * System call numbers should be here. Name of the system calls should be as they are in Linux.
     */
    public static class SysCallNum
    {
        public const isize close = 3;
        public const isize socket = 41;
        public const isize accept = 43;
        public const isize recvfrom = 45;
        public const isize bind = 49;
        public const isize listen = 50;
        public const isize setsockopt = 54;
        public const isize getsockopt = 55;
        public const isize setpriority = 141;
        public const isize sched_setaffinity = 203;
        public const isize sched_getaffinity = 204;
        public const isize clock_gettime = 228;
        public const isize epoll_wait = 232;
        public const isize epoll_ctl = 233;
        public const isize unshare = 272;
        public const isize epoll_create1 = 291;
    }
    
    /**
     * System calls' control words should be here. Name of the control words should at least be
     * as if they're in Linux.
     */
    public static class SysCallCtrl
    {
        public const i32 CLOCK_REALTIME = 0;
        
        public const i32 CURRENT_THREAD_CONTROL_PID = 0;
        public const i32 _SC_NPROCESSORS_ONLN = 84;
        
        public const i32 AF_INET = 2;

        public const i32 SOCK_STREAM = 1;
        
        public const i32 SOL_SOCKET = 1;

        public const i32 SO_REUSEPORT = 15;

        public const isize INADDR_ANY = 0;
        
        public const i32 POSITIVE = 1;
        
        public const i32 BUSYPOLL = 50;
        
        public const i32 O_NONBLOCK = 2048;
        
        public const i32 F_SETFL = 4;
        
        public const i32 IPPROTO_TCP = 6;
        
        public const i32 TCP_NODELAY = 1;
        
        public const isize SYS_FCNTL = 72;

        public const i32 SO_LINGER = 13;
        
        public const int EPOLL_CTL_ADD = 1;
        public const int EPOLL_CTL_DEL = 2;


        // BPF_CLASS
        public const u16 BPF_LD = 0x00;
        public const u16 BPF_RET = 0x06;

        // BPF_SIZE
        public const u16 BPF_W = 0x00;

        // BPF_MODE
        public const u16 BPF_ABS = 0x20;

        // BPF_RVAL
        public const u16 BPF_A = 0x10;

        // SKF
        public const i32 SKF_AD_OFF = -0x1000;
        public const i32 SKF_AD_CPU = 36;
        
        public const i32 SO_ATTACH_REUSEPORT_CBPF = 51;
        public const i32 SO_INCOMING_CPU = 49;
        public const i32 SO_INCOMING_NAPI_ID = 56;

        public const isize PRIO_PROCESS = 0;

        public const isize CLONE_FILES = 0x400;

        internal const isize EPOLLIN = 0x1;
        
        internal const int EPOLL_TIMEOUT_BLOCKING = -1;

        internal const int EPOLL_TIMEOUT_IMMEDIATE_RETURN = 0;


    }
    
    // ReSharper restore IdentifierTypo
    // ReSharper restore InconsistentNaming
}

/// <inheritdoc cref="ISysModule"/>
public sealed class SysModule : ISysModule
{
    [DllImport("libc", SetLastError = true)]
    private static extern isize syscall(isize number, __arglist);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number)
    {
        return syscall(number, __arglist());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number, isize arg1)
    {
        return syscall(number, __arglist(arg1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number, isize arg1, isize arg2)
    {
        return syscall(number, __arglist(arg1, arg2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3)
    {
        return syscall(number, __arglist(arg1, arg2, arg3));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3, isize arg4)
    {
        return syscall(number, __arglist(arg1, arg2, arg3, arg4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3, isize arg4, isize arg5)
    {
        return syscall(number, __arglist(arg1, arg2, arg3, arg4, arg5));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public isize SysCall(isize number, isize arg1, isize arg2, isize arg3, isize arg4, isize arg5, isize arg6)
    {
        return syscall(number, __arglist(arg1, arg2, arg3, arg4, arg5, arg6));
    }
}
