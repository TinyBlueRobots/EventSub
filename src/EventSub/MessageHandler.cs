using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using Rebus.Messages;
using Rebus.Exceptions;

namespace EventSub
{
    class MessageHandler : IHandleMessages<Message>, IHandleMessages<IFailed<Message>>
    {
        static readonly HttpClient httpClient = new();
        readonly int[] retryIntervals;
        readonly Uri url;
        readonly IBus bus;

        public MessageHandler(int[] retryIntervals, Uri url, IBus bus)
        {
            this.retryIntervals = retryIntervals;
            this.url = url;
            this.bus = bus;
        }

        public async Task Handle(Message message)
        {
            var json = JsonConvert.SerializeObject(message.Data);
            var response = await httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }

        public Task Handle(IFailed<Message> message)
        {
            message.Headers.TryGetValue(Headers.DeferCount, out var deferCountValue);
            int.TryParse(deferCountValue, out var deferCount);
            return deferCount < retryIntervals.Length ? bus.Advanced.TransportMessage.Defer(TimeSpan.FromSeconds(retryIntervals[deferCount])) : bus.Advanced.TransportMessage.Deadletter(message.ErrorDescription);
        }
    }
}
