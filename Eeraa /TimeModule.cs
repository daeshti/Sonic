using static Eeraan.ISysModule.SysCallCtrl;
using static Eeraan.ISysModule.SysCallNum;

namespace Eeraan;


public interface ITimeModule
{
    /// <summary>
    ///  Returns a stack allocated byte buffer with value:
    /// "Date: Thu, 01 Jan 1970 00:00:00 GMT" in UTF8.
    /// </summary>
    /// <returns></returns>
    public u8x35 EpochAsUtf8Buff();

    /// <summary>
    /// This is a no allocation implementation of
    /// DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", System.Globalization.CultureInfo.InvariantCulture);
    /// </summary>
    /// <param name="res">The stack memory allocated for the string</param>
    public void UtcNowHttpStr(ref u8x35 res);
}

/// <inheritdoc cref="ITimeModule"/>

public sealed class TimeModule : ITimeModule
{
    private static readonly u8x35 EpochAsUtf8BuffCached;

    static TimeModule()
    {
        EpochAsUtf8BuffCached = new u8x35();
        EpochAsUtf8BuffCached[0] = (u8)'D';
        EpochAsUtf8BuffCached[1] = (u8)'a';
        EpochAsUtf8BuffCached[2] = (u8)'t';
        EpochAsUtf8BuffCached[3] = (u8)'e';
        EpochAsUtf8BuffCached[4] = (u8)':';
        EpochAsUtf8BuffCached[5] = (u8)' ';
        EpochAsUtf8BuffCached[6] = (u8)' ';
        EpochAsUtf8BuffCached[7] = (u8)' ';
        EpochAsUtf8BuffCached[8] = (u8)' ';
        EpochAsUtf8BuffCached[9] = (u8)',';
        EpochAsUtf8BuffCached[10] = (u8)' ';
        EpochAsUtf8BuffCached[11] = (u8)'0';
        EpochAsUtf8BuffCached[12] = (u8)'0';
        EpochAsUtf8BuffCached[13] = (u8)' ';
        EpochAsUtf8BuffCached[14] = (u8)' ';
        EpochAsUtf8BuffCached[15] = (u8)' ';
        EpochAsUtf8BuffCached[16] = (u8)' ';
        EpochAsUtf8BuffCached[17] = (u8)' ';
        EpochAsUtf8BuffCached[18] = (u8)'0';
        EpochAsUtf8BuffCached[19] = (u8)'0';
        EpochAsUtf8BuffCached[20] = (u8)'0';
        EpochAsUtf8BuffCached[21] = (u8)'0';
        EpochAsUtf8BuffCached[22] = (u8)' ';
        EpochAsUtf8BuffCached[23] = (u8)'0';
        EpochAsUtf8BuffCached[24] = (u8)'0';
        EpochAsUtf8BuffCached[25] = (u8)':';
        EpochAsUtf8BuffCached[26] = (u8)'0';
        EpochAsUtf8BuffCached[27] = (u8)'0';
        EpochAsUtf8BuffCached[28] = (u8)':';
        EpochAsUtf8BuffCached[29] = (u8)'0';
        EpochAsUtf8BuffCached[30] = (u8)'0';
        EpochAsUtf8BuffCached[31] = (u8)' ';
        EpochAsUtf8BuffCached[32] = (u8)'G';
        EpochAsUtf8BuffCached[33] = (u8)'M';
        EpochAsUtf8BuffCached[34] = (u8)'T';
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
    public u8x35 EpochAsUtf8Buff()
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
    public void UtcNowHttpStr(ref u8x35 res)
    {
        var secsSinceEpoch = SecsSinceEpoch();
        
        const i64 leapEpoch = 11017;
        const i64 daysPer400Years = 365 * 400 + 97;
        const i64 daysPer100Years = 365 * 100 + 24;
        const i64 daysPer4Years = 365 * 4 + 1;

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

        ReadOnlySpan<i64> monthLengths = stackalloc i64[] { 31, 30, 31, 30, 31, 31, 30, 31, 30, 31, 31, 29 };
        i64 month = 0;
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

        var sec = (u8)(secsOfDay % 60);
        var min = (u8)((secsOfDay % 3600) / 60);
        var hour = (u8)(secsOfDay / 3600);
        var day = (u8)mDay;
        var monthByte = (u8)month;
        var yearUshort = (ushort)year;
        var weekDayByte = (u8)weekDay;


        var weekDayStr = DayOfWeek(weekDayByte);
        var monthStr = Month(monthByte);

        res[6] = (u8)weekDayStr[0];
        res[7] = (u8)weekDayStr[1];
        res[8] = (u8)weekDayStr[2];

        res[11] = (u8)(day / 10 + '0');
        res[12] = (u8)(day % 10 + '0');

        res[14] = (u8)monthStr[0];
        res[15] = (u8)monthStr[1];
        res[16] = (u8)monthStr[2];

        res[18] = (u8)(yearUshort / 1000 + '0');
        res[19] = (u8)((yearUshort / 100) % 10 + '0');
        res[20] = (u8)((yearUshort / 10) % 10 + '0');
        res[21] = (u8)(yearUshort % 10 + '0');

        res[23] = (u8)(hour / 10 + '0');
        res[24] = (u8)(hour % 10 + '0');

        res[26] = (u8)(min / 10 + '0');
        res[27] = (u8)(min % 10 + '0');

        res[29] = (u8)(sec / 10 + '0');
        res[30] = (u8)(sec % 10 + '0');
        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        i64 SecsSinceEpoch()
        {
            timespec ts = new();
            
            isize resPtr;
            unsafe
            {
                resPtr = (isize)(&ts);
            }

            _sysModule.SysCall(clock_gettime, CLOCK_REALTIME, resPtr);

            return ts.tv_sec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string DayOfWeek(i32 index)
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
        string Month(i32 index)
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
