namespace GuestService.Middleware;

public class MaintenanceModeOptions
{
    public const string SectionName = "MaintenanceMode";

    public bool Read  { get; set; }
    public bool Write { get; set; }
}