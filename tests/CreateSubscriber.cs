using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests
{
    public class CreateSubscriber
    {
        Subscriber expectedSubscriber;
        HttpResponseMessage createSubscriberResponse;
        Subscriber actualSubscriber;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var testApi = new TestApi();
            expectedSubscriber = new Subscriber(nameof(CreateSubscriber), new[] { "test" }, testApi.Handler.Url, null, null, null);
            var subscriberJson = JsonConvert.SerializeObject(expectedSubscriber);
            createSubscriberResponse = await testApi.RegisterSubscriber(subscriberJson);
            var subscribersJson = await testApi.GetSubscribers();
            actualSubscriber = JsonConvert.DeserializeObject<Subscriber[]>(subscribersJson)[0];
        }

        [Test]
        public void status_code_is_200()
        {
            Assert.AreEqual(200, (int)createSubscriberResponse.StatusCode);
        }

        [Test]
        public void subscriber_is_returned()
        {
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(expectedSubscriber), JToken.FromObject(actualSubscriber)));
        }
    }
}