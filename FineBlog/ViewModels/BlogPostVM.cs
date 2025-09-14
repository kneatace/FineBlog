using FineBlog.Models;

namespace FineBlog.ViewModels
{
    public class BlogPostVM
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? AuthorName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }

        // New properties
        public List<Comment> Comments { get; set; } = new();
        public List<Tag> Tags { get; set; } = new();
    }
}

