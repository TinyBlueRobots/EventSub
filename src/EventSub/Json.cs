using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EventSub;

static class Json
{
    static readonly JsonSerializerSettings? settings;

    static Json()
    {
        settings = new JsonSerializerSettings();
        settings.Converters.Add(new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() });
        settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        settings.NullValueHandling = NullValueHandling.Ignore;
        settings.MissingMemberHandling = MissingMemberHandling.Ignore;
    }

    internal static string Serialize(object obj) => JsonConvert.SerializeObject(obj, settings);
    internal static T? Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, settings);
}

class JsonException : Exception
{
}