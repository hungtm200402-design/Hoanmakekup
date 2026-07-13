using System.Text.Json;
using Beauty.Api.Data;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static async Task<IResult> GetAvailabilityAsync(
        string? date,
        AppointmentAvailabilityService service,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        {
            return Results.BadRequest(new { error = "Ngày cần kiểm tra không hợp lệ. Dùng định dạng YYYY-MM-DD." });
        }

        var slots = await service.GetForDateAsync(parsedDate, cancellationToken);
        return Results.Ok(slots);
    }

    public static async Task<IResult> CreateAppointmentAsync(
        HttpContext context,
        AppointmentService service,
        CancellationToken cancellationToken)
    {
        CreateAppointmentRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateAppointmentRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Dữ liệu đặt lịch không hợp lệ." });
        }

        if (request is null)
        {
            return Results.BadRequest(new { error = "Dữ liệu đặt lịch không hợp lệ." });
        }

        var result = await service.CreateAsync(request, cancellationToken);
        if (result.Created)
        {
            return Results.Created($"/api/appointments/{result.Appointment!.Id}", result.Appointment);
        }

        return result.Failure switch
        {
            AppointmentCreateFailure.Overlap => Results.Conflict(new { error = result.Message }),
            _ => Results.BadRequest(new { error = result.Message })
        };
    }

    public static async Task<IResult> GetAdminAppointmentsAsync(HttpRequest request, BeautyDbContext db, CancellationToken cancellationToken)
    {
        var filters = ParseFilters(request.Query);
        if (filters.Error is not null)
        {
            return Results.BadRequest(new { error = filters.Error });
        }

        var query = db.Appointments.AsQueryable();
        if (!string.IsNullOrEmpty(filters.CustomerName))
        {
            var name = filters.CustomerName.ToLowerInvariant();
            query = query.Where(appointment => appointment.CustomerName.ToLower().Contains(name));
        }
        if (!string.IsNullOrEmpty(filters.Phone))
        {
            query = query.Where(appointment => appointment.Phone.Contains(filters.Phone));
        }
        if (filters.Status.HasValue)
        {
            query = query.Where(appointment => appointment.Status == filters.Status.Value);
        }
        if (!string.IsNullOrEmpty(filters.Service))
        {
            var service = filters.Service.ToLowerInvariant();
            query = query.Where(appointment => appointment.Service.ToLower().Contains(service));
        }
        if (filters.FromDate.HasValue)
        {
            var from = new DateTimeOffset(filters.FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(appointment => appointment.StartAt >= from);
        }
        if (filters.ToDate.HasValue)
        {
            var until = new DateTimeOffset(filters.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(appointment => appointment.StartAt < until);
        }

        var rows = (await query.ToListAsync(cancellationToken))
            .OrderBy(appointment => appointment.StartAt)
            .ToList();
        return Results.Ok(rows);
    }

    private static AppointmentFilters ParseFilters(IQueryCollection query)
    {
        var customerName = query["customerName"].ToString().Trim();
        var phone = query["phone"].ToString().Trim();
        var service = query["service"].ToString().Trim();
        if (customerName.Length > 120 || phone.Length > 32 || service.Length > 120)
        {
            return new(null, null, null, null, null, null, "Giá trị bộ lọc quá dài.");
        }

        AppointmentStatus? status = null;
        var parsedStatus = default(AppointmentStatus);
        var statusText = query["status"].ToString().Trim();
        if (statusText.Length > 0 && (!Enum.TryParse<AppointmentStatus>(statusText, true, out parsedStatus) || !Enum.IsDefined(parsedStatus)))
        {
            return new(null, null, null, null, null, null, "Trạng thái lịch hẹn không hợp lệ.");
        }
        if (statusText.Length > 0) status = parsedStatus;

        if (!TryParseDate(query["fromDate"].ToString(), out var fromDate) || !TryParseDate(query["toDate"].ToString(), out var toDate))
        {
            return new(null, null, null, null, null, null, "Khoảng ngày không hợp lệ. Dùng định dạng YYYY-MM-DD.");
        }
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            return new(null, null, null, null, null, null, "Ngày bắt đầu không được sau ngày kết thúc.");
        }

        return new(customerName, phone, status, service, fromDate, toDate, null);
    }

    private static bool TryParseDate(string value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }
        var parsed = DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", out var result);
        date = parsed ? result : null;
        return parsed;
    }

    private sealed record AppointmentFilters(
        string? CustomerName,
        string? Phone,
        AppointmentStatus? Status,
        string? Service,
        DateOnly? FromDate,
        DateOnly? ToDate,
        string? Error);

    public static async Task<IResult> UpdateAdminAppointmentStatusAsync(
        Guid id,
        UpdateAppointmentStatusRequest request,
        BeautyDbContext db,
        CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.FindAsync([id], cancellationToken);
        if (appointment is null)
        {
            return Results.NotFound();
        }

        appointment.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(appointment);
    }
}
