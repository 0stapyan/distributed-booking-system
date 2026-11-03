namespace Shared.Messaging;

internal sealed class MessageIdAccessor : IMessageIdAccessor
{
    private Guid _messageId;
    private string _correlationId = string.Empty;
    private IDictionary<string, object?> _headers = new Dictionary<string, object?>();

    public Guid GetMessageId() => _messageId;
    public string GetCorrelationId() => _correlationId;
    public IDictionary<string, object?> GetHeaders() => _headers;

    public void SetMessageId(Guid messageId) => _messageId = messageId;
    public void SetCorrelationId(string correlationId) => _correlationId = correlationId;
    public void SetHeaders(IDictionary<string, object?> headers) => _headers = headers;
}
