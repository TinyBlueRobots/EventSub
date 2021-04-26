namespace EventSub
{
    record Subscriber(string Name, string[] Types, string Url, int[] RetryIntervals, int? MaxParallelism, int? NumberOfWorkers) { }
}
