using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests
{
    public class test_all_endpoints
    {
        static IEnumerable<TestApi> TestApis => new[] { TestApi.MySql(), TestApi.SqlServer(), TestApi.PostgreSql() };

        [TestCaseSource(nameof(TestApis))]
        public async Task all_endpoints_work(TestApi testApi)
        {
            var subscriber = new Subscriber("test", new[] { "test" }, testApi.Handler.Url, null, null, null);
            var subscriberJson = JsonConvert.SerializeObject(subscriber);

            //create subscriber
            var response = await testApi.CreateSubscriber(subscriberJson);
            Assert.AreEqual(200, (int)response.StatusCode);

            //read subscriber
            response = await testApi.GetSubscriber(subscriber.Name);
            var actualSubscriberJson = await response.Content.ReadAsStringAsync();
            var actualSubscriber = JsonConvert.DeserializeObject<Subscriber>(actualSubscriberJson);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(subscriber), JToken.FromObject(actualSubscriber)));

            //read subscribers
            var actualSubscribersJson = await testApi.GetSubscribers();
            actualSubscriber = JsonConvert.DeserializeObject<Subscriber[]>(actualSubscribersJson)[0];
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(subscriber), JToken.FromObject(actualSubscriber)));

            //send message
            var json = JsonConvert.SerializeObject(new { type = "test", data = new { message = "Hello" } });
            response = await testApi.PublishMessage(json);
            Assert.AreEqual(200, (int)response.StatusCode);
            var expected = JsonConvert.SerializeObject(new { message = "Hello" });
            Assert.AreEqual(expected, testApi.Handler.Requests[0].Body);

            //delete subscriber
            response = await testApi.DeleteSubscriber(subscriber.Name);
            Assert.AreEqual(200, (int)response.StatusCode);
            response = await testApi.GetSubscriber(subscriber.Name);
            Assert.AreEqual(404, (int)response.StatusCode);
        }
    }
}