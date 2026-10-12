namespace Booking.Domain.Entities;

public sealed class TransportRequestHorse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransportRequestId { get; set; }
    public Guid HorseId { get; set; }
    public int SequenceNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
