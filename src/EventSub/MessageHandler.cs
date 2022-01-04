using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Retry.Simple;

namespace EventSub;

class MessageHandler : IHandleMessages<Message>, IHandleMessages<IFailed<Message>>
{
    static readonly HttpClient httpClient = new();
    readonly string? _apiKey;
    readonly IBus _bus;
    readonly int[] _retryIntervals;
    readonly Uri _url;

    public MessageHandler(int[]? retryIntervals, Uri url, string? apiKey, IBus bus)
    {
        _retryIntervals = retryIntervals ?? Array.Empty<int>();
        _url = url;
        _apiKey = apiKey;
        _bus = bus;
    }

    public Task Handle(IFailed<Message> message)
    {
        message.Headers.TryGetValue(Headers.DeferCount, out var deferCountValue);
        int.TryParse(deferCountValue, out var deferCount);
        return deferCount < _retryIntervals.Length
            ? _bus.Advanced.TransportMessage.Defer(TimeSpan.FromSeconds(_retryIntervals[deferCount]))
            : _bus.Advanced.TransportMessage.Deadletter(message.ErrorDescription);
    }

    public async Task Handle(Message message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _url)
        {
            Content = new StringContent(Json.Serialize(message.Data), Encoding.UTF8, "application/json")
        };
        if (_apiKey is not null) request.Headers.Add("X-API-KEY", _apiKey);
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}