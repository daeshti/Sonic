using static Eeraan.ISysModule.SysCallCtrl;
using static Eeraan.ISysModule.SysCallNum;

namespace Eeraan;

public class EpModule
{
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    private struct AlignedHttpDate
    {
        public u8x35 Flattened;
    }

    [InlineArray(MaxEpollEsRet)]
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

    [InlineArray(ReqBufSize * MaxConnPerThrd)]
    internal struct AlignedRequestBufferData
    {
        private u8 _element0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    internal struct AlignedRequestBuffer
    {
        public AlignedRequestBufferData Flatten;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    internal struct AlignedResultBuffer
    {
        public u8x4096 Flatten;
    }

    private readonly ISysModule _sysModule;
    private readonly ITimeModule _timeModule;
    private readonly IProcessorModule _processorModule;
    private readonly INetworkModule _networkModule;
    
    private u8x35 _date;

    public EpModule(
        ISysModule sysModule, ITimeModule timeModule, 
        IProcessorModule processorModule, INetworkModule networkModule)
    {
        _sysModule = sysModule;
        _timeModule = timeModule;
        _processorModule = processorModule;
        _networkModule = networkModule;

        _date = new u8x35();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Run(u16 port, Action<string, i64, string, i64, string, string> callBack)
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
        u16 port,
        Action<string, i64, string, i64, string, string> callBack,
        i32 coreId,
        i32 coreCnt,
        CountdownEvent cntDown
    )
    {
        var listenerFd = (i32) _networkModule.GetListenerFd(port);
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
                events = (u32) EPOLLIN
            }
        };
        isize epEListenerPtr;
        unsafe
        {
            epEListenerPtr = (isize)(&epEListener);
        }

        _sysModule.SysCall(epoll_ctl, epFd, EPOLL_CTL_ADD, listenerFd, epEListenerPtr);

        var epEs = new AlignedEpollEvents();
        isize epEsFPtr;
        unsafe
        {
            epEsFPtr = (isize)(&epEs.Flatten);
        }

        var savedEs = new AlignedEpollEvent();
        savedEs.Flatten.events = (u32)EPOLLIN;
        
        var reqBuff = new AlignedRequestBuffer();
        
        // Init state for tracking request buffer position across events
        var reqBuffCurrAddr = new i64x1048();
        isize reqBuffCurrAddrPtr;
        unsafe
        {
            reqBuffCurrAddrPtr = (isize) (&reqBuff.Flatten[0]);
        }
        for (var i = 0; i < MaxConnPerThrd; i++)
        {
            reqBuffCurrAddr[i] = reqBuffCurrAddrPtr + i * ReqBufSize;
        }
        
        var reqBuffResidual = new i64x1048();
        
        
        var resBuff = new AlignedResultBuffer();
        i64 resBuffStartAddr;
        unsafe
        {
            resBuffStartAddr = (i64)(&resBuff.Flatten[0]);
        }

        var epWaitType = EPOLL_TIMEOUT_BLOCKING;

        while (true)
        {
            var incEsCnt =
                _sysModule.SysCall(
                    epoll_wait,
                    epFd,
                    epEsFPtr,
                    MaxEpollEsRet
                );
            if (incEsCnt <= 0)
            {
                epWaitType = EPOLL_TIMEOUT_BLOCKING;
                continue;
            }

            epWaitType = EPOLL_TIMEOUT_IMMEDIATE_RETURN;
            
            for (i32 i = 0; i < incEsCnt; i++)
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
                    var reqBuffStartAddr = (isize) (&reqBuff.Flatten[0]) + currFd * ReqBufSize;
                    
                    // DANGER
                    var reqBuffCurrPos = (isize) (&reqBuffCurrAddr[currFd]) + currFd;
                    
                    // DANGER
                    var residual = &reqBuffResidual[currFd];

                    if (currFd == listenerFd)
                    {
                        var incomingFd = _sysModule.SysCall(accept, listenerFd, 0, 0);

                        if (incomingFd is >= 0 and < MaxConnPerThrd)
                        {
                            // DANGER
                            reqBuffCurrPos = reqBuffStartAddr;
                            
                            // DANGER
                            *residual = 0;
                            
                            _networkModule.SetupConn(incomingFd);
                            savedEs.Flatten.Data.fd = (i32)incomingFd;

                            _sysModule.SysCall(
                                epoll_ctl,
                                epFd,
                                EPOLL_CTL_ADD,
                                incomingFd,
                                (isize)(&savedEs.Flatten)
                            );
                        }
                        else
                        {
                            _networkModule.CloseConn((i32)epFd, currFd);
                        }
                    }
                    else
                    {
                        // DANGER
                        var buffRem = ReqBufSize - (isize)reqBuffCurrPos - (isize)reqBuffStartAddr;

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
                            var reqBuffOffset = 0;
                            var resBuffFilledTotal = 0;

                            while (reqBuffOffset != (read + *residual))
                            {
                                i8* method = null;
                                var methodLen = 0;
                                i8* path = null;
                                var pathLen = 0;

                                throw new NotImplementedException();
                            }
                        }
                    }
                }
            }
        }
        
        
    }
    
}