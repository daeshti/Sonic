namespace Eeraan;

public static class PrimitiveExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static usize USize(this i64 self)
    {
        return (usize)self;
    }
    
}