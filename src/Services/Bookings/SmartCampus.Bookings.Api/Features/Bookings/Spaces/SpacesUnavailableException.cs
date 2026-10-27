namespace SmartCampus.Bookings.Api.Features.Bookings.Spaces;

public class SpacesUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
