namespace BookingService.Application.ReadModels;

public record RoomView(int Id, int MaxGuests, bool IsAvailable);

public interface IMasterDataReader
{
    Task<RoomView?> GetRoomAsync(int roomId, CancellationToken cancellationToken);
    Task<bool> AllGuestsExistAsync(IReadOnlyCollection<int> guestIds, CancellationToken cancellationToken);
}
