using System.Runtime.CompilerServices;
using static Sonic.ISysModule.SysCallCtrl;
using static Sonic.ISysModule.SysCallNum;

namespace Sonic;


public interface ITimeModule
{
    /// <summary>
    ///  Returns a stack allocated byte buffer with value:
    /// "Date: Thu, 01 Jan 1970 00:00:00 GMT" in UTF8.
    /// </summary>
    /// <returns></returns>
    public ByteX35 EpochAsUtf8Buff();

    /// <summary>
    /// This is a no allocation implementation of
    /// DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", System.Globalization.CultureInfo.InvariantCulture);
    /// </summary>
    /// <param name="res">The stack memory allocated for the string</param>
    public void UtcNowHttpStr(ref ByteX35 res);
}

/// <inheritdoc cref="ITimeModule"/>

public sealed class TimeModule : ITimeModule
{
    private static readonly ByteX35 EpochAsUtf8BuffCached;

    static TimeModule()
    {
        EpochAsUtf8BuffCached = new ByteX35();
        EpochAsUtf8BuffCached[0] = (byte)'D';
        EpochAsUtf8BuffCached[1] = (byte)'a';
        EpochAsUtf8BuffCached[2] = (byte)'t';
        EpochAsUtf8BuffCached[3] = (byte)'e';
        EpochAsUtf8BuffCached[4] = (byte)':';
        EpochAsUtf8BuffCached[5] = (byte)' ';
        EpochAsUtf8BuffCached[6] = (byte)' ';
        EpochAsUtf8BuffCached[7] = (byte)' ';
        EpochAsUtf8BuffCached[8] = (byte)' ';
        EpochAsUtf8BuffCached[9] = (byte)',';
        EpochAsUtf8BuffCached[10] = (byte)' ';
        EpochAsUtf8BuffCached[11] = (byte)'0';
        EpochAsUtf8BuffCached[12] = (byte)'0';
        EpochAsUtf8BuffCached[13] = (byte)' ';
        EpochAsUtf8BuffCached[14] = (byte)' ';
        EpochAsUtf8BuffCached[15] = (byte)' ';
        EpochAsUtf8BuffCached[16] = (byte)' ';
        EpochAsUtf8BuffCached[17] = (byte)' ';
        EpochAsUtf8BuffCached[18] = (byte)'0';
        EpochAsUtf8BuffCached[19] = (byte)'0';
        EpochAsUtf8BuffCached[20] = (byte)'0';
        EpochAsUtf8BuffCached[21] = (byte)'0';
        EpochAsUtf8BuffCached[22] = (byte)' ';
        EpochAsUtf8BuffCached[23] = (byte)'0';
        EpochAsUtf8BuffCached[24] = (byte)'0';
        EpochAsUtf8BuffCached[25] = (byte)':';
        EpochAsUtf8BuffCached[26] = (byte)'0';
        EpochAsUtf8BuffCached[27] = (byte)'0';
        EpochAsUtf8BuffCached[28] = (byte)':';
        EpochAsUtf8BuffCached[29] = (byte)'0';
        EpochAsUtf8BuffCached[30] = (byte)'0';
        EpochAsUtf8BuffCached[31] = (byte)' ';
        EpochAsUtf8BuffCached[32] = (byte)'G';
        EpochAsUtf8BuffCached[33] = (byte)'M';
        EpochAsUtf8BuffCached[34] = (byte)'T';
    }

    private readonly ISysModule _sysModule;

    public TimeModule(ISysModule sysModule)
    {
        _sysModule = sysModule;
    }

    /// <summary>
    ///  Returns a stack allocated byte buffer with value:
    /// "Date: Thu, 01 Jan 1970 00:00:00 GMT" in UTF8.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteX35 EpochAsUtf8Buff()
    {
        // This compiles to memcpy since b35 is a struct.
        return EpochAsUtf8BuffCached;
    }

