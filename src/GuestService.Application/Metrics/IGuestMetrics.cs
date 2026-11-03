namespace GuestService.Application.Metrics;

public interface IGuestMetrics
{
    void GuestCreated();
    void RecordCreationDuration(double milliseconds);
}