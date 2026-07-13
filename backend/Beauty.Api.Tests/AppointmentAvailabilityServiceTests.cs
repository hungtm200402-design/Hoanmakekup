using Beauty.Api.Data;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AppointmentAvailabilityServiceTests
{
    [Fact]
    public async Task GetForDateAsync_ReturnsAllBusinessSlotsWhenNoAppointmentsExist()
    {
        await using var db = CreateDbContext();
        var service = new AppointmentAvailabilityService(db);

        var slots = await service.GetForDateAsync(new DateOnly(2026, 8, 10), CancellationToken.None);

        Assert.Equal(["08:00", "10:00", "14:00", "16:00", "18:00"], slots.Select(slot => slot.Time));
    }

    [Fact]
    public async Task GetForDateAsync_ExcludesOccupiedSlotsButKeepsRejectedAppointmentsAvailable()
    {
        await using var db = CreateDbContext();
        db.Appointments.AddRange(
            Appointment("2026-08-10T03:00:00+00:00", AppointmentStatus.Confirmed),
            Appointment("2026-08-10T07:00:00+00:00", AppointmentStatus.Rejected));
        await db.SaveChangesAsync();
        var service = new AppointmentAvailabilityService(db);

        var slots = await service.GetForDateAsync(new DateOnly(2026, 8, 10), CancellationToken.None);

        Assert.DoesNotContain(slots, slot => slot.Time == "10:00");
        Assert.Contains(slots, slot => slot.Time == "14:00");
    }

    private static Appointment Appointment(string start, AppointmentStatus status) => new()
    {
        CustomerName = "Nguyen Thi Hoa",
        Phone = "0909000111",
        Service = "Makeup",
        StartAt = DateTimeOffset.Parse(start),
        EndAt = DateTimeOffset.Parse(start).AddHours(2),
        Status = status
    };

    private static BeautyDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BeautyDbContext>()
        .UseInMemoryDatabase($"AppointmentAvailabilityServiceTests-{Guid.NewGuid():N}")
        .Options);
}
