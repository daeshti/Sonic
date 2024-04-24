using System.Runtime.CompilerServices;

namespace Sonic;

public static class PrimitiveExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UIntPtr USize(this long self)
    {
        return (UIntPtr)self;
    }
    
}