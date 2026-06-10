using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class CalendarController : Controller
{
    private static readonly IReadOnlyDictionary<string, TimeSpan> AlertOffsetValues = new Dictionary<string, TimeSpan>
    {
        ["0"] = TimeSpan.Zero,
        ["-10m"] = TimeSpan.FromMinutes(-10),
        ["-1h"] = TimeSpan.FromHours(-1),
        ["-1d"] = TimeSpan.FromDays(-1)
    };

    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;

    public CalendarController(RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(
        int? year,
        int? month,
        DateTime? selectedDate,
        Guid? editId,
        bool showEditor = false,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var displayMonth = GetDisplayMonth(year, month, today);
        var selected = selectedDate?.Date ?? (displayMonth.Year == today.Year && displayMonth.Month == today.Month
            ? today
            : displayMonth);

        if (selected.Month != displayMonth.Month || selected.Year != displayMonth.Year)
        {
            selected = new DateTime(displayMonth.Year, displayMonth.Month, Math.Min(selected.Day, DateTime.DaysInMonth(displayMonth.Year, displayMonth.Month)));
        }

        var events = await GetVisibleEventsAsync(displayMonth, cancellationToken);
        var eventForm = await BuildEventFormAsync(editId, showEditor, selected, displayMonth, cancellationToken);

        return View(new CalendarIndexViewModel
        {
            DisplayMonth = displayMonth,
            SelectedDate = selected,
            Days = BuildCalendarDays(displayMonth, events),
            SelectedDayEvents = events
                .Where(calendarEvent => calendarEvent.Date.Date == selected.Date)
                .OrderBy(calendarEvent => calendarEvent.AllDay ? TimeSpan.Zero : calendarEvent.StartTime)
                .ThenBy(calendarEvent => calendarEvent.Title)
                .Select(ToRowViewModel)
                .ToList(),
            EventForm = eventForm,
            StatusMessage = TempData["CalendarStatus"] as string,
            ErrorMessage = TempData["CalendarError"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CalendarEventEditViewModel form, CancellationToken cancellationToken)
    {
        form.Date = form.Date.Date;

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            TempData["CalendarError"] = "Event title is required.";
            return RedirectToCalendar(form, form.Id, showEditor: true);
        }

        var startTime = ParseTime(form.StartTime, new TimeSpan(9, 0, 0));
        var endTime = ParseTime(form.EndTime, startTime.Add(TimeSpan.FromHours(1)));

        if (!form.AllDay && endTime <= startTime)
        {
            endTime = startTime.Add(TimeSpan.FromHours(1));
        }

        var now = DateTime.UtcNow;
        CalendarEvent calendarEvent;

        if (form.Id.HasValue)
        {
            calendarEvent = await _dbContext.CalendarEvents
                .ForCompany(_currentUserSession.RenoCompanyID)
                .FirstOrDefaultAsync(item => item.CalendarEventId == form.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Calendar event was not found.");
        }
        else
        {
            calendarEvent = new CalendarEvent
            {
                CalendarEventId = Guid.NewGuid(),
                RenoCompanyID = _currentUserSession.RenoCompanyID,
                RenoUserID = _currentUserSession.UserID,
                CreatedAtUtc = now
            };
            _dbContext.CalendarEvents.Add(calendarEvent);
        }

        calendarEvent.RenoCompanyID = _currentUserSession.RenoCompanyID;
        calendarEvent.RenoUserID = _currentUserSession.UserID;
        calendarEvent.Title = Clean(form.Title);
        calendarEvent.Date = NormalizeDate(form.Date);
        calendarEvent.AllDay = form.AllDay;
        calendarEvent.StartTime = startTime;
        calendarEvent.EndTime = endTime;
        calendarEvent.EventAlertTimes = BuildAlertTimes(form.Date, form.AllDay, startTime, form.AlertOffsets);
        calendarEvent.Notes = Clean(form.Notes);
        calendarEvent.IsPrivate = form.IsPrivate;
        calendarEvent.IsDeleted = false;
        calendarEvent.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["CalendarStatus"] = form.Id.HasValue ? "Calendar event updated." : "Calendar event added.";

        return RedirectToCalendar(form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, int year, int month, DateTime selectedDate, CancellationToken cancellationToken)
    {
        var calendarEvent = await _dbContext.CalendarEvents
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.CalendarEventId == id, cancellationToken);

        if (calendarEvent is null)
        {
            return NotFound();
        }

        calendarEvent.IsDeleted = true;
        calendarEvent.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["CalendarStatus"] = "Calendar event deleted.";

        return RedirectToAction(nameof(Index), new
        {
            year,
            month,
            selectedDate = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }

    private async Task<IReadOnlyList<CalendarEvent>> GetVisibleEventsAsync(DateTime displayMonth, CancellationToken cancellationToken)
    {
        var visibleStart = NormalizeDate(displayMonth.AddDays(-(int)displayMonth.DayOfWeek));
        var visibleEnd = NormalizeDate(visibleStart.AddDays(42));

        return await _dbContext.CalendarEvents
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Where(calendarEvent => !calendarEvent.IsDeleted
                && calendarEvent.Date >= visibleStart
                && calendarEvent.Date < visibleEnd)
            .OrderBy(calendarEvent => calendarEvent.Date)
            .ThenBy(calendarEvent => calendarEvent.AllDay ? TimeSpan.Zero : calendarEvent.StartTime)
            .ThenBy(calendarEvent => calendarEvent.Title)
            .ToListAsync(cancellationToken);
    }

    private async Task<CalendarEventEditViewModel?> BuildEventFormAsync(
        Guid? editId,
        bool showEditor,
        DateTime selectedDate,
        DateTime displayMonth,
        CancellationToken cancellationToken)
    {
        if (editId.HasValue)
        {
            var calendarEvent = await _dbContext.CalendarEvents
                .AsNoTracking()
                .ForCompany(_currentUserSession.RenoCompanyID)
                .FirstOrDefaultAsync(item => item.CalendarEventId == editId.Value && !item.IsDeleted, cancellationToken);

            if (calendarEvent is null)
            {
                TempData["CalendarError"] = "Calendar event was not found.";
                return null;
            }

            return new CalendarEventEditViewModel
            {
                Id = calendarEvent.CalendarEventId,
                Date = calendarEvent.Date.Date,
                Title = calendarEvent.Title,
                AllDay = calendarEvent.AllDay,
                StartTime = FormatTimeInput(calendarEvent.StartTime),
                EndTime = FormatTimeInput(calendarEvent.EndTime),
                AlertOffsets = GetSelectedAlertOffsets(calendarEvent).ToList(),
                Notes = calendarEvent.Notes,
                IsPrivate = calendarEvent.IsPrivate,
                Year = displayMonth.Year,
                Month = displayMonth.Month
            };
        }

        if (!showEditor)
        {
            return null;
        }

        return new CalendarEventEditViewModel
        {
            Date = selectedDate,
            Year = displayMonth.Year,
            Month = displayMonth.Month
        };
    }

    private static IReadOnlyList<CalendarDayViewModel> BuildCalendarDays(DateTime displayMonth, IReadOnlyList<CalendarEvent> events)
    {
        var visibleStart = displayMonth.AddDays(-(int)displayMonth.DayOfWeek);
        var eventsByDate = events
            .GroupBy(calendarEvent => calendarEvent.Date.Date)
            .ToDictionary(group => group.Key, group => group.Select(ToRowViewModel).ToList());

        return Enumerable
            .Range(0, 42)
            .Select(index =>
            {
                var date = visibleStart.AddDays(index);
                return new CalendarDayViewModel
                {
                    Date = date,
                    Events = eventsByDate.GetValueOrDefault(date.Date) ?? []
                };
            })
            .ToList();
    }

    private static CalendarEventRowViewModel ToRowViewModel(CalendarEvent calendarEvent)
    {
        return new CalendarEventRowViewModel
        {
            Id = calendarEvent.CalendarEventId,
            Title = calendarEvent.Title,
            Date = calendarEvent.Date.Date,
            AllDay = calendarEvent.AllDay,
            StartTime = calendarEvent.StartTime,
            EndTime = calendarEvent.EndTime,
            Notes = calendarEvent.Notes,
            IsPrivate = calendarEvent.IsPrivate,
            AlertOffsets = GetSelectedAlertOffsets(calendarEvent)
        };
    }

    private static DateTime GetDisplayMonth(int? year, int? month, DateTime today)
    {
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        return selectedYear is < 2025 or > 2050 || selectedMonth is < 1 or > 12
            ? new DateTime(today.Year, today.Month, 1)
            : new DateTime(selectedYear, selectedMonth, 1);
    }

    private RedirectToActionResult RedirectToCalendar(CalendarEventEditViewModel form, Guid? editId = null, bool showEditor = false)
    {
        return RedirectToAction(nameof(Index), new
        {
            year = form.Year == 0 ? form.Date.Year : form.Year,
            month = form.Month == 0 ? form.Date.Month : form.Month,
            selectedDate = form.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            editId,
            showEditor
        });
    }

    private static string BuildAlertTimes(DateTime eventDate, bool allDay, TimeSpan startTime, IEnumerable<string> selectedOffsets)
    {
        var eventStart = eventDate.Date + (allDay ? TimeSpan.Zero : startTime);

        return string.Join("|", selectedOffsets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(offset => AlertOffsetValues.TryGetValue(offset, out var value) ? value : (TimeSpan?)null)
            .Where(offset => offset.HasValue)
            .Select(offset => eventStart + offset!.Value)
            .OrderBy(alertTime => alertTime)
            .Select(alertTime => alertTime.ToString("O", CultureInfo.InvariantCulture)));
    }

    private static IReadOnlyList<string> GetSelectedAlertOffsets(CalendarEvent calendarEvent)
    {
        var eventStart = calendarEvent.Date.Date + (calendarEvent.AllDay ? TimeSpan.Zero : calendarEvent.StartTime);
        var selectedOffsets = new List<string>();

        foreach (var value in calendarEvent.EventAlertTimes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var alertTime))
            {
                continue;
            }

            var offset = alertTime - eventStart;
            var match = AlertOffsetValues.FirstOrDefault(option => option.Value == offset);
            if (!string.IsNullOrWhiteSpace(match.Key))
            {
                selectedOffsets.Add(match.Key);
            }
        }

        return selectedOffsets;
    }

    private static TimeSpan ParseTime(string value, TimeSpan fallback)
    {
        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string FormatTimeInput(TimeSpan value)
    {
        return DateTime.Today.Add(value).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
