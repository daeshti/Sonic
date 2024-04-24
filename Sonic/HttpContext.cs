namespace Sonic;

// TODO: don't expose unsolicited raw pointers.
public unsafe struct HttpContext
{
    public sbyte* Method;
    public int MethodLen;
    public sbyte* Path;
    public int PathLen;
    public ByteX35 Date;
    public IntPtr ResBuff;
}