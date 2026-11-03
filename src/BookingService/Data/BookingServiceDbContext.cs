using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Models;

namespace BookingService.Data;

public class BookingServiceDbContext : DbContext
{
    public BookingServiceDbContext(DbContextOptions<BookingServiceDbContext> options) : base(options) { }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<BookingSaga> BookingSagas => Set<BookingSaga>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<RoomReplica> RoomReplicas => Set<RoomReplica>();
    public DbSet<GuestReplica> GuestReplicas => Set<GuestReplica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.TotalPrice).HasColumnType("decimal(18,2)");
            e.Property(b => b.OwnerId).HasMaxLength(256);
            e.Ignore(b => b.Nights);
            e.HasMany(b => b.Guests).WithOne().HasForeignKey(bg => bg.BookingId);
        });

        modelBuilder.Entity<BookingGuest>(e => e.HasKey(bg => bg.Id));
        modelBuilder.Entity<OutboxMessage>(e => e.HasKey(m => m.Id));

        modelBuilder.Entity<BookingSaga>(e =>
        {
            e.HasKey(s => s.CorrelationId);
            e.Property(s => s.State).HasConversion<string>();
            e.Property(s => s.Amount).HasColumnType("decimal(18,2)");
            e.Property(s => s.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<InboxMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.MessageType).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<RoomReplica>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.PricePerNight).HasColumnType("decimal(18,2)");
            e.Property(r => r.ReplicaLsn).HasColumnName("replica_lsn");
            e.Property(r => r.IsDeleted).HasColumnName("is_deleted");
            e.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<GuestReplica>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Id).ValueGeneratedNever();
            e.Property(g => g.ReplicaLsn).HasColumnName("replica_lsn");
            e.Property(g => g.IsDeleted).HasColumnName("is_deleted");
            e.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
