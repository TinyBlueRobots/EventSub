namespace EventSub
{
    record Subscriber(string Name, string[] Types, string Uri, int[] RetryIntervals, int? MaxParallelism, int? NumberOfWorkers) { }
}
