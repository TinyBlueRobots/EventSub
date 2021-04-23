using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        TestApi testApi;

        [SetUp]
        public void Setup()
        {
            testApi = new TestApi();
        }

        [Test]
        public async Task CreateSubscriber()
        {
            var expectedSubscriber = new Subscriber("test", new[] { "test" }, testApi.Handler.Url, null, null, null);
            var subscriberJson = JsonConvert.SerializeObject(expectedSubscriber);
            var response = await testApi.RegisterSubscriber(subscriberJson);
            var subscribersJson = await testApi.GetSubscribers();
            var actualSubscriber = JsonConvert.DeserializeObject<Subscriber[]>(subscribersJson)[0];
            Assert.AreEqual(200, (int)response.StatusCode);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(expectedSubscriber), JToken.FromObject(actualSubscriber)));
        }
    }
}