    /// <summary>
    /// This is a no allocation implementation of
    /// DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", System.Globalization.CultureInfo.InvariantCulture);
    /// </summary>
    /// <param name="res">The stack memory allocated for the string</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UtcNowHttpStr(ref ByteX35 res)
    {
        var secsSinceEpoch = SecsSinceEpoch();
        
        const long leapEpoch = 11017;
        const long daysPer400Years = 365 * 400 + 97;
        const long daysPer100Years = 365 * 100 + 24;
        const long daysPer4Years = 365 * 4 + 1;

        var days = (secsSinceEpoch / 86400) - leapEpoch;
        var secsOfDay = secsSinceEpoch % 86400;

        var qcCycles = days / daysPer400Years;
        var remDays = days % daysPer400Years;

        if (remDays < 0)
        {
            remDays += daysPer400Years;
            qcCycles -= 1;
        }

        var cCycles = remDays / daysPer100Years;
        if (cCycles == 4)
        {
            cCycles -= 1;
        }

        remDays -= cCycles * daysPer100Years;

        var qCycles = remDays / daysPer4Years;
        if (qCycles == 25)
        {
            qCycles -= 1;
        }

        remDays -= qCycles * daysPer4Years;

        var remYears = remDays / 365;
        if (remYears == 4)
        {
            remYears -= 1;
        }

        remDays -= remYears * 365;

        var year = 2000 + remYears + 4 * qCycles + 100 * cCycles + 400 * qcCycles;

        ReadOnlySpan<long> monthLengths = stackalloc long[] { 31, 30, 31, 30, 31, 31, 30, 31, 30, 31, 31, 29 };
        long month = 0;
        foreach (var monthLength in monthLengths)
        {
            month += 1;
            if (remDays < monthLength)
            {
                break;
            }

            remDays -= monthLength;
        }

        var mDay = remDays + 1;
        month = (month + 2) > 12 ? month - 10 : month + 2;

        var weekDay = (3 + days) % 7;
        if (weekDay <= 0)
        {
            weekDay += 7;
        }

        var sec = (byte)(secsOfDay % 60);
        var min = (byte)((secsOfDay % 3600) / 60);
        var hour = (byte)(secsOfDay / 3600);
        var day = (byte)mDay;
        var monthByte = (byte)month;
        var yearUshort = (ushort)year;
        var weekDayByte = (byte)weekDay;


        var weekDayStr = DayOfWeek(weekDayByte);
        var monthStr = Month(monthByte);

        res[6] = (byte)weekDayStr[0];
        res[7] = (byte)weekDayStr[1];
        res[8] = (byte)weekDayStr[2];

        res[11] = (byte)(day / 10 + '0');
        res[12] = (byte)(day % 10 + '0');

        res[14] = (byte)monthStr[0];
        res[15] = (byte)monthStr[1];
        res[16] = (byte)monthStr[2];

        res[18] = (byte)(yearUshort / 1000 + '0');
        res[19] = (byte)((yearUshort / 100) % 10 + '0');
        res[20] = (byte)((yearUshort / 10) % 10 + '0');
        res[21] = (byte)(yearUshort % 10 + '0');

        res[23] = (byte)(hour / 10 + '0');
        res[24] = (byte)(hour % 10 + '0');

        res[26] = (byte)(min / 10 + '0');
        res[27] = (byte)(min % 10 + '0');

        res[29] = (byte)(sec / 10 + '0');
        res[30] = (byte)(sec % 10 + '0');
        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long SecsSinceEpoch()
        {
            timespec ts = new();
            
            IntPtr resPtr;
            unsafe
            {
                resPtr = (IntPtr)(&ts);
            }

            _sysModule.SysCall(clock_gettime, CLOCK_REALTIME, resPtr);

            return ts.tv_sec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string DayOfWeek(int index)
        {
            return index switch
            {
                1 => "Mon",
                2 => "Tue",
                3 => "Wed",
                4 => "Thu",
                5 => "Fri",
                6 => "Sat",
                7 => "Sun",
                _ => "Invalid"
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string Month(int index)
        {
            return index switch
            {
                1 => "Jan",
                2 => "Feb",
                3 => "Mar",
                4 => "Apr",
                5 => "May",
                6 => "Jun",
                7 => "Jul",
                8 => "Aug",
                9 => "Sep",
                10 => "Oct",
                11 => "Nov",
                12 => "Dec",
                _ => "Invalid"
            };
        }
    }
}
