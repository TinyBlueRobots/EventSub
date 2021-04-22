using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSub
{
    interface ISqlClient
    {
        Task<Dictionary<string, (int, int)>> GetMessageCounts();
        Task DeleteSubscriber(string name);
        Task CreateSubscriber(Subscriber subscriber);
        Task CreateSubscribersTable();
        Task<IEnumerable<Subscriber>> ReadSubscribers();
    }
}