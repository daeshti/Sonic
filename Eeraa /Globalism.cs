/*
 * Global using directives belong here.
 * The file name only exists to scare my brother away.
 */

global using i8 = sbyte;
global using i16 = short;
global using i32 = int;
global using i64 = long;
global using u8 = byte;
global using u16 = ushort;
global using u32 = uint;
global using u64 = ulong;

/*
 * There's no way these bite me back in the future...
 */
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
global using isize = nint;
global using usize = nuint;

global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using static Eeraan.Consts;

// ReSharper disable once InconsistentNaming
[InlineArray(8)]
public struct u8x8
{
    private u8 _element0;
}

[InlineArray(35)]
public struct u8x35
{
    private u8 _element0;
}

[InlineArray(ResBufSize)]
internal struct u8x4096
{
    private u8 _element0;
}

[InlineArray(MaxConnPerThrd)]
internal struct i64x1048
{
    private i64 _element0;
}

[StructLayout(LayoutKind.Sequential)]
internal struct timespec
{
    public i64 tv_sec; // Seconds
    public i64 tv_nsec; // Nanoseconds [0, 999'999'999]
}

[InlineArray(CpuSetLen)]
internal struct cpu_set_data_t
{
    private u64 _element0;
}
    
[StructLayout(LayoutKind.Sequential, Pack = 64)]
internal struct cpu_set_t
{
    public cpu_set_data_t Data;
}


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct in_addr
{
    public u32 s_addr;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct sockaddr_in
{
    public u16 sin_family;
    public u16 sin_port;
    public in_addr sin_addr;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public u8x8 sin_zero;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct linger
{
    public i32 l_onoff;
    public i32 l_linger;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct sock_filter
{
    public u16 code;
    public u8 jt;
    public u8 jf;
    public u32 k;
}

[InlineArray(2)]
public struct sock_filter_x2
{
    private sock_filter _element0;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct sock_fprog
{
    public u16 len;
    public i64 filterPtr; // We'll be a stack pointer, so no need for management
}

[StructLayout(LayoutKind.Explicit)]
internal struct epoll_data
{
    [FieldOffset(0)] public nint ptr;
    [FieldOffset(0)] public int fd;
    [FieldOffset(0)] public uint uint32_t;
    [FieldOffset(0)] public ulong uint64_t;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct epoll_event
{
    public u32 events;
    public epoll_data Data;
}

#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
