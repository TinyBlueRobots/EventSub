using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSub
{
    interface ISqlClient
    {
        Task<Dictionary<string, (int, int)>> GetMessageCounts();
        Task<(int, int)> GetMessageCount(string name);
        Task DeleteSubscriber(string name);
        Task CreateSubscriber(Subscriber subscriber);
        Task CreateSubscribersTable();
        Task<IEnumerable<Subscriber>> ReadSubscribers();
        Task<Subscriber?> ReadSubscriber(string name);
    }
}