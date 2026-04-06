using PhotoDrop.Data;
using PhotoDrop.Models;

namespace PhotoDrop.Services;

public class GuestSessionService
{
    private readonly PhotoDropContext _db;

    public GuestSessionService(PhotoDropContext db)
    {
        _db = db;
    }

    public GuestSession GetOrCreateSession(string eventId, string sessionToken, string? ipAddress)
    {
        var session = _db.GuestSessions
            .FirstOrDefault(s => s.EventId == eventId && s.SessionToken == sessionToken);

        if (session is not null)
            return session;

        session = new GuestSession
        {
            EventId = eventId,
            SessionToken = sessionToken,
            IpAddress = ipAddress
        };

        _db.GuestSessions.Add(session);
        _db.SaveChanges();
        return session;
    }

    public void IncrementPhotoCount(string sessionId, int count = 1)
    {
        var session = _db.GuestSessions.Find(sessionId);
        if (session is not null)
        {
            session.PhotosUploaded += count;
            session.LastUploadAt = DateTime.UtcNow;
            _db.SaveChanges();
        }
    }

    public int GetPhotoCount(string eventId, string sessionToken)
    {
        var session = _db.GuestSessions
            .FirstOrDefault(s => s.EventId == eventId && s.SessionToken == sessionToken);

        return session?.PhotosUploaded ?? 0;
    }
}