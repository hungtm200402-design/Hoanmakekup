using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Services;

public sealed class AppointmentAvailabilityService(BeautyDbContext db)
{
    private static readonly TimeOnly[] BusinessStartTimes =
    [
        new(8, 0),
        new(10, 0),
        new(14, 0),
        new(16, 0),
        new(18, 0)
    ];

    private static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(7);
    private static readonly TimeSpan Duration = TimeSpan.FromHours(2);

    public async Task<IReadOnlyList<AvailableAppointmentSlot>> GetForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BusinessOffset).ToUniversalTime();
        var dayEnd = dayStart.AddDays(1);
        var appointments = await db.Appointments
            .Where(appointment => appointment.Status != AppointmentStatus.Cancelled &&
                                  appointment.Status != AppointmentStatus.Rejected)
            .ToListAsync(cancellationToken);

        var appointmentsForDay = appointments
            .Where(appointment => appointment.StartAt < dayEnd && appointment.EndAt > dayStart)
            .ToList();

        return BusinessStartTimes
            .Select(time => new DateTimeOffset(date.ToDateTime(time), BusinessOffset).ToUniversalTime())
            .Where(start => !appointmentsForDay.Any(appointment => start < appointment.EndAt && start.Add(Duration) > appointment.StartAt))
            .Select(start => new AvailableAppointmentSlot(date.ToString("yyyy-MM-dd"), start.ToOffset(BusinessOffset).ToString("HH:mm")))
            .ToList();
    }
}

public sealed record AvailableAppointmentSlot(string Date, string Time);
