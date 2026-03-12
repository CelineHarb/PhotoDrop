using Microsoft.AspNetCore.Authentication.BearerToken;
using PhotoDrop.Models;

namespace PhotoDrop.Services
{
    public class EventStorage
    {
        private readonly List<EventRecord> _events = new();

        public EventRecord Add(string eventName, string folderId, string accessToken)
        {
            var record = new EventRecord
            {
                EventName = eventName,
                FolderId = folderId,
                AccessToken = accessToken,
                GuestToken = GenerateToken()
            };

            _events.Add(record);
            return record;
        }

        public EventRecord? GetByToken(string token)
        {
            return _events.FirstOrDefault(e => e.GuestToken == token);
        }

        private static string GenerateToken()
        {
            return Guid.NewGuid().ToString("N")[..12];
        }
    }
}
