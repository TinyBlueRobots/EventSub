namespace EventSub;

record Subscriber(string Name, string[] Types, string Url, string? ApiKey, int[] RetryIntervals, int? MaxParallelism,
    int? NumberOfWorkers);