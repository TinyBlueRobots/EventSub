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
    readonly IBus _bus;
    readonly int[] _retryIntervals;
    readonly Uri _url;

    public MessageHandler(int[] retryIntervals, Uri url, IBus bus)
    {
        _retryIntervals = retryIntervals;
        _url = url;
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
        var json = Json.Serialize(message.Data);
        var response = await httpClient.PostAsync(_url, new StringContent(json, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
    }
}