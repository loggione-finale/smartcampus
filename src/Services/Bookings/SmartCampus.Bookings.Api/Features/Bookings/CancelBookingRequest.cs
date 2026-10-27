using System.ComponentModel.DataAnnotations;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public record CancelBookingRequest([StringLength(500)] string? Reason);
