using System.Data.Entity;
using VideoTimelineApp.Models;

namespace VideoTimelineApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base("name=DefaultConnection") { }

        public DbSet<VideoSession> VideoSessions { get; set; }
        public DbSet<Segment>      Segments      { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Segment>()
                .HasRequired(s => s.VideoSession)
                .WithMany(v => v.Segments)
                .HasForeignKey(s => s.VideoSessionId)
                .WillCascadeOnDelete(true);  // deleting a VideoSession deletes its Segments

            base.OnModelCreating(modelBuilder);
        }
    }
}
