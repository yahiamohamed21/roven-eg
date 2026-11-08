using System;
using System.Globalization;

namespace Stockify.Common.Extensions;

public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo EgyptTimeZone = ResolveEgyptTimeZone();

    private static TimeZoneInfo ResolveEgyptTimeZone()
    {
        try
        {
            // Windows Timezone Identifier for Cairo / Egypt
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // IANA / Linux / Docker Timezone Identifier
                return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            }
            catch
            {
                // Safe fallback to system local timezone
                return TimeZoneInfo.Local;
            }
        }
    }

    /// <summary>
    /// Gets the cached TimeZoneInfo for Egypt (UTC+02:00 / UTC+03:00 during DST).
    /// </summary>
    public static TimeZoneInfo EgyptZone => EgyptTimeZone;

    /// <summary>
    /// Gets the current date and time in Egypt / Cairo.
    /// </summary>
    public static DateTime EgyptNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EgyptTimeZone);

    /// <summary>
    /// Converts a UTC or unspecified DateTime to Egypt Local Time (Cairo).
    /// </summary>
    public static DateTime ToEgyptTime(this DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue)
            return dateTime;

        DateTime utc = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, EgyptTimeZone);
    }

    /// <summary>
    /// Nullable overload for ToEgyptTime.
    /// </summary>
    public static DateTime? ToEgyptTime(this DateTime? dateTime)
    {
        return dateTime.HasValue ? dateTime.Value.ToEgyptTime() : null;
    }

    /// <summary>
    /// Converts an Egypt local DateTime to UTC (ideal for date-range database queries).
    /// </summary>
    public static DateTime ToUtcFromEgypt(this DateTime egyptDateTime)
    {
        if (egyptDateTime == DateTime.MinValue || egyptDateTime == DateTime.MaxValue)
            return egyptDateTime;

        var unspecified = DateTime.SpecifyKind(egyptDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, EgyptTimeZone);
    }

    /// <summary>
    /// Nullable overload for ToUtcFromEgypt.
    /// </summary>
    public static DateTime? ToUtcFromEgypt(this DateTime? egyptDateTime)
    {
        return egyptDateTime.HasValue ? egyptDateTime.Value.ToUtcFromEgypt() : null;
    }

    /// <summary>
    /// Formats a DateTime in Egypt Local Time (e.g. "06 Nov 2026, 01:15 AM").
    /// </summary>
    public static string ToEgyptTimeString(this DateTime dateTime, string format = "dd MMM yyyy, hh:mm tt")
    {
        if (dateTime == DateTime.MinValue) return "-";
        return dateTime.ToEgyptTime().ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Nullable overload for ToEgyptTimeString.
    /// </summary>
    public static string ToEgyptTimeString(this DateTime? dateTime, string format = "dd MMM yyyy, hh:mm tt")
    {
        return dateTime.HasValue ? dateTime.Value.ToEgyptTimeString(format) : "-";
    }
}

