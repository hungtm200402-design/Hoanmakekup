using System.Text;
using System.Text.Json;
using Beauty.Api.Data;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AppointmentEndpointTests
{
    [Fact]
    public async Task CreateAppointmentAsync_ReturnsCreatedForValidBooking()
    {
        await using var db = CreateDbContext();
        var result = await PostAppointmentAsync(db, new
        {
            customerName = "Nguyen Thi Hoa",
            phone = "0909000111",
            email = "hoa@example.com",
            service = "Makeup cao cap",
            startAt = FutureStart()
        });

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_ReturnsBadRequestForInvalidBookingData()
    {
        await using var db = CreateDbContext();
        var result = await PostAppointmentAsync(db, new
        {
            customerName = "Nguyen Thi Hoa",
            phone = "bad-phone",
            service = "Makeup cao cap",
            startAt = FutureStart()
        });

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("Số điện thoại không hợp lệ.", result.Body);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_ReturnsConflictForOverlappingBooking()
    {
        await using var db = CreateDbContext();
        var startAt = FutureStart();
        await PostAppointmentAsync(db, new
        {
            customerName = "Nguyen Thi Hoa",
            phone = "0909000111",
            service = "Makeup cao cap",
            startAt
        });

        var result = await PostAppointmentAsync(db, new
        {
            customerName = "Tran My Mai",
            phone = "0909000222",
            service = "Makeup cao cap",
            startAt
        });

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Contains("Khung giờ này đã có lịch hẹn.", result.Body);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task GetAdminAppointmentsAsync_ReturnsAppointmentsOrderedByStartTime()
    {
        await using var db = CreateDbContext();
        db.Appointments.AddRange(
            new Appointment
            {
                CustomerName = "Tran My Mai",
                Phone = "0909000222",
                Service = "Makeup cao cap",
                StartAt = DateTimeOffset.Parse("2026-08-11T03:00:00+00:00"),
                EndAt = DateTimeOffset.Parse("2026-08-11T05:00:00+00:00")
            },
            new Appointment
            {
                CustomerName = "Nguyen Thi Hoa",
                Phone = "0909000111",
                Service = "Makeup cao cap",
                StartAt = DateTimeOffset.Parse("2026-08-10T03:00:00+00:00"),
                EndAt = DateTimeOffset.Parse("2026-08-10T05:00:00+00:00")
            });
        await db.SaveChangesAsync();

        var httpContext = CreateHttpContext();
        var result = await AppointmentEndpoints.GetAdminAppointmentsAsync(httpContext.Request, db, CancellationToken.None);
        await result.ExecuteAsync(httpContext);
        var body = await ReadResponseBodyAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.True(body.IndexOf("Nguyen Thi Hoa", StringComparison.Ordinal) < body.IndexOf("Tran My Mai", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateAdminAppointmentStatusAsync_SavesStatusToDatabase()
    {
        await using var db = CreateDbContext();
        var appointment = new Appointment
        {
            CustomerName = "Nguyen Thi Hoa",
            Phone = "0909000111",
            Service = "Makeup co dau",
            StartAt = DateTimeOffset.Parse("2026-08-10T03:00:00+00:00"),
            EndAt = DateTimeOffset.Parse("2026-08-10T05:00:00+00:00"),
            Status = AppointmentStatus.Pending
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var result = await AppointmentEndpoints.UpdateAdminAppointmentStatusAsync(
            appointment.Id,
            new UpdateAppointmentStatusRequest(AppointmentStatus.Confirmed),
            db,
            CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var saved = await db.Appointments.FindAsync(appointment.Id);
        Assert.NotNull(saved);
        Assert.Equal(AppointmentStatus.Confirmed, saved.Status);
    }

    [Fact]
    public async Task UpdateAdminAppointmentStatusAsync_ReturnsNotFoundForMissingAppointment()
    {
        await using var db = CreateDbContext();

        var result = await AppointmentEndpoints.UpdateAdminAppointmentStatusAsync(
            Guid.NewGuid(),
            new UpdateAppointmentStatusRequest(AppointmentStatus.Rejected),
            db,
            CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
    }

    [Fact]
    public void Program_ProtectsAdminAppointmentStatusEndpointWithStaffPolicy()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Beauty.Api", "Program.cs"));

        Assert.Contains("MapPut(\"/api/admin/appointments/{id:guid}/status\", AppointmentEndpoints.UpdateAdminAppointmentStatusAsync)", program);
        Assert.Contains(".RequireAuthorization(\"Staff\")", program);
    }

    private static async Task<(int StatusCode, string Body)> PostAppointmentAsync(BeautyDbContext db, object payload)
    {
        var httpContext = CreateHttpContext();
        var json = JsonSerializer.Serialize(payload);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var service = new AppointmentService(db, new TestTimeProvider());

        var result = await AppointmentEndpoints.CreateAppointmentAsync(httpContext, service, CancellationToken.None);
        await result.ExecuteAsync(httpContext);

        return (httpContext.Response.StatusCode, await ReadResponseBodyAsync(httpContext));
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var httpContext = CreateHttpContext();
        await result.ExecuteAsync(httpContext);
        return (httpContext.Response.StatusCode, await ReadResponseBodyAsync(httpContext));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static BeautyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BeautyDbContext>()
            .UseInMemoryDatabase($"AppointmentEndpointTests-{Guid.NewGuid():N}")
            .Options;

        return new BeautyDbContext(options);
    }

    private static string FutureStart() => new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.FromHours(7)).ToString("O");

    private sealed class TestTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
    }
}
