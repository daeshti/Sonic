namespace Eeraan;

public static class Consts
{
#if W32
    public const int PtrWidthInBits = 32;
#else
    internal const i32 PtrWidthInBits = 64;
#endif
    /// We always want a total of 1024 bits, so 16 segments on 64-bit platforms, 32 segments on 32-bit platforms
    internal const i32 CpuSetLen = 1024 / PtrWidthInBits;
    
    internal const i32 MaxConnPerThrd = 1024;
    
    internal const i32 MaxEpollEsRet = (4096 / 12) - 1; // 12 being sizeof(Epoll.epoll_event)

    internal const i32 ReqBufSize = 4096;
    
    internal const i32 ResBufSize = 4096;
}