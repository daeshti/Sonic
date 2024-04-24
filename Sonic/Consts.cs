namespace Sonic;

public static class Consts
{
#if W32
    public const int PtrWidthInBits = 32;
#else
    internal const int PtrWidthInBits = 64;
#endif
    /// We always want a total of 1024 bits, so 16 segments on 64-bit platforms, 32 segments on 32-bit platforms
    internal const int CpuSetLen = 1024 / PtrWidthInBits;
    
    internal const int MaxConnPerThrd = 1024;
    
    internal const int MaxEpollEsRet = (4096 / 12) - 1; // 12 being sizeof(Epoll.epoll_event)

    internal const int ReqBufSize = 4096;
    
    internal const int ResBufSize = 4096;
}