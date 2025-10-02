using AspNetCoreHero.ToastNotification.Abstractions;
using FineBlog.Data;
using FineBlog.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineBlog.Controllers
{
    public class CommentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotyfService _notification;

        public CommentController(ApplicationDbContext context, INotyfService notification)
        {
            _context = context;
            _notification = notification;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
     [FromForm] string AuthorName,
     [FromForm] string Content,
     [FromForm] int PostId)
        {
            try
            {
                var comment = new Comment
                {
                    AuthorName = AuthorName,
                    Content = Content,
                    PostId = PostId,
                    CreatedAt = DateTime.Now
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                _notification.Success("Comment saved!");
                return RedirectToPost(PostId);
            }
            catch (Exception ex)
            {
                _notification.Error($"Error: {ex.Message}");
                return RedirectToPost(PostId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int CommentId)
        {
            var comment = await _context.Comments.FindAsync(CommentId);
            if (comment == null)
            {
                _notification.Error("Comment not found!");
                return RedirectToPost(comment.PostId);
            }

            // Check if the logged-in user is the comment's author or an admin
            if (User.Identity.Name != comment.AuthorName && !User.IsInRole("Admin"))
            {
                _notification.Error("You are not authorized to delete this comment.");
                return RedirectToPost(comment.PostId);
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            _notification.Success("Comment deleted successfully!");
            return RedirectToPost(comment.PostId);
        }

        private IActionResult RedirectToPost(int postId)
        {
            // Get the post slug for redirection
            var post = _context.Posts
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == postId);

            if (post == null)
            {
                _notification.Error("Post not found");
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Post", "Blog", new { slug = post.Slug });
        }
    }
}