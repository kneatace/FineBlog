// Models/Tag.cs
namespace FineBlog.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        // Many-to-many relationship with Post
        public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    }
}