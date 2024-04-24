using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using static Sonic.ISysModule.SysCallCtrl;
using static Sonic.ISysModule.SysCallNum;

namespace Sonic;

public interface INetworkModule
{
    long GetListenerFd(ushort port);
    void SetupConn(IntPtr fd);
    void CloseConn(IntPtr epfd, IntPtr fd);
    void AttachReusePortClassicBerkeleyPacketFilter(int fd);
    void DebugIncomingCpu(int incomingFd, int listenerFd, int cpuCore);
}

public sealed class NetworkModule : INetworkModule
{
    private static readonly IntPtr SockAddrInSize;
    private static readonly IntPtr LingerSize;
    private static readonly IntPtr SockFilterProgSize;
    private static readonly linger SockLingerTimeout;

    static NetworkModule()
    {
        SockAddrInSize = Marshal.SizeOf<sockaddr_in>();
        LingerSize = Marshal.SizeOf<linger>();
        SockFilterProgSize = Marshal.SizeOf<sock_fprog>();
        SockLingerTimeout = new linger { l_onoff = 1, l_linger = 0 };
    }

    private readonly ILogger<SonicModule> _logger;
    private readonly ISysModule _sysModule;

    public NetworkModule(ILogger<SonicModule> logger, ISysModule sysModule)
    {
        _logger = logger;
        _sysModule = sysModule;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetListenerFd(ushort port)
    {
        var tcpFastOpenQueueLen = Consts.MaxConnPerThrd;

        var fdListener = _sysModule.SysCall(socket, AF_INET, SOCK_STREAM, 0);
        var optSize = sizeof(long);
        var opt = POSITIVE;
        IntPtr optPtr;
        unsafe
        {
            optPtr = (IntPtr)(&opt);
        }

        _sysModule.SysCall(
            setsockopt,
            fdListener,
            SOL_SOCKET,
            SO_REUSEPORT,
            optPtr,
            optSize
        );

        var addr = new sockaddr_in
        {
            sin_family = AF_INET,
            sin_port = (ushort)IPAddress.HostToNetworkOrder(port),
            sin_addr = new in_addr { s_addr = (uint)IPAddress.HostToNetworkOrder(INADDR_ANY) },
            sin_zero = new ByteX8(),
        };

        IntPtr addressPointer;
        unsafe
        {
            addressPointer = (IntPtr)(&addr);
        }

        _sysModule.SysCall(
            bind,
            fdListener,
            addressPointer,
            SockAddrInSize
        );

        _sysModule.SysCall(listen, fdListener, tcpFastOpenQueueLen);

        return fdListener;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetupConn(IntPtr fd)
    {
        var opt = POSITIVE;
        IntPtr optPtr;
        unsafe
        {
            optPtr = (IntPtr)(&opt);
        }

        _sysModule.SysCall(
            setsockopt,
            fd,
            IPPROTO_TCP,
            TCP_NODELAY,
            optPtr,
            sizeof(int)
        );

        _sysModule.SysCall(SYS_FCNTL, fd, F_SETFL, O_NONBLOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseConn(IntPtr epfd, IntPtr fd)
    {
        var opt = SockLingerTimeout;
        IntPtr optPointer;
        unsafe
        {
            optPointer = (IntPtr)(&opt);
        }

        _sysModule.SysCall(
            setsockopt,
            fd,
            SOL_SOCKET,
            SO_LINGER,
            optPointer,
            LingerSize
        );

        _sysModule.SysCall(
            epoll_ctl,
            epfd,
            EPOLL_CTL_DEL,
            fd,
            0);

        _sysModule.SysCall(close, fd);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AttachReusePortClassicBerkeleyPacketFilter(int fd)
    {
        var code = new sock_filter_x2();
        code[0] = new sock_filter
            { code = BPF_LD | BPF_W | BPF_ABS, jt = 0, jf = 0, k = unchecked((uint)(SKF_AD_OFF + SKF_AD_CPU)) };
        code[1] = new sock_filter { code = BPF_RET | BPF_A, jt = 0, jf = 0, k = 0 };
        
        IntPtr codePtr;
        unsafe
        {
            codePtr = (IntPtr)(&code[0]);
        }

        var prog = new sock_fprog { len = 2, filterPtr = codePtr };
        IntPtr progPtr;
        unsafe
        {
            progPtr = (IntPtr)(&prog);
        }

        _sysModule.SysCall(
            setsockopt,
            fd,
            SOL_SOCKET,
            SO_ATTACH_REUSEPORT_CBPF,
            progPtr,
            SockFilterProgSize
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DebugIncomingCpu(int incomingFd, int listenerFd, int cpuCore)
    {
        var incCpu = -1;
        IntPtr incCpuPtr;
        unsafe
        {
            incCpuPtr = (IntPtr)Unsafe.AsPointer(ref incCpu);
        }

        var incomingCpuRet = _sysModule.SysCall(
            getsockopt,
            incomingFd,
            SOL_SOCKET,
            SO_INCOMING_CPU,
            incCpuPtr,
            sizeof(int)
        );

        var listeningCpu = -1;
        IntPtr listeningCpuPointer;
        unsafe
        {
            listeningCpuPointer = (IntPtr)Unsafe.AsPointer(ref listeningCpu);
        }

        var listenerCpuRet = _sysModule.SysCall(
            getsockopt,
            listenerFd,
            SOL_SOCKET,
            SO_INCOMING_CPU,
            listeningCpuPointer,
            sizeof(int)
        );

        var incNapiId = -1;
        IntPtr incNapiIdPtr;
        unsafe
        {
            incNapiIdPtr = (IntPtr)Unsafe.AsPointer(ref incNapiId);
        }

        var incNapiIdRet = _sysModule.SysCall(
            getsockopt,
            incomingFd,
            SOL_SOCKET,
            SO_INCOMING_NAPI_ID,
            incNapiIdPtr,
            sizeof(int)
        );

        _logger.LogDebug(
            "fd: {}, " +
            "received request on core {} " +
            "with ret value {}, " +
            "should be core {}, " +
            "listener_fd is on core {} " +
            "with ret value {}, " +
            "with napi id {} " +
            "with ret {}.",
            incomingFd, incCpu, incomingCpuRet, cpuCore,
            listeningCpu, listenerCpuRet, incNapiId, incNapiIdRet
            );
    }
}