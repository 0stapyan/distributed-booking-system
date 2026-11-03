using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace BookingService.Tests.Saga;

public class BookingSagaConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("bookingdb")
        .WithUsername("booking")
        .WithPassword("booking123")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private BookingServiceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookingServiceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new BookingServiceDbContext(options);
    }

    [Fact]
    public async Task Two_concurrent_transitions_of_one_saga_record_exactly_one_wins()
    {
        var correlationId = Guid.NewGuid();

        await using (var seedDb = CreateContext())
        {
            seedDb.BookingSagas.Add(new BookingSaga
            {
                CorrelationId = correlationId,
                BookingId = 1,
                State = SagaState.Started,
                RoomId = 10,
                Amount = 250m,
                CheckInDate = DateTime.UtcNow.Date,
                CheckOutDate = DateTime.UtcNow.Date.AddDays(2),
                Guests = 2,
            });
            await seedDb.SaveChangesAsync();
        }

        await using var dbA = CreateContext();
        await using var dbB = CreateContext();

        var sagaA = await dbA.BookingSagas.SingleAsync(s => s.CorrelationId == correlationId);
        var sagaB = await dbB.BookingSagas.SingleAsync(s => s.CorrelationId == correlationId);

        sagaA.State = SagaState.RoomReserved;
        sagaA.Version++;

        sagaB.State = SagaState.Cancelled;
        sagaB.Version++;

        var results = await Task.WhenAll(
            SaveAndClassify(dbA),
            SaveAndClassify(dbB));

        Assert.Single(results, r => r == SaveOutcome.Committed);
        Assert.Single(results, r => r == SaveOutcome.ConcurrencyConflict);

        await using var verifyDb = CreateContext();
        var finalSaga = await verifyDb.BookingSagas.SingleAsync(s => s.CorrelationId == correlationId);
        Assert.Equal(1, finalSaga.Version);
        Assert.True(finalSaga.State == SagaState.RoomReserved || finalSaga.State == SagaState.Cancelled);
    }

    private enum SaveOutcome { Committed, ConcurrencyConflict }

    private static async Task<SaveOutcome> SaveAndClassify(BookingServiceDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return SaveOutcome.Committed;
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveOutcome.ConcurrencyConflict;
        }
    }
}
