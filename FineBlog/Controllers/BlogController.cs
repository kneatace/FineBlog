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
                return NotFound();
            }

            var post = _context.Posts!
                .Include(p => p.ApplicationUser)
                .Include(p => p.Comments)
                .Include(p => p.PostTags)
                    .ThenInclude(pt => pt.Tag)
                .FirstOrDefault(p => p.Slug == slug);

            if (post == null)
            {
                _notification.Error("Post not found");
                return NotFound();
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
                Tags = post.PostTags.Select(pt => pt.Tag).ToList()
            };

            return View(vm);
        }

        // Tag filtering action
        public IActionResult Index(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                // Filter posts by tag
                var posts = _context.Posts!
                    .Where(p => p.IsPublished && p.PostTags.Any(pt => pt.Tag != null && pt.Tag.Name == tag))
                    .Include(p => p.ApplicationUser)
                    .Include(p => p.PostTags)
                        .ThenInclude(pt => pt.Tag)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToList();

                if (!posts.Any())
                {
                    _notification.Warning($"No posts found with tag: {tag}");
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.FilterType = "tag";
                ViewBag.FilterValue = tag;
                return View("TaggedPosts", posts);
            }

            // If no tag provided, redirect to home
            return RedirectToAction("Index", "Home");
        }
    }
}