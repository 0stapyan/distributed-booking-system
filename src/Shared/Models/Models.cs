namespace Shared.Models;

public class Hotel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int StarRating { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<Room> Rooms { get; set; } = new();
}

public class Room
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public RoomType Type { get; set; }
    public decimal PricePerNight { get; set; }
    public int MaxGuests { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class Guest
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Booking
{
    public int Id { get; set; }
    public int RoomId { get; set; }           // HotelId не потрібен — береться через Room.HotelId
    public string? OwnerId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Nights => (CheckOutDate - CheckInDate).Days;
    public List<BookingGuest> Guests { get; set; } = new();
}

public class BookingGuest
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int GuestId { get; set; }
}

public enum RoomType { Single, Double, Suite, Deluxe }

public enum BookingStatus { Pending, Confirmed, Cancelled, Completed }
