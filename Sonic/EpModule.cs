using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Sonic.ISysModule.SysCallCtrl;
using static Sonic.ISysModule.SysCallNum;

namespace Sonic;

public delegate int CallBack(in HttpContext context);

public interface IEpModule
{
    void Run(ushort port, CallBack callBack);
}

public sealed class EpModule : IEpModule
{
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    private struct AlignedHttpDate
    {
        public ByteX35 Flattened;
    }

    [InlineArray(Consts.MaxEpollEsRet)]
    private struct EpollEvent340
    {
        private epoll_event _element0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    private struct AlignedEpollEvents
    {
        public EpollEvent340 Flatten;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    internal struct AlignedEpollEvent
    {
        public epoll_event Flatten;
    }

    [InlineArray(Consts.ReqBufSize * Consts.MaxConnPerThrd)]
    internal struct AlignedRequestBufferData
    {
        private byte _element0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    internal struct AlignedRequestBuffer
    {
        public AlignedRequestBufferData Flatten;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    internal struct AlignedResultBuffer
    {
        public ByteX4096 Flatten;
    }

    private readonly ISysModule _sysModule;
    private readonly ITimeModule _timeModule;
    private readonly IProcessorModule _processorModule;
    private readonly INetworkModule _networkModule;
    private readonly IRequestPathModule _requestPathModule;

    private ByteX35 _date;

    public EpModule(
        ISysModule sysModule, ITimeModule timeModule,
        IProcessorModule processorModule, INetworkModule networkModule,
        IRequestPathModule requestPathModule)
    {
        _sysModule = sysModule;
        _timeModule = timeModule;
        _processorModule = processorModule;
        _networkModule = networkModule;
        _requestPathModule = requestPathModule;

        _date = new ByteX35();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Run(ushort port, CallBack callBack)
    {
        _sysModule.SysCall(setpriority, PRIO_PROCESS, 0, -19);

        _date = _timeModule.EpochAsUtf8Buff();

        var coreCnt = Environment.ProcessorCount;
        var cntDown = new CountdownEvent(coreCnt);
        for (var c = 0; c < coreCnt; c++)
        {
            var coreId = c;
            _ = new Thread(start: () =>
            {
                _sysModule.SysCall(unshare, CLONE_FILES);
                _processorModule.SetCurrThrdCpuAffinity(coreId);
                ThreadStart(port, callBack, coreId, cntDown);
            }, maxStackSize: 1024 * 1024 * 8)
            {
                Name = $"sonic{c}",
            };
        }

        cntDown.Wait();

        while (true)
        {
            _date = _timeModule.EpochAsUtf8Buff();
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        // ReSharper disable once FunctionNeverReturns
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThreadStart(
        ushort port,
        CallBack callBack,
        int coreId,
        CountdownEvent cntDown
    )
    {
        var listenerFd = (int)_networkModule.GetListenerFd(port);
        _networkModule.SetupConn(listenerFd);
        cntDown.AddCount();
        if (coreId == 0)
        {
            cntDown.Wait();
            _networkModule.AttachReusePortClassicBerkeleyPacketFilter(listenerFd);
        }

        var epFd = _sysModule.SysCall(epoll_create1, 0);

        var epEListener = new AlignedEpollEvent
        {
            Flatten = new epoll_event()
            {
                Data = new epoll_data()
                {
                    fd = listenerFd,
                },
                events = (uint)EPOLLIN
            }
        };
        IntPtr epEListenerPtr;
        unsafe
        {
            epEListenerPtr = (IntPtr)(&epEListener);
        }

        _sysModule.SysCall(epoll_ctl, epFd, EPOLL_CTL_ADD, listenerFd, epEListenerPtr);

        var epEs = new AlignedEpollEvents();
        IntPtr epEsFPtr;
        unsafe
        {
            epEsFPtr = (IntPtr)(&epEs.Flatten);
        }

        var savedEs = new AlignedEpollEvent();
        savedEs.Flatten.events = (uint)EPOLLIN;

        var reqBuff = new AlignedRequestBuffer();

        // Init state for tracking request buffer position across events
        var reqBuffCurrAddr = new LongX1024();
        IntPtr reqBuffCurrAddrPtr;
        unsafe
        {
            reqBuffCurrAddrPtr = (IntPtr)(&reqBuff.Flatten[0]);
        }

        for (var i = 0; i < Consts.MaxConnPerThrd; i++)
        {
            reqBuffCurrAddr[i] = reqBuffCurrAddrPtr + i * Consts.ReqBufSize;
        }

        var reqBuffResidual = new LongX1024();


        var resBuff = new AlignedResultBuffer();
        long resBuffStartAddr;
        unsafe
        {
            resBuffStartAddr = (long)(&resBuff.Flatten[0]);
        }

        var epWaitType = EPOLL_TIMEOUT_BLOCKING;

        while (true)
        {
            var incEsCnt =
                _sysModule.SysCall(
                    epoll_wait,
                    epFd,
                    epEsFPtr,
                    Consts.MaxEpollEsRet
                );
            if (incEsCnt <= 0)
            {
                epWaitType = EPOLL_TIMEOUT_BLOCKING;
                continue;
            }

            epWaitType = EPOLL_TIMEOUT_IMMEDIATE_RETURN;

            for (var i = 0; i < incEsCnt; i++)
            {
                //         ,     \    /      ,        
                //        / \    )\__/(     / \       
                //       /   \  (_\  /_)   /   \      
                //  ____/_____\__\@  @/___/_____\____ 
                // |             |\../|              |
                // |              \VV/               |
                // |             DANGER              |
                // |_________________________________|
                //  |    /\ /      \\       \ /\    | 
                //  |  /   V        ))       V   \  | 
                //  |/     `       //        '     \| 
                //  `              V                '
                unsafe
                {
                    var currFd = epEs.Flatten[0].Data.fd;

                    // DANGER
                    var reqBuffStartAddr = (IntPtr)(&reqBuff.Flatten[0]) + currFd * Consts.ReqBufSize;

                    // DANGER
                    var reqBuffCurrPos = (IntPtr)(&reqBuffCurrAddr[currFd]) + currFd;

                    // DANGER
                    var residual = &reqBuffResidual[currFd];

                    if (currFd == listenerFd)
                    {
                        var incomingFd = _sysModule.SysCall(accept, listenerFd, 0, 0);

                        if (incomingFd is >= 0 and < Consts.MaxConnPerThrd)
                        {
                            // DANGER
                            reqBuffCurrPos = reqBuffStartAddr;

                            // DANGER
                            *residual = 0;

                            _networkModule.SetupConn(incomingFd);
                            savedEs.Flatten.Data.fd = (int)incomingFd;

                            _sysModule.SysCall(
                                epoll_ctl,
                                epFd,
                                EPOLL_CTL_ADD,
                                incomingFd,
                                (IntPtr)(&savedEs.Flatten)
                            );
                        }
                        else
                        {
                            _networkModule.CloseConn(epFd, currFd);
                        }
                    }
                    else
                    {
                        // DANGER
                        var buffRem = Consts.ReqBufSize - (IntPtr)reqBuffCurrPos - (IntPtr)reqBuffStartAddr;

                        var read = _sysModule.SysCall(
                            recvfrom,
                            currFd,
                            reqBuffCurrPos,
                            buffRem,
                            0,
                            0,
                            0
                        );

                        if (read > 0)
                        {
                            IntPtr reqBuffOffset = 0;
                            var resBuffFilledTotal = 0;

                            while (reqBuffOffset != (read + *residual))
                            {
                                sbyte* method = null;
                                var methodLen = 0;
                                sbyte* path = null;
                                var pathLen = 0;

                                var reqBuffBytesParsed = _requestPathModule.ParseRequestPathPipelinedSimd(
                                    (sbyte*)(reqBuffCurrPos - *residual + reqBuffOffset),
                                    (int)(read + *residual - reqBuffOffset),
                                    &method,
                                    methodLen,
                                    &path,
                                    pathLen);

                                if (reqBuffBytesParsed > 0)
                                {
                                    reqBuffOffset += reqBuffBytesParsed;
                                    var methodStr = MethodStr(method, methodLen);
                                    var pathStr = PathStr(path, pathLen);
                                    var context = new HttpContext
                                    {
                                        Method = method,
                                        MethodLen = methodLen,
                                        Path = path,
                                        PathLen = pathLen,
                                        Date = _date,
                                        ResBuff = (IntPtr)resBuffStartAddr + resBuffFilledTotal
                                    };
                                    var resBuffFilled = callBack(context);

                                    resBuffFilledTotal += resBuffFilled;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (reqBuffOffset == 0 || resBuffFilledTotal == 0)
                            {
                                reqBuffCurrPos = reqBuffStartAddr;
                                *residual = 0;
                                _networkModule.CloseConn((int)epFd, currFd);
                                continue;
                            }
                            else if (reqBuffOffset == read + *residual)
                            {
                                reqBuffCurrPos = reqBuffStartAddr;
                                *residual = 0;
                            }
                            else
                            {
                                reqBuffCurrPos += read;
                                *residual += (read - reqBuffOffset);
                            }

                            var wrote = _sysModule.SysCall(
                                sendto,
                                currFd,
                                (IntPtr)resBuffStartAddr,
                                resBuffFilledTotal,
                                0,
                                0,
                                0
                            );
                            if (wrote == resBuffFilledTotal)
                            {
                            }
                            else if (-wrote == EAGAIN || -wrote == EINTR)
                            {
                                reqBuffCurrPos = reqBuffStartAddr;
                                *residual = 0;
                                _networkModule.CloseConn(epFd, currFd);
                                break;
                            }
                            else
                            {
                                reqBuffCurrPos = reqBuffStartAddr;
                                *residual = 0;
                                _networkModule.CloseConn(epFd, currFd);
                                continue;
                            }
                        }
                        else if (-read == EAGAIN || -read == EINTR)
                        {
                            reqBuffCurrPos = reqBuffStartAddr;
                            *residual = 0;
                            _networkModule.CloseConn(epFd, currFd);
                        }
                    }
                }
            }
        }

        // A best effort no alloc cached function to convert sbyte* to a string holding a Http method.
        // A normal person would probably generate this.
        unsafe string MethodStr(sbyte* methodPtr, int methodLen)
        {
            const byte Null = (byte)'\0';
            const byte A = (byte)'A';
            const byte C = (byte)'C';
            const byte D = (byte)'D';
            const byte E = (byte)'E';
            const byte F = (byte)'F';
            const byte G = (byte)'G';
            const byte H = (byte)'H';
            const byte I = (byte)'I';
            const byte L = (byte)'L';
            const byte M = (byte)'M';
            const byte N = (byte)'N';
            const byte O = (byte)'O';
            const byte P = (byte)'P';
            const byte R = (byte)'R';
            const byte S = (byte)'S';
            const byte T = (byte)'T';
            const byte U = (byte)'U';

            var method = (byte*)methodPtr;

            var x0 = methodLen > 0 ? method[0] : Null;
            var x1 = methodLen > 1 ? method[1] : Null;
            var x2 = methodLen > 2 ? method[2] : Null;
            var x3 = methodLen > 3 ? method[3] : Null;
            var x4 = methodLen > 4 ? method[4] : Null;
            var x5 = methodLen > 5 ? method[5] : Null;
            var x6 = methodLen > 6 ? method[6] : Null;

            switch (x0)
            {
                case C:
                {
                    if (x1 == O && x2 == N && x3 == N && x4 == E && x5 == C && x6 == T)
                    {
                        return "CONNECT";
                    }
                    break;
                }
                case D:
                {
                    if (x1 == E && x2 == L && x3 == E && x4 == T && x5 == E)
                    {
                        return "DELETE";
                    }

                    break;
                }
                case E:
                {
                    break;
                }
                case F:
                {
                    break;
                }
                case G:
                {
                    if (x1 == E && x2 == T)
                    {
                        return "GET";
                    }

                    break;
                }
                case H:
                {
                    if (x1 == E && x2 == A && x3 == D)
                    {
                        return "HEAD";
                    }

                    break;
                }
                default:
                {
                    switch (x0)
                    {
                        case M:
                        {
                            if (x1 == E && x2 == T && x3 == H && x4 == O && x5 == D)
                            {
                                return "METHOD";
                            }

                            break;
                        }
                        case N:
                        {
                            break;
                        }
                        case O:
                        {
                            if (x1 == P && x2 == T && x3 == I && x4 == O && x5 == N && x6 == S)
                            {
                                return "OPTIONS";
                            }

                            break;
                        }
                        case P:
                        {
                            if (x1 == U && x2 == T)
                            {
                                return "PUT";
                            }

                            if (x1 == O && x2 == S && x3 == T)
                            {
                                return "POST";
                            }

                            if (x1 == A && x2 == T && x3 == C && x4 == H)
                            {
                                return "PATCH";
                            }

                            break;
                        }
                        default:
                        {
                            if (x0 != T || x1 != R || x2 != A || x3 != C || x4 != E)
                            {
                                break;
                            }

                            return "TRACE";
                        }
                    }

                    break;
                }
            }

            return Encoding.UTF8.GetString((byte*)methodPtr, methodLen);
        }

        // This method can't be stack allocated because Safari allows a path of up to 80K chars
        // TODO: Let the OP Threads provide pre allocated memory for this
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe string PathStr(sbyte* pathPtr, int pathLen)
        {
            var uMethodPtr = (byte*)pathPtr;
            return Encoding.UTF8.GetString(uMethodPtr, pathLen);
        }
    }
}