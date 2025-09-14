using AspNetCoreHero.ToastNotification.Abstractions;
using FineBlog.Data;
using FineBlog.Models;
using FineBlog.Utilites;
using FineBlog.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList;

namespace FineBlog.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class PostController : Controller
    {
        private readonly ApplicationDbContext _context;
        public INotyfService _notification { get; }
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostController(ApplicationDbContext context,
                                INotyfService notyfService,
                                IWebHostEnvironment webHostEnvironment,
                                UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _notification = notyfService;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? page)
        {
            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
            var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);

            var query = _context.Posts!
                .Include(x => x.ApplicationUser)
                .Include(x => x.PostTags) // ✅ Use PostTags instead of Tags
                    .ThenInclude(pt => pt.Tag) // ✅ Include related Tag
                .AsQueryable();

            if (!loggedInUserRole.Contains(WebsiteRoles.WebsiteAdmin))
            {
                query = query.Where(x => x.ApplicationUserId == loggedInUser!.Id);
            }

            var listOfPostsVM = await query
                .Select(x => new PostVM()
                {
                    Id = x.Id,
                    Title = x.Title,
                    CreatedDate = x.CreatedDate,
                    ThumbnailUrl = x.ThumbnailUrl,
                    AuthorName = x.ApplicationUser!.FirstName + " " + x.ApplicationUser.LastName,
                    Tags = x.PostTags.Select(pt => pt.Tag.Name).ToList() // ✅ Fetch tags properly
                })
                .OrderByDescending(x => x.CreatedDate)
                .ToPagedListAsync(page ?? 1, 5);

            return View(listOfPostsVM);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreatePostVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePostVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);

            var post = new Post()
            {
                Title = vm.Title,
                Description = vm.Description,
                ShortDescription = vm.ShortDescription,
                ApplicationUserId = loggedInUser!.Id,
                Slug = vm.Title?.Trim().Replace(" ", "-") + "-" + Guid.NewGuid()
            };

            if (vm.Thumbnail != null)
            {
                post.ThumbnailUrl = UploadImage(vm.Thumbnail);
            }

            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();

            // ✅ Handle tags
            if (!string.IsNullOrEmpty(vm.TagInput))
            {
                var tagNames = vm.TagInput.Split(',').Select(t => t.Trim()).Distinct();
                foreach (var tagName in tagNames)
                {
                    var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                    if (tag == null)
                    {
                        tag = new Tag { Name = tagName };
                        _context.Tags.Add(tag);
                        await _context.SaveChangesAsync();
                    }
                    _context.PostTags.Add(new PostTag { PostId = post.Id, TagId = tag.Id });
                }
                await _context.SaveChangesAsync();
            }

            _notification.Success("Post Created Successfully");
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts
                .Include(p => p.PostTags) // ✅ Include PostTags for cleanup
                .FirstOrDefaultAsync(x => x.Id == id);

            if (post == null)
            {
                _notification.Error("Post not found");
                return RedirectToAction("Index");
            }

            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
            var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);

            if (loggedInUserRole.Contains(WebsiteRoles.WebsiteAdmin) || loggedInUser?.Id == post.ApplicationUserId)
            {
                _context.PostTags.RemoveRange(post.PostTags); // ✅ Remove post-tags relation
                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();
                _notification.Success("Post Deleted Successfully");
            }
            else
            {
                _notification.Error("You are not authorized to delete this post");
            }

            return RedirectToAction("Index", "Post", new { area = "Admin" });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.Posts!
                .Include(p => p.PostTags) // ✅ Include PostTags
                    .ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (post == null)
            {
                _notification.Error("Post not found");
                return RedirectToAction("Index");
            }

            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
            var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);

            if (!loggedInUserRole.Contains(WebsiteRoles.WebsiteAdmin) && loggedInUser!.Id != post.ApplicationUserId)
            {
                _notification.Error("You are not authorized");
                return RedirectToAction("Index");
            }

            var vm = new CreatePostVM()
            {
                Id = post.Id,
                Title = post.Title,
                ShortDescription = post.ShortDescription,
                Description = post.Description,
                ThumbnailUrl = post.ThumbnailUrl,
                TagInput = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name)) // ✅ Pre-fill tags
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreatePostVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var post = await _context.Posts!
                .Include(p => p.PostTags) // ✅ Include PostTags for update
                .FirstOrDefaultAsync(x => x.Id == vm.Id);

            if (post == null)
            {
                _notification.Error("Post not found");
                return View(vm);
            }

            post.Title = vm.Title;
            post.ShortDescription = vm.ShortDescription;
            post.Description = vm.Description;

            if (vm.Thumbnail != null)
            {
                post.ThumbnailUrl = UploadImage(vm.Thumbnail);
            }

            // ✅ Update tags
            _context.PostTags.RemoveRange(post.PostTags); // Clear existing tags
            if (!string.IsNullOrEmpty(vm.TagInput))
            {
                var tagNames = vm.TagInput.Split(',').Select(t => t.Trim()).Distinct();
                foreach (var tagName in tagNames)
                {
                    var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                    if (tag == null)
                    {
                        tag = new Tag { Name = tagName };
                        _context.Tags.Add(tag);
                        await _context.SaveChangesAsync();
                    }
                    _context.PostTags.Add(new PostTag { PostId = post.Id, TagId = tag.Id });
                }
            }

            await _context.SaveChangesAsync();
            _notification.Success("Post updated successfully");
            return RedirectToAction("Index");
        }

        private string UploadImage(IFormFile file)
        {
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "thumbnails", uniqueFileName);
            using (FileStream fileStream = System.IO.File.Create(filePath))
            {
                file.CopyTo(fileStream);
            }
            return uniqueFileName;
        }
    }
}
