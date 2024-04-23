using System.Net;
using Microsoft.Extensions.Logging;
using static Eeraan.ISysModule.SysCallCtrl;
using static Eeraan.ISysModule.SysCallNum;

namespace Eeraan;

public interface INetworkModule
{
    i64 GetListenerFd(u16 port);
    void SetupConn(isize fd);
    void CloseConn(i32 epfd, i32 fd);
    void AttachReusePortClassicBerkeleyPacketFilter(i32 fd);
    void DebugIncomingCpu(i32 incomingFd, i32 listenerFd, i32 cpuCore);
}

public class NetworkModule : INetworkModule
{
    private static readonly isize SockAddrInSize;
    private static readonly isize LingerSize;
    private static readonly isize SockFilterProgSize;
    private static readonly linger SockLingerTimeout;

    static NetworkModule()
    {
        SockAddrInSize = Marshal.SizeOf<sockaddr_in>();
        LingerSize = Marshal.SizeOf<linger>();
        SockFilterProgSize = Marshal.SizeOf<sock_fprog>();
        SockLingerTimeout = new linger { l_onoff = 1, l_linger = 0 };
    }

    private readonly ILogger<EeraanModule> _logger;
    private readonly ISysModule _sysModule;

    public NetworkModule(ILogger<EeraanModule> logger, ISysModule sysModule)
    {
        _logger = logger;
        _sysModule = sysModule;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public i64 GetListenerFd(u16 port)
    {
        var tcpFastOpenQueueLen = MaxConnPerThrd;

        var fdListener = _sysModule.SysCall(socket, AF_INET, SOCK_STREAM, 0);
        var optSize = sizeof(i64);
        var opt = POSITIVE;
        isize optPtr;
        unsafe
        {
            optPtr = (isize)(&opt);
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
            sin_port = (u16)IPAddress.HostToNetworkOrder(port),
            sin_addr = new in_addr { s_addr = (u32)IPAddress.HostToNetworkOrder(INADDR_ANY) },
            sin_zero = new u8x8(),
        };

        isize addressPointer;
        unsafe
        {
            addressPointer = (isize)(&addr);
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
    public void SetupConn(isize fd)
    {
        var opt = POSITIVE;
        isize optPtr;
        unsafe
        {
            optPtr = (isize)(&opt);
        }

        _sysModule.SysCall(
            setsockopt,
            fd,
            IPPROTO_TCP,
            TCP_NODELAY,
            optPtr,
            sizeof(i32)
        );

        _sysModule.SysCall(SYS_FCNTL, fd, F_SETFL, O_NONBLOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseConn(i32 epfd, i32 fd)
    {
        var opt = SockLingerTimeout;
        isize optPointer;
        unsafe
        {
            optPointer = (isize)(&opt);
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
    public void AttachReusePortClassicBerkeleyPacketFilter(i32 fd)
    {
        var code = new sock_filter_x2();
        code[0] = new sock_filter
            { code = BPF_LD | BPF_W | BPF_ABS, jt = 0, jf = 0, k = unchecked((u32)(SKF_AD_OFF + SKF_AD_CPU)) };
        code[1] = new sock_filter { code = BPF_RET | BPF_A, jt = 0, jf = 0, k = 0 };
        
        isize codePtr;
        unsafe
        {
            codePtr = (isize)(&code[0]);
        }

        var prog = new sock_fprog { len = 2, filterPtr = codePtr };
        isize progPtr;
        unsafe
        {
            progPtr = (isize)(&prog);
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
    public void DebugIncomingCpu(i32 incomingFd, i32 listenerFd, i32 cpuCore)
    {
        var incCpu = -1;
        isize incCpuPtr;
        unsafe
        {
            incCpuPtr = (isize)Unsafe.AsPointer(ref incCpu);
        }

        var incomingCpuRet = _sysModule.SysCall(
            getsockopt,
            incomingFd,
            SOL_SOCKET,
            SO_INCOMING_CPU,
            incCpuPtr,
            sizeof(i32)
        );

        var listeningCpu = -1;
        isize listeningCpuPointer;
        unsafe
        {
            listeningCpuPointer = (isize)Unsafe.AsPointer(ref listeningCpu);
        }

        var listenerCpuRet = _sysModule.SysCall(
            getsockopt,
            listenerFd,
            SOL_SOCKET,
            SO_INCOMING_CPU,
            listeningCpuPointer,
            sizeof(i32)
        );

        var incNapiId = -1;
        isize incNapiIdPtr;
        unsafe
        {
            incNapiIdPtr = (isize)Unsafe.AsPointer(ref incNapiId);
        }

        var incNapiIdRet = _sysModule.SysCall(
            getsockopt,
            incomingFd,
            SOL_SOCKET,
            SO_INCOMING_NAPI_ID,
            incNapiIdPtr,
            sizeof(i32)
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