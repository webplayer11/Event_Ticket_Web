namespace EventGO.Infrastructure.Reservations;

public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    public TimeSpan Duration { get; set; }

    public TimeSpan SweepInterval { get; set; }

}
