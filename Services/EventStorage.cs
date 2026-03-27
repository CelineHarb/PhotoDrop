using PhotoDrop.Data;
using PhotoDrop.Models;
using Microsoft.EntityFrameworkCore;

namespace PhotoDrop.Services;

public class EventStorage
{
    private readonly PhotoDropContext _db;

    public EventStorage(PhotoDropContext db)
    {
        _db = db;
    }

    public EventRecord Add(string eventName, string folderId, string accessToken)
    {
        var record = new EventRecord
        {
            EventName = eventName,
            FolderId = folderId,
            AccessToken = accessToken,
            GuestToken = GenerateToken()
        };

        _db.Events.Add(record);
        _db.SaveChanges();
        return record;
    }

    public EventRecord? GetByToken(string token)
    {
        return _db.Events.FirstOrDefault(e => e.GuestToken == token);
    }

    public EventRecord? GetById(string id)
    {
        return _db.Events.Find(id);
    }

    public void Update(EventRecord record)
    {
        _db.Events.Update(record);
        _db.SaveChanges();
    }

    private static string GenerateToken()
    {
        return Guid.NewGuid().ToString("N")[..12];
    }
}