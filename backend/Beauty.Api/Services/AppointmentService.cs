using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Services;

public sealed class AppointmentService(BeautyDbContext db)
{
    private static readonly TimeSpan Duration = TimeSpan.FromHours(2);

    public async Task<(bool Created, string Message, Appointment? Appointment)> CreateAsync(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(request.StartAt, out var parsedStart))
        {
            return (false, "Thời gian đặt lịch không hợp lệ.", null);
        }

        var start = parsedStart.ToUniversalTime();
        var end = start.Add(Duration);
        var hasOverlap = await db.Appointments.AnyAsync(appointment =>
            appointment.Status != AppointmentStatus.Cancelled &&
            appointment.Status != AppointmentStatus.Rejected &&
            start < appointment.EndAt &&
            end > appointment.StartAt,
            cancellationToken);

        if (hasOverlap)
        {
            return (false, "Khung giờ này đã có lịch hẹn.", null);
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
        return (true, "Lịch hẹn đã được tạo ở trạng thái chờ xác nhận.", appointment);
    }
}
