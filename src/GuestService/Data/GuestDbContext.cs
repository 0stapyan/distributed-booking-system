using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace GuestService.Data;

public class GuestDbContext : DbContext
{
    public GuestDbContext(DbContextOptions<GuestDbContext> options) : base(options) { }

    public DbSet<Guest> Guests => Set<Guest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guest>(e => e.HasKey(g => g.Id));
    }
}
