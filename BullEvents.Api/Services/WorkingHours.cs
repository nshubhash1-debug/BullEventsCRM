using BullEvents.Api.Models;

namespace BullEvents.Api.Services;

/// <summary>
/// Counts elapsed <em>working</em> time against a set of business hours.
///
/// This is the whole reason business hours exist. "Respond within four hours"
/// means four working hours: a lead that arrives at six on Friday evening is
/// not late by Saturday lunchtime, and an SLA that says otherwise trains
/// everyone to ignore it. Getting this wrong does not produce a subtle bug — it
/// produces an alert channel nobody reads.
///
/// Walks day by day rather than doing modular arithmetic over the week. A week
/// has seven different opening times, holidays land on arbitrary dates, and the
/// closed-form version of this is where the off-by-one lives.
/// </summary>
public static class WorkingHours
{
    /// <summary>How many working minutes have passed between two instants.</summary>
    public static int MinutesBetween(BusinessHours hours, DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc) return 0;

        var zone = ResolveZone(hours.TimeZoneId);

        var from = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone);
        var to = TimeZoneInfo.ConvertTimeFromUtc(toUtc, zone);

        var holidays = HolidaySet(hours);

        var total = 0;
        var day = from.Date;

        // A guard rather than a while(true): a target measured in working
        // minutes should never span years, and if it does that is a data problem
        // this loop must not turn into a hang.
        for (var guard = 0; day <= to.Date && guard < 400; guard++, day = day.AddDays(1))
        {
            var (open, close) = WindowFor(hours, day.DayOfWeek);

            if (open < 0 || close <= open) continue;
            if (IsHoliday(holidays, day)) continue;

            var dayOpen = day.AddMinutes(open);
            var dayClose = day.AddMinutes(close);

            // Clip the day's window to the span being measured, so the first and
            // last days count only their overlapping part.
            var start = from > dayOpen ? from : dayOpen;
            var end = to < dayClose ? to : dayClose;

            if (end > start) total += (int)(end - start).TotalMinutes;
        }

        return total;
    }

    /// <summary>
    /// The instant a target expires, given when the clock started.
    ///
    /// Used to show "due by" on a screen. Answering it by adding wall-clock time
    /// would put a Friday-evening deadline on a Saturday morning nobody works.
    /// </summary>
    public static DateTime? DueAt(BusinessHours hours, DateTime fromUtc, int workingMinutes)
    {
        if (workingMinutes <= 0) return fromUtc;

        var zone = ResolveZone(hours.TimeZoneId);
        var cursor = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone);
        var holidays = HolidaySet(hours);

        var remaining = workingMinutes;
        var day = cursor.Date;

        for (var guard = 0; guard < 400; guard++, day = day.AddDays(1))
        {
            var (open, close) = WindowFor(hours, day.DayOfWeek);

            if (open < 0 || close <= open) continue;
            if (IsHoliday(holidays, day)) continue;

            var dayOpen = day.AddMinutes(open);
            var dayClose = day.AddMinutes(close);

            var start = cursor > dayOpen ? cursor : dayOpen;
            if (start >= dayClose) continue;

            var available = (int)(dayClose - start).TotalMinutes;

            if (available >= remaining)
            {
                var local = start.AddMinutes(remaining);
                return TimeZoneInfo.ConvertTimeToUtc(local, zone);
            }

            remaining -= available;
        }

        // More than a year of working days away. Refusing to answer is more
        // honest than returning a date nobody would trust.
        return null;
    }

    /// <summary>Whether the office is open at a given instant.</summary>
    public static bool IsOpen(BusinessHours hours, DateTime atUtc)
    {
        var zone = ResolveZone(hours.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(atUtc, zone);

        if (IsHoliday(HolidaySet(hours), local.Date)) return false;

        var (open, close) = WindowFor(hours, local.DayOfWeek);
        if (open < 0 || close <= open) return false;

        var minute = (local.Hour * 60) + local.Minute;
        return minute >= open && minute < close;
    }

    private static (int Open, int Close) WindowFor(BusinessHours h, DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => (h.SundayOpen, h.SundayClose),
        DayOfWeek.Monday => (h.MondayOpen, h.MondayClose),
        DayOfWeek.Tuesday => (h.TuesdayOpen, h.TuesdayClose),
        DayOfWeek.Wednesday => (h.WednesdayOpen, h.WednesdayClose),
        DayOfWeek.Thursday => (h.ThursdayOpen, h.ThursdayClose),
        DayOfWeek.Friday => (h.FridayOpen, h.FridayClose),
        _ => (h.SaturdayOpen, h.SaturdayClose),
    };

    /// <summary>
    /// Holidays as two sets: fixed dates, and month-day pairs that recur.
    ///
    /// Split so a recurring holiday matches every year without needing a row per
    /// year, which is how a holiday list silently stops working next January.
    /// </summary>
    private static (HashSet<DateTime> Fixed, HashSet<(int Month, int Day)> Recurring) HolidaySet(
        BusinessHours hours)
    {
        var fixedDays = new HashSet<DateTime>();
        var recurring = new HashSet<(int, int)>();

        foreach (var holiday in hours.Holidays)
        {
            if (holiday.IsRecurring) recurring.Add((holiday.Date.Month, holiday.Date.Day));
            else fixedDays.Add(holiday.Date.Date);
        }

        return (fixedDays, recurring);
    }

    private static bool IsHoliday(
        (HashSet<DateTime> Fixed, HashSet<(int Month, int Day)> Recurring) holidays, DateTime day)
        => holidays.Fixed.Contains(day.Date) || holidays.Recurring.Contains((day.Month, day.Day));

    /// <summary>
    /// The zone, falling back to the server's own rather than throwing.
    ///
    /// Windows and Linux disagree about timezone identifiers, and an SLA sweep
    /// that dies on a machine with a different tz database is worse than one
    /// that runs an hour out.
    /// </summary>
    private static TimeZoneInfo ResolveZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }
}
