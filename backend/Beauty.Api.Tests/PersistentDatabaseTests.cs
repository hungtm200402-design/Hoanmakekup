using Beauty.Api.Data;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class PersistentDatabaseTests
{
    [Fact]
    public async Task SqliteDatabase_PreservesAppointmentsWhenContextIsRecreated()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"beauty-persistence-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        await using (var first = CreateDbContext(connectionString))
        {
            await first.Database.EnsureCreatedAsync();
            first.Appointments.Add(new Appointment
            {
                CustomerName = "Nguyen Thi Hoa",
                Phone = "0909000111",
                Service = "Makeup",
                StartAt = DateTimeOffset.UtcNow.AddDays(1),
                EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(2)
            });
            await first.SaveChangesAsync();
        }

        await using var reopened = CreateDbContext(connectionString);
        Assert.Equal(1, await reopened.Appointments.CountAsync());
    }

    [Fact]
    public async Task GetAdminAppointmentsAsync_OrdersAppointmentsWhenUsingSqlite()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"beauty-sqlite-order-{Guid.NewGuid():N}.db");
        await using var db = CreateDbContext($"Data Source={databasePath}");
        await db.Database.EnsureCreatedAsync();
        db.Appointments.AddRange(
            new Appointment { CustomerName = "Later", Phone = "0909000111", Service = "Makeup", StartAt = DateTimeOffset.Parse("2026-08-11T03:00:00+00:00"), EndAt = DateTimeOffset.Parse("2026-08-11T05:00:00+00:00") },
            new Appointment { CustomerName = "Earlier", Phone = "0909000222", Service = "Makeup", StartAt = DateTimeOffset.Parse("2026-08-10T03:00:00+00:00"), EndAt = DateTimeOffset.Parse("2026-08-10T05:00:00+00:00") });
        await db.SaveChangesAsync();
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        var result = await AppointmentEndpoints.GetAdminAppointmentsAsync(context.Request, db, CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task AvailabilityService_ReturnsSlotsWhenUsingSqlite()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"beauty-sqlite-availability-{Guid.NewGuid():N}.db");
        await using var db = CreateDbContext($"Data Source={databasePath}");
        await db.Database.EnsureCreatedAsync();
        var service = new AppointmentAvailabilityService(db);

        var slots = await service.GetForDateAsync(new DateOnly(2026, 12, 1), CancellationToken.None);

        Assert.Equal(5, slots.Count);
    }

    private static BeautyDbContext CreateDbContext(string connectionString) => new(new DbContextOptionsBuilder<BeautyDbContext>()
        .UseSqlite(connectionString)
        .Options);
}
