using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Sonic.ISysModule.SysCallCtrl;
using static Sonic.ISysModule.SysCallNum;

namespace Sonic;

public sealed class EpModule
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
    public void Run(ushort port, Action<string, string, IntPtr, ByteX35> callBack)
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
                ThreadStart(port, callBack, coreId, coreCnt, cntDown);
            }, maxStackSize: 1024 * 1024 * 8)
            {
                Name = $"ecbatana{c}",
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
        Action<string, string, IntPtr, ByteX35> callBack,
        int coreId,
        int coreCnt,
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
                                    callBack(
                                        methodStr,
                                        pathStr,
                                        (IntPtr)resBuffStartAddr + resBuffFilledTotal,
                                        _date
                                    );
                                    var resBuffFilled = 0;

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

        // a best afford no alloc cached function to convert sbyte* to string holding a Http method.
        unsafe string MethodStr(sbyte* methodPtr, int methodLen)
        {
            switch (methodLen)
            {
                case 3:
                {
                    var a = methodPtr[0];
                    var b = methodPtr[1];
                    var c = methodPtr[2];
                    switch (a)
                    {
                        case (sbyte)'G' when b == (sbyte)'E' && c == (sbyte)'T':
                            return "GET";
                        case (sbyte)'P' when b == (sbyte)'U' && c == (sbyte)'T':
                            return "PUT";
                    }

                    goto default;
                }
                case 4:
                {
                    var a = methodPtr[0];
                    var b = methodPtr[1];
                    var c = methodPtr[2];
                    var d = methodPtr[3];
                    switch (a)
                    {
                        case (sbyte)'H' when b == (sbyte)'E' && c == (sbyte)'A' && d == (sbyte)'D':
                            return "HEAD";
                        case (sbyte)'P' when b == (sbyte)'O' && c == (sbyte)'S' && d == (sbyte)'T':
                            return "POST";
                    }

                    goto default;
                }
                case 5:
                {
                    var a = methodPtr[0];
                    var b = methodPtr[1];
                    var c = methodPtr[2];
                    var d = methodPtr[3];
                    var e = methodPtr[4];
                    switch (a)
                    {
                        case (sbyte)'P' when b == (sbyte)'A' && c == (sbyte)'T' && d == (sbyte)'C' && e == (sbyte)'H':
                            return "PATCH";
                        case (sbyte)'T' when b == (sbyte)'R' && c == (sbyte)'A' && d == (sbyte)'C' && e == (sbyte)'E':
                            return "TRACE";
                    }

                    goto default;
                }

                case 6:
                {
                    var a = methodPtr[0];
                    var b = methodPtr[1];
                    var c = methodPtr[2];
                    var d = methodPtr[3];
                    var e = methodPtr[4];
                    var f = methodPtr[5];
                    switch (a)
                    {
                        case (sbyte)'D' when b == (sbyte)'E' && c == (sbyte)'L' && d == (sbyte)'E'
                                             && e == (sbyte)'T' && f == (sbyte)'E':
                            return "DELETE";
                        case (sbyte)'M' when b == (sbyte)'E' && c == (sbyte)'T' && d == (sbyte)'H'
                                             && e == (sbyte)'O' && f == (sbyte)'D':
                            return "METHOD";
                    }

                    goto default;
                }
                case 7:
                {
                    var a = methodPtr[0];
                    var b = methodPtr[1];
                    var c = methodPtr[2];
                    var d = methodPtr[3];
                    var e = methodPtr[4];
                    var f = methodPtr[5];
                    var g = methodPtr[6];
                    switch (a)
                    {
                        case (sbyte)'C' when b == (sbyte)'O' && c == (sbyte)'N' && d == (sbyte)'N'
                                             && e == (sbyte)'E' && f == (sbyte)'C' && g == (sbyte)'T':
                            return "CONNECT";
                        case (sbyte)'O' when b == (sbyte)'P' && c == (sbyte)'T' && d == (sbyte)'I'
                                             && e == (sbyte)'O' && f == (sbyte)'N' && g == (sbyte)'S':
                            return "OPTIONS";
                    }

                    goto default;
                }

                default:
                {
                    var uMethodPtr = (byte*)methodPtr;
                    return Encoding.UTF8.GetString(uMethodPtr, methodLen);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe string PathStr(sbyte* pathPtr, int pathLen)
        {
            var uMethodPtr = (byte*)pathPtr;
            return Encoding.UTF8.GetString(uMethodPtr, pathLen);
        }
    }
}