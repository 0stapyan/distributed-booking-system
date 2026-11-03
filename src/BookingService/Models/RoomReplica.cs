namespace BookingService.Models;

public class RoomReplica
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Type { get; set; }
    public decimal PricePerNight { get; set; }
    public int MaxGuests { get; set; }
    public bool IsAvailable { get; set; }

    public long ReplicaLsn { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime UpdatedAt { get; set; }
}
