using Beauty.Api.Data;
using Beauty.Api.Models;

namespace Beauty.Api.Services;

public sealed class AiDraftService(BeautyDbContext db)
{
    public async Task<AiDraft> CreateDraftAsync(CreateAiDraftRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var content = request.Type == "customer-makeup-image"
            ? $"Bản nháp bài đăng từ ảnh khách: {request.Prompt}. Nội dung này cần admin duyệt trước khi đăng."
            : $"Bản nháp bài sale sản phẩm: {request.Prompt}. Nội dung này không tự đổi giá hoặc kích hoạt sale.";

        var draft = new AiDraft
        {
            Type = request.Type,
            Prompt = request.Prompt,
            SourceImagePrivatePath = request.SourceImagePrivatePath,
            Content = content,
            Status = DraftStatus.Draft,
            CreatedByUserId = userId
        };

        db.AiDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);
        return draft;
    }
}
