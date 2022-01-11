using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSub;

interface IDbClient
{
    Task<Dictionary<string, (int, int)>> ReadMessageCounts();
    Task<(int, int)> ReadMessageCount(string name);
    Task DeleteSubscriber(string name);
    Task CreateSubscriber(Subscriber subscriber);
    Task CreateSubscribersTable();
    Task<IEnumerable<Subscriber>> ReadSubscribers();
    Task<Subscriber?> ReadSubscriber(string name);
    Task<IEnumerable<Message>> ReadMessages(string subscriberName, bool delete);
    Task<IEnumerable<Message>> ReadDeadLetters(string subscriberName, bool delete);
}