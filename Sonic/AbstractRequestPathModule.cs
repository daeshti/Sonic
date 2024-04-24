using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Sonic;

public interface IRequestPathModule
{
    unsafe int ParseRequestPathPipelinedSimd(sbyte* buffStart, int len, sbyte** method,
        int methodLen, sbyte** path, int pathLen);
}

public abstract class AbstractRequestPathModule : IRequestPathModule
{
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    protected struct AlignedPattern
    {
        public byte Item0;
        public byte Item1;

        public AlignedPattern(byte item0, byte item1)
        {
            Item0 = item0;
            Item1 = item1;
        }
    }

    protected static readonly AlignedPattern EolPattern = new(0x0D, 0x0A);

    protected abstract unsafe sbyte* FindSequenceSimd(sbyte* buffStart, sbyte* buffEnd);

    private const sbyte Space = (sbyte)' ';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int ParseRequestPathPipelinedSimd(sbyte* buffStart, int len, sbyte** method,
        int methodLen, sbyte** path, int pathLen)
    {
        var buff = buffStart;
        var buffEnd = buffStart + len;
        var i = 0;

        // The longest HTTP 1.1 request method is 7 characters, + 1 character for the space after
        while (i < 9)
        {
            if (*(buff + i) == Space)
            {
                *method = buff;
                methodLen = i;
                i++;
                while (*(buff + i) == Space)
                {
                    i++;
                }

                break;
            }

            i++;
        }

        buff += i;
        len -= i;
        i = 0;
        while (i < len)
        {
            if (*(buff + i) == Space)
            {
                *path = buff;
                pathLen = i;
                i++;
                while (*(buff + i) == Space)
                {
                    i++;
                }

                break;
            }

            i++;
        }

        if (pathLen == 0 || methodLen == 0)
        {
            return -1;
        }

        buff += i;
        while (buff < buffEnd)
        {
            buff = FindSequenceSimd(buff, buffEnd);
            if (*(uint*)buff == 0x0a0d0a0d)
            {
                buff += 4;
                return (int)(buff - buffStart);
            }
            else
            {
                buff += 2;
            }
        }

        return -2;
    }
}

public sealed class X86RequestPathModule : AbstractRequestPathModule
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override unsafe sbyte* FindSequenceSimd(sbyte* buffStart, sbyte* buffEnd)
    {
        var buff = buffStart;

        var eolPatt = EolPattern;

        // We only use two bytes as our pattern, and that is ok
        var sixteenBytePatt = Sse2.LoadVector128((sbyte*)&eolPatt);

        while (true)
        {
            var sixteenBytesFromBuff = Sse2.LoadVector128(buff);
            int foundAtBytePos = Sse2.CompareEqual(sixteenBytePatt, sixteenBytesFromBuff).GetElement(0);

            // If foundAtBytePos == 16, then we didn't find a match. We found a match if less than 16
            if (foundAtBytePos != 16)
            {
                // Increment buf by foundAtBytePos, which is the position in the 16 byte search
                buff += foundAtBytePos;
                return buff;
            }

            // Increment by 15 bytes instead of 16 bytes to ensure \r\n is never split/overlaps between searches
            buff += 15;
            if (buff >= buffEnd)
            {
                break;
            }
        }

        return buffEnd;
    }
}

public sealed class ArmRequestPathModule : AbstractRequestPathModule
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override unsafe sbyte* FindSequenceSimd(sbyte* buffStart, sbyte* buffEnd)
    {
        var buff = buffStart;

        var eolPatt = AbstractRequestPathModule.EolPattern;

        // We only use two bytes as our pattern, and that is ok
        var sixteenBytePatt = AdvSimd.LoadVector128((sbyte*)&eolPatt);

        while (true)
        {
            var sixteenBytesFromBuff = AdvSimd.LoadVector128(buff);
            int foundAtBytePos = AdvSimd.CompareEqual(sixteenBytesFromBuff, sixteenBytePatt).GetElement(0);

            // If foundAtBytePos == 16, then we didn't find a match. We found a match if less than 16
            if (foundAtBytePos != 16)
            {
                // Increment buf by foundAtBytePos, which is the position in the 16 byte search
                buff += foundAtBytePos;
                return buff;
            }

            // Increment by 15 bytes instead of 16 bytes to ensure \r\n is never split/overlaps between searches
            buff += 15;
            if (buff >= buffEnd)
            {
                break;
            }
        }

        return buffEnd;
    }
}