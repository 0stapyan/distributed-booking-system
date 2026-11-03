namespace GuestService.Application.DTOs;

public record CreateGuestDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone
);

public record UpdateGuestDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone
);
