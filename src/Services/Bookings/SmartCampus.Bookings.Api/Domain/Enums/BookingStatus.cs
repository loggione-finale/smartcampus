namespace SmartCampus.Bookings.Api.Domain;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Closed,

    /// <summary>
    /// Stato derivato/calcolato temporalmente (esposto nei DTO verso i client come MyBookingResponse)
    /// quando una prenotazione Confirmed ha superato l'orario di fine e attende il commento finale.
    /// Non viene persistito sul database relazionale per garantire la consistenza temporale naturale.
    /// </summary>
    AwaitingComment
}
