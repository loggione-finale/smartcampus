namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public interface IClosingCommentService
{
    Task<ClosingCommentResult> SubmitAsync(
        string code,
        Guid userId,
        SubmitClosingCommentRequest request,
        CancellationToken cancellationToken = default);
}
