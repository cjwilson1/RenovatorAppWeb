namespace RenovatorApp.Web.ViewModels;

public sealed class CalendarIndexViewModel
{
    public DateTime DisplayMonth { get; init; }
    public DateTime SelectedDate { get; init; }
    public IReadOnlyList<CalendarDayViewModel> Days { get; init; } = [];
    public IReadOnlyList<CalendarEventRowViewModel> SelectedDayEvents { get; init; } = [];
    public CalendarEventEditViewModel? EventForm { get; init; }
    public bool ShowDayDialog { get; init; }
    public string? StatusMessage { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class CalendarDayViewModel
{
    public DateTime Date { get; init; }
    public IReadOnlyList<CalendarEventRowViewModel> Events { get; init; } = [];
}

public sealed class CalendarEventRowViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public bool AllDay { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public string Notes { get; init; } = string.Empty;
    public bool IsPrivate { get; init; }
    public IReadOnlyList<string> AlertOffsets { get; init; } = [];
}

public sealed class CalendarEventEditViewModel
{
    public Guid? Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Title { get; set; } = string.Empty;
    public bool AllDay { get; set; }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "10:00";
    public List<string> AlertOffsets { get; set; } = ["0"];
    public string Notes { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}
