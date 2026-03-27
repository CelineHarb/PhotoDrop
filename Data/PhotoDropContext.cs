using Microsoft.EntityFrameworkCore;
using PhotoDrop.Models;

namespace PhotoDrop.Data
{
    public class PhotoDropContext : DbContext
    {
        public PhotoDropContext(DbContextOptions<PhotoDropContext> options) : base(options) { }
        public DbSet<EventRecord> Events { get; set; }
    }
}
