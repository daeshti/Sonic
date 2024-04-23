/*
 * Global using directives belong here.
 * The file name only exists to scare my brother away.
 */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sonic;

// ReSharper disable once InconsistentNaming
[InlineArray(8)]
public struct u8x8
{
    private Byte _element0;
}

[InlineArray(35)]
public struct u8x35
{
    private Byte _element0;
}

[InlineArray(Consts.ResBufSize)]
internal struct u8x4096
{
    private Byte _element0;
}

[InlineArray(Consts.MaxConnPerThrd)]
internal struct i64x1048
{
    private Int64 _element0;
}

[StructLayout(LayoutKind.Sequential)]
internal struct timespec
{
    public Int64 tv_sec; // Seconds
    public Int64 tv_nsec; // Nanoseconds [0, 999'999'999]
}

[InlineArray(Consts.CpuSetLen)]
internal struct cpu_set_data_t
{
    private UInt64 _element0;
}
    
[StructLayout(LayoutKind.Sequential, Pack = 64)]
internal struct cpu_set_t
{
    public cpu_set_data_t Data;
}


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct in_addr
{
    public UInt32 s_addr;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct sockaddr_in
{
    public UInt16 sin_family;
    public UInt16 sin_port;
    public in_addr sin_addr;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public u8x8 sin_zero;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct linger
{
    public Int32 l_onoff;
    public Int32 l_linger;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct sock_filter
{
    public UInt16 code;
    public Byte jt;
    public Byte jf;
    public UInt32 k;
}

[InlineArray(2)]
public struct sock_filter_x2
{
    private sock_filter _element0;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct sock_fprog
{
    public UInt16 len;
    public Int64 filterPtr; // We'll be a stack pointer, so no need for management
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
    public UInt32 events;
    public epoll_data Data;
}

#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.