using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests
{
    public class PublishMessage
    {
        TestApi testApi;
        HttpResponseMessage response;

        [OneTimeSetUp]
        public async Task Setup()
        {
            testApi = new TestApi();
            var expectedSubscriber = new Subscriber(nameof(PublishMessage), new[] { "test" }, testApi.Handler.Url, null, null, null);
            var subscriberJson = JsonConvert.SerializeObject(expectedSubscriber);
            await testApi.RegisterSubscriber(subscriberJson);
            var json = JsonConvert.SerializeObject(new { type = "test", data = new { message = "Hello" } });
            response = await testApi.PublishMessage(json);
        }

        [Test]
        public void status_code_is_200()
        {
            Assert.AreEqual(200, (int)response.StatusCode);
        }

        [Test]
        public void handler_received_event()
        {
            var expected = JsonConvert.SerializeObject(new { message = "Hello" });
            Assert.AreEqual(expected, testApi.Handler.Requests[0].Body);
        }
    }
}