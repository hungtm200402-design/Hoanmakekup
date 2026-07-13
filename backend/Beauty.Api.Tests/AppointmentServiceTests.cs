using Beauty.Api.Data;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AppointmentServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingAppointmentForValidBooking()
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider();
        var service = new AppointmentService(db, clock);
        var requestedStart = FutureLocalStart(clock, 10);

        var result = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "  Nguyen Thi Hoa  ",
            Phone = "  0909000111  ",
            Email = "hoa@example.com",
            Address = "Hoan Doan Beauty & Academy",
            Service = "Makeup cao cap",
            Tone = "Chuyen gia makeup",
            Note = "Can tu van tone nhe",
            StartAt = requestedStart.ToString("O")
        }, CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.Appointment);
        Assert.Equal("Nguyen Thi Hoa", result.Appointment.CustomerName);
        Assert.Equal("0909000111", result.Appointment.Phone);
        Assert.Equal(AppointmentStatus.Pending, result.Appointment.Status);
        Assert.Equal(requestedStart.ToUniversalTime(), result.Appointment.StartAt);
        Assert.Equal(requestedStart.ToUniversalTime().AddHours(2), result.Appointment.EndAt);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsOverlappingActiveAppointment()
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider();
        var service = new AppointmentService(db, clock);
        var requestedStart = FutureLocalStart(clock, 10);

        var first = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "Nguyen Thi Hoa",
            Phone = "0909000111",
            Service = "Makeup cao cap",
            StartAt = requestedStart.ToString("O")
        }, CancellationToken.None);

        var overlapping = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "Tran My Mai",
            Phone = "0909000222",
            Service = "Makeup cao cap",
            StartAt = requestedStart.AddHours(1).ToString("O")
        }, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(overlapping.Created);
        Assert.Equal(AppointmentCreateFailure.Overlap, overlapping.Failure);
        Assert.Equal("Khung giờ này đã có lịch hẹn.", overlapping.Message);
        Assert.Null(overlapping.Appointment);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsConcurrentOverlappingAppointments()
    {
        var databaseName = $"AppointmentServiceTests-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var clock = new TestTimeProvider();
        var requestedStart = FutureLocalStart(clock, 10);

        async Task<AppointmentCreateResult> BookAsync(string customerName, string phone)
        {
            await using var db = CreateDbContext(databaseName, databaseRoot);
            var service = new AppointmentService(db, clock);
            return await service.CreateAsync(new CreateAppointmentRequest
            {
                CustomerName = customerName,
                Phone = phone,
                Service = "Makeup cao cap",
                StartAt = requestedStart.ToString("O")
            }, CancellationToken.None);
        }

        var results = await Task.WhenAll(
            BookAsync("Nguyen Thi Hoa", "0909000111"),
            BookAsync("Tran My Mai", "0909000222"));

        await using var verificationDb = CreateDbContext(databaseName, databaseRoot);
        Assert.Single(results, result => result.Created);
        Assert.Single(results, result => result.Failure == AppointmentCreateFailure.Overlap);
        Assert.Equal(1, await verificationDb.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_AllowsBookingOverRejectedAppointment()
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider();
        var requestedStart = FutureLocalStart(clock, 10);
        db.Appointments.Add(new Appointment
        {
            CustomerName = "Khach cu",
            Phone = "0909000333",
            Service = "Makeup cao cap",
            StartAt = requestedStart.ToUniversalTime(),
            EndAt = requestedStart.ToUniversalTime().AddHours(2),
            Status = AppointmentStatus.Rejected
        });
        await db.SaveChangesAsync();
        var service = new AppointmentService(db, clock);

        var result = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "Tran My Mai",
            Phone = "0909000222",
            Service = "Makeup cao cap",
            StartAt = requestedStart.ToString("O")
        }, CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal(2, await db.Appointments.CountAsync());
    }

    [Theory]
    [InlineData("", "0909000111", "Makeup cao cap")]
    [InlineData("Nguyen Thi Hoa", "", "Makeup cao cap")]
    [InlineData("Nguyen Thi Hoa", "0909000111", "")]
    public async Task CreateAsync_RejectsMissingRequiredBookingFields(string customerName, string phone, string makeupService)
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider();
        var service = new AppointmentService(db, clock);

        var result = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = customerName,
            Phone = phone,
            Service = makeupService,
            StartAt = FutureLocalStart(clock, 10).ToString("O")
        }, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(AppointmentCreateFailure.InvalidRequest, result.Failure);
        Assert.Equal("Vui lòng nhập đầy đủ tên, số điện thoại và dịch vụ.", result.Message);
        Assert.Null(result.Appointment);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    [Theory]
    [InlineData("abc", "", "Số điện thoại không hợp lệ.")]
    [InlineData("012345", "", "Số điện thoại không hợp lệ.")]
    [InlineData("0909000111", "not-an-email", "Email không hợp lệ.")]
    public async Task CreateAsync_RejectsInvalidContactFields(string phone, string email, string expectedMessage)
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider();
        var service = new AppointmentService(db, clock);

        var result = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "Nguyen Thi Hoa",
            Phone = phone,
            Email = email,
            Service = "Makeup cao cap",
            StartAt = FutureLocalStart(clock, 10).ToString("O")
        }, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(AppointmentCreateFailure.InvalidRequest, result.Failure);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsTooLongNote()
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider();
        var service = new AppointmentService(db, clock);

        var result = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "Nguyen Thi Hoa",
            Phone = "0909000111",
            Service = "Makeup cao cap",
            Note = new string('x', 1001),
            StartAt = FutureLocalStart(clock, 10).ToString("O")
        }, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(AppointmentCreateFailure.InvalidRequest, result.Failure);
        Assert.Equal("Ghi chú không được vượt quá 1000 ký tự.", result.Message);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidStartTime()
    {
        await using var db = CreateDbContext();
        var service = new AppointmentService(db, new TestTimeProvider());

        var result = await service.CreateAsync(new CreateAppointmentRequest
        {
            CustomerName = "Nguyen Thi Hoa",
            Phone = "0909000111",
            Service = "Makeup cao cap",
            StartAt = "not-a-date"
        }, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(AppointmentCreateFailure.InvalidRequest, result.Failure);
        Assert.Equal("Thời gian đặt lịch không hợp lệ.", result.Message);
        Assert.Null(result.Appointment);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    private static BeautyDbContext CreateDbContext(string? databaseName = null, InMemoryDatabaseRoot? databaseRoot = null)
    {
        var options = new DbContextOptionsBuilder<BeautyDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"AppointmentServiceTests-{Guid.NewGuid():N}", databaseRoot)
            .Options;

        return new BeautyDbContext(options);
    }

    private static DateTimeOffset FutureLocalStart(TimeProvider clock, int hour) =>
        new(clock.GetUtcNow().AddDays(10).Date.AddHours(hour - 7), TimeSpan.Zero);

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public TestTimeProvider()
        {
            utcNow = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
