using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests;

public class test_all_endpoints
{
    static IEnumerable<TestApi> TestApis => new[] { TestApi.MySql(), TestApi.SqlServer(), TestApi.PostgreSql() };

    [TestCaseSource(nameof(TestApis))]
    public async Task all_endpoints_work(TestApi testApi)
    {
        var subscriber1 = new Subscriber("test1", new[] { "test" }, testApi.Handler1.Url, null, null, null);
        var subscriber1Json = JsonConvert.SerializeObject(subscriber1);

        var subscriber2 = new Subscriber("test2", new[] { "test" }, testApi.Handler2.Url, null, null, null);
        var subscriber2Json = JsonConvert.SerializeObject(subscriber2);

        //create subscriber1
        var response = await testApi.CreateSubscriber(subscriber1Json);
        Assert.AreEqual(200, (int)response.StatusCode);

        //create subscriber2
        response = await testApi.CreateSubscriber(subscriber2Json);
        Assert.AreEqual(200, (int)response.StatusCode);

        //read subscriber1
        response = await testApi.GetSubscriber(subscriber1.Name);
        var actualSubscriberJson = await response.Content.ReadAsStringAsync();
        var actualSubscriber = JsonConvert.DeserializeObject<Subscriber>(actualSubscriberJson);
        Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(subscriber1), JToken.FromObject(actualSubscriber)));

        //read subscriber2
        response = await testApi.GetSubscriber(subscriber2.Name);
        actualSubscriberJson = await response.Content.ReadAsStringAsync();
        actualSubscriber = JsonConvert.DeserializeObject<Subscriber>(actualSubscriberJson);
        Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(subscriber2), JToken.FromObject(actualSubscriber)));

        //read subscribers
        var actualSubscribersJson = await testApi.GetSubscribers();
        var actualSubscribers = JsonConvert.DeserializeObject<Subscriber[]>(actualSubscribersJson);
        Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(new[] { subscriber1, subscriber2 }),
            JToken.FromObject(actualSubscribers)));

        //send message
        var json = JsonConvert.SerializeObject(new { type = "test", data = new { message = "Hello" } });
        response = await testApi.PublishMessage(json);
        Assert.AreEqual(200, (int)response.StatusCode);
        var expected = JsonConvert.SerializeObject(new { message = "Hello" });
        Assert.AreEqual(expected, testApi.Handler1.Requests[0].Body);
        Assert.AreEqual(expected, testApi.Handler2.Requests[0].Body);

        //delete subscriber1
        response = await testApi.DeleteSubscriber(subscriber1.Name);
        Assert.AreEqual(200, (int)response.StatusCode);
        response = await testApi.GetSubscriber(subscriber1.Name);
        Assert.AreEqual(404, (int)response.StatusCode);

        //delete subscriber2
        response = await testApi.DeleteSubscriber(subscriber2.Name);
        Assert.AreEqual(200, (int)response.StatusCode);
        response = await testApi.GetSubscriber(subscriber2.Name);
        Assert.AreEqual(404, (int)response.StatusCode);

        //delete subscriber2 again
        response = await testApi.DeleteSubscriber(subscriber2.Name);
        Assert.AreEqual(200, (int)response.StatusCode);
        response = await testApi.GetSubscriber(subscriber2.Name);
        Assert.AreEqual(404, (int)response.StatusCode);
    }
}