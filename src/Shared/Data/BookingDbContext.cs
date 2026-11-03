using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Models;

namespace Shared.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hotel>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasMany(h => h.Rooms).WithOne().HasForeignKey(r => r.HotelId);
        });

        modelBuilder.Entity<Room>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.PricePerNight).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Guest>(e => e.HasKey(g => g.Id));

        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.TotalPrice).HasColumnType("decimal(18,2)");
            e.Ignore(b => b.Nights); // computed property
            e.HasMany(b => b.Guests).WithOne().HasForeignKey(bg => bg.BookingId);
        });

        modelBuilder.Entity<BookingGuest>(e => e.HasKey(bg => bg.Id));
        modelBuilder.Entity<OutboxMessage>(e => e.HasKey(m => m.Id));
        modelBuilder.Entity<InboxMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.MessageType).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<Hotel>().HasData(
            new Hotel { Id = 1, Name = "Grand Kyiv Hotel", Address = "вул. Хрещатик, 1", City = "Київ", StarRating = 5, Description = "Готель у серці Києва" },
            new Hotel { Id = 2, Name = "Lviv Palace", Address = "пл. Ринок, 10", City = "Львів", StarRating = 4, Description = "Бутік-готель у старому місті" }
        );

        modelBuilder.Entity<Room>().HasData(
            new Room { Id = 1, HotelId = 1, RoomNumber = "101", Type = RoomType.Single, PricePerNight = 1500, MaxGuests = 1, IsAvailable = true },
            new Room { Id = 2, HotelId = 1, RoomNumber = "201", Type = RoomType.Double, PricePerNight = 2500, MaxGuests = 2, IsAvailable = true },
            new Room { Id = 3, HotelId = 1, RoomNumber = "301", Type = RoomType.Suite, PricePerNight = 5000, MaxGuests = 4, IsAvailable = true },
            new Room { Id = 4, HotelId = 2, RoomNumber = "101", Type = RoomType.Double, PricePerNight = 2000, MaxGuests = 2, IsAvailable = true }
        );

        modelBuilder.Entity<Guest>().HasData(
            new Guest { Id = 1, FirstName = "Олексій", LastName = "Мельник", Email = "melnyk@example.com", Phone = "+380991234567", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Guest { Id = 2, FirstName = "Марія", LastName = "Коваль", Email = "koval@example.com", Phone = "+380992345678", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
