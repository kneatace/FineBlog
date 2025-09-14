namespace FineBlog.ViewModels
{
    public class PostVM
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? AuthorName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ThumbnailUrl { get; set; }
        public List<string> Tags { get; set; } = new List<string>(); // New property for tags

        public string TagsDisplay => Tags.Any() ? string.Join(", ", Tags) : "No Tags";
    }
}
