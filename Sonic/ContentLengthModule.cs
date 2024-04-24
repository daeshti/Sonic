namespace Sonic;

public interface IContentLengthModule
{
    long LongToAscii(ref Span<byte> buff, ulong value);
}

public sealed class ContentLengthModule : IContentLengthModule
{
    public static readonly char[] DigitsLutInChar =
    [
        '0', '0', '0', '1', '0', '2', '0', '3', '0', '4', '0', '5', '0', '6', '0', '7', '0', '8', '0', '9', '1', '0', '1',
        '1', '1', '2', '1', '3', '1', '4', '1', '5', '1', '6', '1', '7', '1', '8', '1', '9', '2', '0', '2', '1', '2', '2',
        '2', '3', '2', '4', '2', '5', '2', '6', '2', '7', '2', '8', '2', '9', '3', '0', '3', '1', '3', '2', '3', '3', '3',
        '4', '3', '5', '3', '6', '3', '7', '3', '8', '3', '9', '4', '0', '4', '1', '4', '2', '4', '3', '4', '4', '4', '5',
        '4', '6', '4', '7', '4', '8', '4', '9', '5', '0', '5', '1', '5', '2', '5', '3', '5', '4', '5', '5', '5', '6', '5',
        '7', '5', '8', '5', '9', '6', '0', '6', '1', '6', '2', '6', '3', '6', '4', '6', '5', '6', '6', '6', '7', '6', '8',
        '6', '9', '7', '0', '7', '1', '7', '2', '7', '3', '7', '4', '7', '5', '7', '6', '7', '7', '7', '8', '7', '9', '8',
        '0', '8', '1', '8', '2', '8', '3', '8', '4', '8', '5', '8', '6', '8', '7', '8', '8', '8', '9', '9', '0', '9', '1',
        '9', '2', '9', '3', '9', '4', '9', '5', '9', '6', '9', '7', '9', '8', '9', '9'
    ];

    public static readonly ByteX200 DigitsLut;

    static ContentLengthModule()
    {
        for (var i = 0; i < 200; i++)
        {
            DigitsLut[i] = (byte) DigitsLutInChar[i];
        }
    }
    
    public long LongToAscii(ref Span<byte> buff, ulong value)
    {
        var index = 0;

        if (value >= 100000000) return index;
        
        var v = (int)value;
        if (v < 10000)
        {
            var d1 = (v / 100) << 1;
            var d2 = (v % 100) << 1;

            if (v >= 1000)
            {
                buff[index] = DigitsLut[d1];
                index++;
            }

            if (v >= 100)
            {
                buff[index] = DigitsLut[d1 + 1];
                index++;
            }

            if (v >= 10)
            {
                buff[index] = DigitsLut[d2];
                index++;
            }

            buff[index] = DigitsLut[d2 + 1];
            index++;
        }
        else
        {
            var b = v / 10000;
            var c = v % 10000;

            var d1 = (b / 100) << 1;
            var d2 = (b % 100) << 1;

            var d3 = (c / 100) << 1;
            var d4 = (c % 100) << 1;

            if (value >= 10000000)
            {
                buff[index] = DigitsLut[d1];
                index++;
            }

            if (value >= 1000000)
            {
                buff[index] = DigitsLut[d1 + 1];
                index++;
            }

            if (value >= 100000)
            {
                buff[index] = DigitsLut[d2];
                index++;
            }

            buff[index] = DigitsLut[d2 + 1];
            index++;

            buff[index] = DigitsLut[d3];
            index++;

            buff[index] = DigitsLut[d3 + 1];
            index++;

            buff[index] = DigitsLut[d4];
            index++;

            buff[index] = DigitsLut[d4 + 1];
            index++;
        }

        return index;
    }


}