using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Beauty.Api.Services;

public sealed class AppointmentService(BeautyDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan Duration = TimeSpan.FromHours(2);
    private static readonly SemaphoreSlim BookingGate = new(1, 1);
    private static readonly Regex PhoneCharacters = new(@"^[0-9+().\-\s]+$", RegexOptions.Compiled);

    public async Task<AppointmentCreateResult> CreateAsync(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return AppointmentCreateResult.Invalid(validationError);
        }

        if (!DateTimeOffset.TryParse(request.StartAt, out var parsedStart))
        {
            return AppointmentCreateResult.Invalid("Thời gian đặt lịch không hợp lệ.");
        }

        var start = parsedStart.ToUniversalTime();
        if (start < clock.GetUtcNow().AddMinutes(-5))
        {
            return AppointmentCreateResult.Invalid("Không thể đặt lịch trong quá khứ.");
        }

        var end = start.Add(Duration);
        await BookingGate.WaitAsync(cancellationToken);
        try
        {
            var hasOverlap = await db.Appointments.AnyAsync(appointment =>
                appointment.Status != AppointmentStatus.Cancelled &&
                appointment.Status != AppointmentStatus.Rejected &&
                start < appointment.EndAt &&
                end > appointment.StartAt,
                cancellationToken);

            if (hasOverlap)
            {
                return AppointmentCreateResult.Overlap("Khung giờ này đã có lịch hẹn.");
            }

            var appointment = new Appointment
            {
                CustomerName = request.CustomerName.Trim(),
                Phone = request.Phone.Trim(),
                Email = request.Email.Trim(),
                Address = request.Address.Trim(),
                Service = request.Service.Trim(),
                Tone = request.Tone.Trim(),
                Note = request.Note.Trim(),
                StartAt = start,
                EndAt = end
            };

            db.Appointments.Add(appointment);
            await db.SaveChangesAsync(cancellationToken);
            return AppointmentCreateResult.Success(appointment);
        }
        finally
        {
            BookingGate.Release();
        }
    }

    private static string? Validate(CreateAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Service))
        {
            return "Vui lòng nhập đầy đủ tên, số điện thoại và dịch vụ.";
        }

        if (request.CustomerName.Trim().Length > 120)
        {
            return "Tên khách hàng không được vượt quá 120 ký tự.";
        }

        var phone = request.Phone.Trim();
        var digitCount = phone.Count(char.IsDigit);
        if (phone.Length > 24 || digitCount < 9 || digitCount > 15 || !PhoneCharacters.IsMatch(phone))
        {
            return "Số điện thoại không hợp lệ.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (email.Length > 254 || !IsValidEmail(email))
            {
                return "Email không hợp lệ.";
            }
        }

        if (request.Address.Trim().Length > 240)
        {
            return "Địa chỉ không được vượt quá 240 ký tự.";
        }

        if (request.Service.Trim().Length > 120)
        {
            return "Tên dịch vụ không được vượt quá 120 ký tự.";
        }

        if (request.Tone.Trim().Length > 120)
        {
            return "Tone/chuyên viên không được vượt quá 120 ký tự.";
        }

        if (request.Note.Trim().Length > 1000)
        {
            return "Ghi chú không được vượt quá 1000 ký tự.";
        }

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public enum AppointmentCreateFailure
{
    None,
    InvalidRequest,
    Overlap
}

public sealed record AppointmentCreateResult(
    bool Created,
    AppointmentCreateFailure Failure,
    string Message,
    Appointment? Appointment)
{
    public static AppointmentCreateResult Success(Appointment appointment) =>
        new(true, AppointmentCreateFailure.None, "Lịch hẹn đã được tạo ở trạng thái chờ xác nhận.", appointment);

    public static AppointmentCreateResult Invalid(string message) =>
        new(false, AppointmentCreateFailure.InvalidRequest, message, null);

    public static AppointmentCreateResult Overlap(string message) =>
        new(false, AppointmentCreateFailure.Overlap, message, null);
}
