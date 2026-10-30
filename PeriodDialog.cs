using System.IO;
using System.Windows;
using System.Windows.Controls;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed class PeriodDialog : FormWindow
{
    public ReportPeriod Result { get; private set; }
    public PaySchedule? Schedule { get; private set; }

    public PeriodDialog(Window owner, FinanceDocument document, ReportPeriod current)
        : base(owner,"Period","Choose a calendar month, pay cycle, or custom date range. Save the document to keep your selection.","Apply",620)
    {
        Result = current; Schedule = document.PaySchedule;
        var type = new ComboBox { ItemsSource = new[] { "Calendar Month", "Pay Cycle", "Custom Range" }, SelectedIndex = (int)current.Mode };
        Field("Period type *",type);
        var months = new ComboBox { ItemsSource = Display.Culture.DateTimeFormat.MonthNames.Take(12), SelectedIndex = current.Month.Month - 1 };
        var year = new TextBox { Text = current.Month.Year.ToString(), MaxLength = 4 };
        UIElement LastRow() => Fields.Children[Fields.Children.Count - 1];
        Field("Month *",months); var monthRow = LastRow(); Field("Year *",year); var yearRow = LastRow();
        PayRule? existing = null;
        if (Schedule is not null && current.Month >= Schedule.FirstMonth) existing = Schedule.RuleFor(current.Month);
        var day = new TextBox { Text = existing?.MonthlyDay.ToString() ?? "", MaxLength = 2 };
        var weekend = new ComboBox { ItemsSource = new[] { "Keep original date", "Previous working day (Friday)", "Next working day (Monday)" }, SelectedIndex = (int)(existing?.Weekend ?? WeekendAdjustment.KeepDate) };
        var first = DateInput(Schedule is not null && existing is not null ? Schedule.ActualPayday(current.Month) : current.Month);
        Field("Monthly payday *",day); var dayRow = LastRow(); Field("Weekend rule *",weekend); var weekendRow = LastRow(); DateField("Actual payday *",first); var firstRow = LastRow();
        var help = Ui.Text("The first selected period starts on Actual payday. Changes apply from this month; earlier payday rules are retained. The preceding cycle ends the day before this payday. Correct Actual payday manually for public holidays.",12,Ui.Muted); help.Margin = new Thickness(0,8,0,12); Fields.Children.Add(help);
        var start = DateInput(current.CustomStart); var end = DateInput(current.CustomEnd);
        DateField("Start date *",start); var startRow = LastRow(); DateField("End date *",end); var endRow = LastRow();
        var preview = Ui.Text("",14,Ui.Ink,true); Fields.Children.Add(Ui.Box(preview,new Thickness(12),Ui.Brush("#F6F6F9")));
        var applying = false;

        DateOnly ReadMonth()
        {
            FinanceDocument.Require(int.TryParse(year.Text,out var y) && y is >= 1 and <= 9999 && months.SelectedIndex >= 0,"Enter a year between 1 and 9999.");
            return new(y,months.SelectedIndex + 1,1);
        }
        (ReportPeriod Period,PaySchedule? Schedule) Read()
        {
            var mode = (ReportPeriodMode)type.SelectedIndex;
            var month = mode == ReportPeriodMode.CustomRange ? current.Month : ReadMonth();
            var schedule = document.PaySchedule;
            if (mode == ReportPeriodMode.PayCycle)
            {
                FinanceDocument.Require(int.TryParse(day.Text,out var payday) && payday is >= 1 and <= 31,"Enter a monthly payday from 1 to 31.");
                var actual = ReadDate(first); var adjustment = (WeekendAdjustment)weekend.SelectedIndex;
                var same = schedule is not null && month >= schedule.FirstMonth && schedule.RuleFor(month).MonthlyDay == payday && schedule.RuleFor(month).Weekend == adjustment && schedule.ActualPayday(month) == actual;
                if (!same) schedule = new PaySchedule((schedule?.Rules ?? []).Where(r => r.EffectiveMonth != month).Append(new(month,payday,adjustment,actual)).OrderBy(r => r.EffectiveMonth).ToArray());
                schedule!.Validate();
            }
            var selection = new ReportPeriod(mode,month,mode == ReportPeriodMode.CustomRange ? ReadDate(start) : current.CustomStart,mode == ReportPeriodMode.CustomRange ? ReadDate(end) : current.CustomEnd);
            selection.Range(schedule); return (selection,schedule);
        }
        void Refresh(bool recalculatePayday = false)
        {
            if (applying) return;
            applying = true;
            try
            {
                var mode = (ReportPeriodMode)type.SelectedIndex;
                monthRow.Visibility = yearRow.Visibility = mode == ReportPeriodMode.CustomRange ? Visibility.Collapsed : Visibility.Visible;
                dayRow.Visibility = weekendRow.Visibility = firstRow.Visibility = help.Visibility = mode == ReportPeriodMode.PayCycle ? Visibility.Visible : Visibility.Collapsed;
                startRow.Visibility = endRow.Visibility = mode == ReportPeriodMode.CustomRange ? Visibility.Visible : Visibility.Collapsed;
                if (recalculatePayday && mode == ReportPeriodMode.PayCycle && int.TryParse(day.Text,out var payday) && payday is >= 1 and <= 31)
                    first.Text = Display.Date(PaySchedule.ScheduledPayday(ReadMonth(),payday,(WeekendAdjustment)weekend.SelectedIndex));
                var value = Read(); var range = value.Period.Range(value.Schedule);
                preview.Text = $"{Display.Date(range.Start)} – {Display.Date(range.End)}\nIncome, expenses and transactions include both dates.";
                preview.Foreground = Ui.Ink;
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException)
            { preview.Text = e is InvalidDataException ? e.Message : "Choose dates within the supported range."; preview.Foreground = Ui.Muted; }
            finally { applying = false; }
        }
        void LoadMonth()
        {
            if (applying) return;
            try
            {
                var month = ReadMonth(); var saved = document.PaySchedule;
                if (saved is not null && month >= saved.FirstMonth)
                {
                    applying = true; var rule = saved.RuleFor(month); day.Text = rule.MonthlyDay.ToString(); weekend.SelectedIndex = (int)rule.Weekend;
                    first.Text = Display.Date(saved.ActualPayday(month)); applying = false; Refresh(); return;
                }
            }
            catch (InvalidDataException) { }
            finally { applying = false; }
            Refresh(true);
        }
        type.SelectionChanged += (_,_) => Refresh(); months.SelectionChanged += (_,_) => LoadMonth(); year.TextChanged += (_,_) => LoadMonth();
        day.TextChanged += (_,_) => Refresh(true); weekend.SelectionChanged += (_,_) => Refresh(true);
        first.TextChanged += (_,_) => Refresh(); start.TextChanged += (_,_) => Refresh(); end.TextChanged += (_,_) => Refresh();
        OnSubmit(() => { var value = Read(); Result = value.Period; Schedule = value.Schedule; }); Refresh();
    }
}
