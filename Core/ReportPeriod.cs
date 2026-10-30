namespace MyPiggyBank.Core;

public enum ReportPeriodMode { CalendarMonth, PayCycle, CustomRange }
public enum WeekendAdjustment { KeepDate, PreviousWorkingDay, NextWorkingDay }
public sealed record PayRule(DateOnly EffectiveMonth, int MonthlyDay, WeekendAdjustment Weekend, DateOnly FirstPayday);
public sealed record DateRange(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
    public void Validate() => FinanceDocument.Require(Start <= End,"The start date must be on or before the end date.");
}

public sealed record PaySchedule(IReadOnlyList<PayRule> Rules)
{
    [System.Text.Json.Serialization.JsonIgnore] public DateOnly FirstMonth => Rules.Min(r => r.EffectiveMonth);
    public PayRule RuleFor(DateOnly month) => Rules.Where(r => r.EffectiveMonth <= month).OrderBy(r => r.EffectiveMonth).LastOrDefault()
        ?? throw new InvalidDataException("Choose a month on or after the first pay cycle, or configure an earlier starting period.");
    public static DateOnly ScheduledPayday(DateOnly month, int day, WeekendAdjustment weekend)
    {
        FinanceDocument.Require(day is >= 1 and <= 31 && Enum.IsDefined(weekend),"Enter a payday from 1 to 31 and a valid weekend rule.");
        var date = new DateOnly(month.Year,month.Month,Math.Min(day,DateTime.DaysInMonth(month.Year,month.Month)));
        var offset = (weekend,date.DayOfWeek) switch {
            (WeekendAdjustment.PreviousWorkingDay,DayOfWeek.Saturday) => -1,
            (WeekendAdjustment.PreviousWorkingDay,DayOfWeek.Sunday) => -2,
            (WeekendAdjustment.NextWorkingDay,DayOfWeek.Saturday) => 2,
            (WeekendAdjustment.NextWorkingDay,DayOfWeek.Sunday) => 1,
            _ => 0 };
        return date.AddDays(offset);
    }
    public DateOnly ActualPayday(DateOnly month)
    {
        var rule = RuleFor(month);
        return month == rule.EffectiveMonth ? rule.FirstPayday : ScheduledPayday(month,rule.MonthlyDay,rule.Weekend);
    }
    public DateRange Cycle(DateOnly month)
    {
        FinanceDocument.Require(month.Day == 1 && month < new DateOnly(9999,12,1),"Select a pay-cycle month before December 9999.");
        var start = ActualPayday(month); var next = ActualPayday(month.AddMonths(1));
        FinanceDocument.Require(start < next,"Paydays must increase from one period to the next. Check the actual payday or effective month.");
        return new(start,next.AddDays(-1));
    }
    public DateOnly MonthContaining(DateOnly date)
    {
        // Find the latest payday on/before the date, including paydays shifted across month boundaries.
        var first = (FirstMonth.Year - 1) * 12 + FirstMonth.Month - 1;
        var low = first; var high = (9999 - 1) * 12 + 10;
        DateOnly Month(int index) => new(index / 12 + 1,index % 12 + 1,1);
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (ActualPayday(Month(middle)) <= date) low = middle; else high = middle - 1;
        }
        return Month(low);
    }
    public void Validate()
    {
        FinanceDocument.Require(Rules is { Count: > 0 } && Rules.All(r => r is not null),"Configure at least one pay-cycle rule.");
        FinanceDocument.Require(Rules.Select(r => r.EffectiveMonth).Distinct().Count() == Rules.Count,"Use only one pay-cycle rule per effective month.");
        foreach (var rule in Rules)
        {
            FinanceDocument.Require(rule.EffectiveMonth.Day == 1 && rule.EffectiveMonth < new DateOnly(9999,12,1) && rule.FirstPayday != default,
                "Choose a valid effective month and actual payday.");
            FinanceDocument.Require(rule.MonthlyDay is >= 1 and <= 31 && Enum.IsDefined(rule.Weekend),"Enter a payday from 1 to 31 and a valid weekend rule.");
            Cycle(rule.EffectiveMonth);
            if (rule.EffectiveMonth > FirstMonth) Cycle(rule.EffectiveMonth.AddMonths(-1));
        }
    }
}

public sealed record ReportPeriod(ReportPeriodMode Mode, DateOnly Month, DateOnly CustomStart, DateOnly CustomEnd)
{
    public static ReportPeriod Restore(FinanceDocument document, DateOnly fallbackDate)
    {
        if (document.SavedPeriod is not null) return document.SavedPeriod;
        var month = new DateOnly(fallbackDate.Year,fallbackDate.Month,1);
        return document.PaySchedule is { } schedule
            ? new(ReportPeriodMode.PayCycle,schedule.MonthContaining(fallbackDate),month,fallbackDate)
            : new(ReportPeriodMode.CalendarMonth,month,month,fallbackDate);
    }
    public ReportPeriod AfterTransaction(DateOnly date) => Mode == ReportPeriodMode.CalendarMonth
        ? this with { Month = new(date.Year,date.Month,1) } : this;

    public DateRange Range(PaySchedule? schedule)
    {
        FinanceDocument.Require(Enum.IsDefined(Mode),"Choose a valid period type.");
        var range = Mode switch {
            ReportPeriodMode.CalendarMonth => new DateRange(new(Month.Year,Month.Month,1),new(Month.Year,Month.Month,DateTime.DaysInMonth(Month.Year,Month.Month))),
            ReportPeriodMode.PayCycle => (schedule ?? throw new InvalidDataException("Configure your first pay cycle.")).Cycle(Month),
            _ => new DateRange(CustomStart,CustomEnd) };
        range.Validate(); return range;
    }
}
