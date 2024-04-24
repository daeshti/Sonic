using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sonic;


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
    public IntPtr SysCall(IntPtr number);

    /// <inheritdoc cref="SysModule.SysCall(System.IntPtr)"/>
    public IntPtr SysCall(IntPtr number, IntPtr arg1);

    /// <inheritdoc cref="SysModule.SysCall(System.IntPtr)"/>
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2);

    /// <inheritdoc cref="SysModule.SysCall(System.IntPtr)"/>
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    /// <inheritdoc cref="SysModule.SysCall(System.IntPtr)"/>
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);

    /// <inheritdoc cref="SysModule.SysCall(System.IntPtr)"/>
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5);

    /// <inheritdoc cref="SysModule.SysCall(System.IntPtr)"/>
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5, IntPtr arg6);
    
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    
    /**
     * System call numbers should be here. Name of the system calls should be as they are in Linux.
     */
    public static class SysCallNum
    {
        public const IntPtr close = 3;
        public const IntPtr socket = 41;
        public const IntPtr accept = 43;
        public const IntPtr sendto = 44;
        public const IntPtr recvfrom = 45;
        public const IntPtr bind = 49;
        public const IntPtr listen = 50;
        public const IntPtr setsockopt = 54;
        public const IntPtr getsockopt = 55;
        public const IntPtr setpriority = 141;
        public const IntPtr sched_setaffinity = 203;
        public const IntPtr sched_getaffinity = 204;
        public const IntPtr clock_gettime = 228;
        public const IntPtr epoll_wait = 232;
        public const IntPtr epoll_ctl = 233;
        public const IntPtr unshare = 272;
        public const IntPtr epoll_create1 = 291;
    }
    
    /**
     * System calls' control words should be here. Name of the control words should at least be
     * as if they're in Linux.
     */
    public static class SysCallCtrl
    {
        public const int CLOCK_REALTIME = 0;
        
        public const int CURRENT_THREAD_CONTROL_PID = 0;
        public const int _SC_NPROCESSORS_ONLN = 84;
        
        public const int AF_INET = 2;

        public const int SOCK_STREAM = 1;
        
        public const int SOL_SOCKET = 1;

        public const int SO_REUSEPORT = 15;

        public const IntPtr INADDR_ANY = 0;
        
        public const int POSITIVE = 1;
        
        public const int BUSYPOLL = 50;
        
        public const int O_NONBLOCK = 2048;
        
        public const int F_SETFL = 4;
        
        public const int IPPROTO_TCP = 6;
        
        public const int TCP_NODELAY = 1;
        
        public const IntPtr SYS_FCNTL = 72;

        public const int SO_LINGER = 13;
        
        public const int EPOLL_CTL_ADD = 1;
        public const int EPOLL_CTL_DEL = 2;


        // BPF_CLASS
        public const ushort BPF_LD = 0x00;
        public const ushort BPF_RET = 0x06;

        // BPF_SIZE
        public const ushort BPF_W = 0x00;

        // BPF_MODE
        public const ushort BPF_ABS = 0x20;

        // BPF_RVAL
        public const ushort BPF_A = 0x10;

        // SKF
        public const int SKF_AD_OFF = -0x1000;
        public const int SKF_AD_CPU = 36;
        
        public const int SO_ATTACH_REUSEPORT_CBPF = 51;
        public const int SO_INCOMING_CPU = 49;
        public const int SO_INCOMING_NAPI_ID = 56;

        public const IntPtr PRIO_PROCESS = 0;

        public const IntPtr CLONE_FILES = 0x400;

        public const IntPtr EPOLLIN = 0x1;
        
        public const int EPOLL_TIMEOUT_BLOCKING = -1;

        public const int EPOLL_TIMEOUT_IMMEDIATE_RETURN = 0;
        
        public const int EAGAIN = 1;

        public const int EINTR = 4;



    }
    
    // ReSharper restore IdentifierTypo
    // ReSharper restore InconsistentNaming
}

/// <inheritdoc cref="ISysModule"/>
public sealed class SysModule : ISysModule
{
    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr syscall(IntPtr number, __arglist);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number)
    {
        return syscall(number, __arglist());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number, IntPtr arg1)
    {
        return syscall(number, __arglist(arg1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2)
    {
        return syscall(number, __arglist(arg1, arg2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3)
    {
        return syscall(number, __arglist(arg1, arg2, arg3));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4)
    {
        return syscall(number, __arglist(arg1, arg2, arg3, arg4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5)
    {
        return syscall(number, __arglist(arg1, arg2, arg3, arg4, arg5));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr SysCall(IntPtr number, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5, IntPtr arg6)
    {
        return syscall(number, __arglist(arg1, arg2, arg3, arg4, arg5, arg6));
    }
}
