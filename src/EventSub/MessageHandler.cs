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
        static readonly HttpClient httpClient = new HttpClient();
        readonly int[] retryIntervals;
        readonly string name;
        readonly Uri uri;
        readonly IBus bus;

        public MessageHandler(int[] retryIntervals, string name, Uri uri, IBus bus)
        {
            this.retryIntervals = retryIntervals ?? new int[0];
            this.name = name;
            this.uri = uri;
            this.bus = bus;
        }

        public async Task Handle(Message message)
        {
            var json = JsonConvert.SerializeObject(message.Data);
            var response = await httpClient.PostAsync(uri, new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }

        public Task Handle(IFailed<Message> message)
        {
            string? deferCountValue;
            message.Headers.TryGetValue(Headers.DeferCount, out deferCountValue);
            int deferCount;
            int.TryParse(deferCountValue, out deferCount);
            if (deferCount < retryIntervals.Length)
            {
                return bus.Advanced.TransportMessage.Defer(TimeSpan.FromSeconds(retryIntervals[deferCount]));
            }
            else
            {
                return bus.Advanced.TransportMessage.Deadletter(message.ErrorDescription);
            }
        }
    }
}
