using AspNetCoreHero.ToastNotification.Abstractions;
using FineBlog.Data;
using FineBlog.Models;
using FineBlog.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineBlog.Controllers
{
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        public INotyfService _notification { get; }

        public BlogController(ApplicationDbContext context, INotyfService notification)
        {
            _context = context;
            _notification = notification;
        }

        [HttpGet("[controller]/{slug}")]
        public IActionResult Post(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                _notification.Error("Post not found");
                return NotFound(); // Return 404 instead of an empty view
            }

            // ✅ Fetch Post with related data: User, Comments, and Tags
            var post = _context.Posts!
                .Include(p => p.ApplicationUser) // Fetch author details
                .Include(p => p.Comments) // Fetch comments
                .Include(p => p.PostTags) // Fetch related PostTags
                    .ThenInclude(pt => pt.Tag) // Fetch associated Tags
                .FirstOrDefault(p => p.Slug == slug);

            if (post == null)
            {
                _notification.Error("Post not found");
                return NotFound(); // Return 404 if post doesn't exist
            }

            var vm = new BlogPostVM()
            {
                Id = post.Id,
                Title = post.Title,
                AuthorName = post.ApplicationUser != null
                    ? post.ApplicationUser.FirstName + " " + post.ApplicationUser.LastName
                    : "Unknown",
                CreatedDate = post.CreatedDate,
                ThumbnailUrl = post.ThumbnailUrl,
                Description = post.Description,
                ShortDescription = post.ShortDescription,
                Comments = post.Comments.ToList(),
                Tags = post.PostTags.Select(pt => pt.Tag).ToList() // ✅ Fetch Tags correctly
            };

            return View(vm);
        }
    }
}
