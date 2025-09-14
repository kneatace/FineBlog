using FineBlog.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; // Required for IFormFile
using System.Collections.Generic; // Required for List<T>

namespace FineBlog.ViewModels
{
    public class CreatePostVM
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }

        public string? ShortDescription { get; set; }

        public string? ApplicationUserId { get; set; }

        public string? Description { get; set; }

        public string? ThumbnailUrl { get; set; }

        public IFormFile? Thumbnail { get; set; }

        // 🔹 New: Tags input as comma-separated values
        public string? TagInput { get; set; }

        // 🔹 New: List of selected tags
        public List<string>? SelectedTags { get; set; }
    }
}
