using System.Text;
using Beauty.Api.Data;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AppointmentFilterEndpointTests
{
    [Theory]
    [InlineData("", 4)]
    [InlineData("?customerName=  hoa  ", 2)]
    [InlineData("?phone=0909000111", 2)]
    [InlineData("?status=Confirmed", 1)]
    [InlineData("?fromDate=2026-08-11&toDate=2026-08-11", 2)]
    [InlineData("?customerName=hoa&status=Pending&service=Co%20dau&fromDate=2026-08-10&toDate=2026-08-10", 1)]
    [InlineData("?customerName=khong-co", 0)]
    public async Task GetAdminAppointmentsAsync_AppliesValidFilters(string query, int expectedCount)
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);

        var response = await InvokeAsync(db, query);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(expectedCount, CountRows(response.Body));
    }

    [Theory]
    [InlineData("?status=Waiting")]
    [InlineData("?fromDate=not-a-date")]
    [InlineData("?fromDate=2026-08-12&toDate=2026-08-10")]
    public async Task GetAdminAppointmentsAsync_RejectsInvalidFilters(string query)
    {
        await using var db = CreateDbContext();

        var response = await InvokeAsync(db, query);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    private static async Task<(int StatusCode, string Body)> InvokeAsync(BeautyDbContext db, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        var result = await AppointmentEndpoints.GetAdminAppointmentsAsync(context.Request, db, CancellationToken.None);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static int CountRows(string body) => body.Split("\"id\"", StringSplitOptions.None).Length - 1;

    private static async Task SeedAsync(BeautyDbContext db)
    {
        db.Appointments.AddRange(
            Appointment("Nguyen Thi Hoa", "0909000111", "Co dau", "2026-08-10T03:00:00+00:00", AppointmentStatus.Pending),
            Appointment("Tran Hoa", "0909000222", "Co dau", "2026-08-11T03:00:00+00:00", AppointmentStatus.Confirmed),
            Appointment("Le Mai", "0909000111", "Du tiec", "2026-08-11T07:00:00+00:00", AppointmentStatus.Pending),
            Appointment("Pham An", "0909000333", "Du tiec", "2026-08-12T03:00:00+00:00", AppointmentStatus.Completed));
        await db.SaveChangesAsync();
    }

    private static Appointment Appointment(string name, string phone, string service, string startAt, AppointmentStatus status) => new()
    {
        CustomerName = name,
        Phone = phone,
        Service = service,
        StartAt = DateTimeOffset.Parse(startAt),
        EndAt = DateTimeOffset.Parse(startAt).AddHours(2),
        Status = status
    };

    private static BeautyDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BeautyDbContext>()
        .UseInMemoryDatabase($"AppointmentFilterEndpointTests-{Guid.NewGuid():N}")
        .Options);
}
