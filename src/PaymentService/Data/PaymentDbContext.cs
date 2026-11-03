using Microsoft.EntityFrameworkCore;
using PaymentService.Models;
using Shared.Data;
using Shared.Messaging;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.Property(p => p.Status).HasConversion<string>();
        });

        modelBuilder.Entity<OutboxMessage>(e => e.HasKey(m => m.Id));
        modelBuilder.Entity<InboxMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.MessageType).HasMaxLength(256).IsRequired();
        });
    }
}
