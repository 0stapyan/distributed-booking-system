namespace Shared.Messaging;

public interface IMessageIdAccessor
{
    Guid GetMessageId();
    string GetCorrelationId();
    IDictionary<string, object?> GetHeaders();

    internal void SetMessageId(Guid messageId);
    internal void SetCorrelationId(string correlationId);
    internal void SetHeaders(IDictionary<string, object?> headers);
}
