using FineBlog.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FineBlog.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ✅ Define DbSets for all models
        public DbSet<ApplicationUser> ApplicationUsers { get; set; } = null!;
        public DbSet<Post> Posts { get; set; } = null!;
        public DbSet<Page> Pages { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<PostTag> PostTags { get; set; } = null!; // ✅ Explicit join table

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Many-to-many: Post-Tag relationship using PostTag join table
            modelBuilder.Entity<PostTag>()
                .HasKey(pt => new { pt.PostId, pt.TagId }); // Composite primary key

            modelBuilder.Entity<PostTag>()
                .HasOne(pt => pt.Post)
                .WithMany(p => p.PostTags)
                .HasForeignKey(pt => pt.PostId);

            modelBuilder.Entity<PostTag>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.PostTags)
                .HasForeignKey(pt => pt.TagId);

            // ✅ One-to-many: Post-Comments relationship
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ One-to-many: ApplicationUser-Posts relationship
            modelBuilder.Entity<Post>()
                .HasOne(p => p.ApplicationUser)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
