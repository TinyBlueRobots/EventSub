using System;
using System.Collections.Generic;
using System.Threading;
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
        var subscriber1 = new Subscriber("test1", new[] { "test" }, testApi.Handler1.Url, "apikey", Array.Empty<int>(),
            null, null);
        var subscriber1Json = JsonConvert.SerializeObject(subscriber1);

        var subscriber2 = new Subscriber("test2", new[] { "test" }, testApi.Handler2.Url, "apikey", Array.Empty<int>(),
            null, null);
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
        Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(subscriber1), JToken.FromObject(actualSubscriber!)));

        //read subscriber2
        response = await testApi.GetSubscriber(subscriber2.Name);
        actualSubscriberJson = await response.Content.ReadAsStringAsync();
        actualSubscriber = JsonConvert.DeserializeObject<Subscriber>(actualSubscriberJson);
        Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(subscriber2), JToken.FromObject(actualSubscriber!)));

        //read subscribers
        var actualSubscribersJson = await testApi.GetSubscribers();
        var actualSubscribers = JsonConvert.DeserializeObject<Subscriber[]>(actualSubscribersJson);
        Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(new[] { subscriber1, subscriber2 }),
            JToken.FromObject(actualSubscribers!)));

        //send message
        var json = JsonConvert.SerializeObject(new { type = "test", data = new { message = "Hello" } });
        response = await testApi.PublishMessageHandler1And2(json);
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
        
        //create subscriber3
        var subscriber3 = new Subscriber("test3", new[] { "test" }, testApi.Handler3.Url, "apikey", new[] { 1 }, null,
            null);
        response = await testApi.DeleteSubscriber(subscriber3.Name);
        Assert.AreEqual(200, (int)response.StatusCode);
        var subscriber3Json = JsonConvert.SerializeObject(subscriber3);
        response = await testApi.CreateSubscriber(subscriber3Json);
        Assert.AreEqual(200, (int)response.StatusCode);

        //read active messages
        await testApi.PublishMessageHandler3(json);
        var messages = await testApi.ReadMessages("test3", false);
        Assert.AreEqual($"[{json}]", messages);
        
        //delete subscriber3
        response = await testApi.DeleteSubscriber(subscriber3.Name);
        Assert.AreEqual(200, (int)response.StatusCode);
        
        //create subscriber3
        subscriber3 = new Subscriber("test3", new[] { "test" }, testApi.Handler3.Url, "apikey", Array.Empty<int>(), null,
            null);
        subscriber3Json = JsonConvert.SerializeObject(subscriber3);
        response = await testApi.CreateSubscriber(subscriber3Json);
        Assert.AreEqual(200, (int)response.StatusCode);        
        
        //read and delete dead letters
        await testApi.PublishMessageHandler3(json);
        Thread.Sleep(1000);
        messages = await testApi.ReadDeadLetters("test3", true);
        Assert.AreEqual($"[{json}]", messages);
        messages = await testApi.ReadDeadLetters("test3", true);
        Assert.AreEqual("[]", messages);
        
        //delete subscriber3
        response = await testApi.DeleteSubscriber(subscriber3.Name);
        Assert.AreEqual(200, (int)response.StatusCode);        
    }
}