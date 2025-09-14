// Models/Comment.cs
using FineBlog.Models;

public class Comment
{
    public int Id { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string AuthorName { get; set; } // Optional: If you allow anonymous comments
    public string? AuthorEmail { get; set; } // Optional: For notifications or gravatar
    public int PostId { get; set; } // Foreign key to associate with a post
    public Post Post { get; set; } // Navigation property
}