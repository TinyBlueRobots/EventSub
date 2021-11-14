using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Retry.Simple;

namespace EventSub;

class MessageHandler : IHandleMessages<Message>, IHandleMessages<IFailed<Message>>
{
    static readonly HttpClient httpClient = new();
    readonly IBus bus;
    readonly int[] retryIntervals;
    readonly Uri url;

    public MessageHandler(int[] retryIntervals, Uri url, IBus bus)
    {
        this.retryIntervals = retryIntervals;
        this.url = url;
        this.bus = bus;
    }

    public Task Handle(IFailed<Message> message)
    {
        message.Headers.TryGetValue(Headers.DeferCount, out var deferCountValue);
        int.TryParse(deferCountValue, out var deferCount);
        return deferCount < retryIntervals.Length
            ? bus.Advanced.TransportMessage.Defer(TimeSpan.FromSeconds(retryIntervals[deferCount]))
            : bus.Advanced.TransportMessage.Deadletter(message.ErrorDescription);
    }

    public async Task Handle(Message message)
    {
        var json = JsonConvert.SerializeObject(message.Data);
        var response = await httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
    }
}