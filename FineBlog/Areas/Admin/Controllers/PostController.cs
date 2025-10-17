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
        private readonly ILogger<PostController> _logger; // ✅ ADD THIS

        public PostController(ApplicationDbContext context,
                                INotyfService notyfService,
                                IWebHostEnvironment webHostEnvironment,
                                UserManager<ApplicationUser> userManager,
                                ILogger<PostController> logger)
        {
            _context = context;
            _notification = notyfService;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? page)
        {
            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
            var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);

            var query = _context.Posts!
                .Include(x => x.ApplicationUser)
                .Include(x => x.PostTags)
                    .ThenInclude(pt => pt.Tag)
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
                    Tags = x.PostTags.Select(pt => pt.Tag.Name).ToList()
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

        // ✅ MOVE THIS METHOD BEFORE THE CREATE METHOD
        private async Task ProcessTagsInBatch(int postId, string tagInput)
        {
            var tagNames = tagInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim())
                                  .Where(t => !string.IsNullOrEmpty(t))
                                  .Distinct()
                                  .ToList();

            if (!tagNames.Any()) return;

            // Get existing tags in one query
            var existingTags = await _context.Tags
                .Where(t => tagNames.Contains(t.Name))
                .ToListAsync();

            var existingTagNames = existingTags.Select(t => t.Name).ToHashSet();
            var newTagNames = tagNames.Where(t => !existingTagNames.Contains(t)).ToList();

            // Add new tags if any
            if (newTagNames.Any())
            {
                var newTags = newTagNames.Select(name => new Tag { Name = name }).ToList();
                await _context.Tags.AddRangeAsync(newTags);
                await _context.SaveChangesAsync(); // Get IDs for new tags first
                existingTags.AddRange(newTags); // Now newTags have proper IDs
            }

            var postTags = existingTags.Select(tag => new PostTag
            {
                PostId = postId,
                TagId = tag.Id  // ✅ All tags now have valid IDs
            }).ToList();

            await _context.PostTags.AddRangeAsync(postTags);
            await _context.SaveChangesAsync();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePostVM vm)
        {
            // ✅ ADD LOGGING AT THE VERY START
            System.Diagnostics.Debug.WriteLine("🔥🔥🔥 CREATE POST STARTED 🔥🔥🔥");
            _logger.LogInformation("📝 CREATE POST METHOD CALLED - Title: '{Title}'", vm.Title);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("❌ MODEL STATE INVALID");
                _notification.Error("Please fix validation errors");
                return View(vm);
            }

            try
            {
                var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
                _logger.LogInformation("👤 USER FOUND: {UserId}", loggedInUser?.Id);

                // ✅ STEP 1: Handle file upload FIRST (OUTSIDE transaction)
                string? thumbnailUrl = null;
                if (vm.Thumbnail != null)
                {
                    _logger.LogInformation("🖼️ THUMBNAIL UPLOAD STARTED - Size: {Size} bytes", vm.Thumbnail.Length);
                    thumbnailUrl = await SafeUploadThumbnailAsync(vm.Thumbnail);
                    _logger.LogInformation("🖼️ THUMBNAIL UPLOAD COMPLETED - URL: {Url}", thumbnailUrl);
                }

                // ✅ STEP 2: Single database operation
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _logger.LogInformation("💾 DATABASE TRANSACTION STARTED");

                    var post = new Post()
                    {
                        Title = vm.Title,
                        Description = vm.Description,
                        ShortDescription = vm.ShortDescription,
                        ApplicationUserId = loggedInUser!.Id,
                        Slug = vm.Title?.Trim().Replace(" ", "-") + "-" + Guid.NewGuid(),
                        ThumbnailUrl = thumbnailUrl
                    };

                    await _context.Posts.AddAsync(post);
                    _logger.LogInformation("➕ POST ADDED TO CONTEXT");

                    await _context.SaveChangesAsync(); // Single save to get Post ID
                    _logger.LogInformation("💾 POST SAVED TO DATABASE - ID: {PostId}", post.Id);

                    // Handle tags in a single batch operation
                    if (!string.IsNullOrEmpty(vm.TagInput))
                    {
                        _logger.LogInformation("🏷️ PROCESSING TAGS: {Tags}", vm.TagInput);
                        await ProcessTagsInBatch(post.Id, vm.TagInput);
                        _logger.LogInformation("🏷️ TAGS PROCESSED SUCCESSFULLY");
                    }

                    await transaction.CommitAsync();
                    _logger.LogInformation("✅ TRANSACTION COMMITTED SUCCESSFULLY");

                    System.Diagnostics.Debug.WriteLine("✅✅✅ POST CREATION SUCCESSFUL ✅✅✅");
                    _notification.Success("Post Created Successfully");

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "❌ DATABASE OPERATION FAILED");

                    // Clean up uploaded file if database operation failed
                    if (!string.IsNullOrEmpty(thumbnailUrl))
                    {
                        SafeDeleteThumbnail(thumbnailUrl);
                    }

                    _notification.Error($"Error creating post: {ex.Message}");
                    return View(vm);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 UNEXPECTED ERROR IN CREATE METHOD");
                _notification.Error($"Unexpected error: {ex.Message}");
                return View(vm);
            }
        }
        
        [HttpPost]
public async Task<IActionResult> Delete(int id)
{
    try
    {
        var post = await _context.Posts
            .Include(p => p.PostTags)
            .Include(p => p.Comments)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (post == null)
        {
            _notification.Error("Post not found");
            return RedirectToAction("Index");
        }

        var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
        var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);

        if (!loggedInUserRole.Contains(WebsiteRoles.WebsiteAdmin) && loggedInUser?.Id != post.ApplicationUserId)
        {
            _notification.Error("You are not authorized to delete this post");
            return RedirectToAction("Index");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // ✅ STEP 1: Extract and delete content images from post description
            if (!string.IsNullOrEmpty(post.Description))
            {
                await DeleteContentImagesFromPost(post.Description);
            }

            // ✅ STEP 2: Clean up thumbnail file
            if (!string.IsNullOrEmpty(post.ThumbnailUrl))
            {
                SafeDeleteThumbnail(post.ThumbnailUrl);
            }

            // ✅ STEP 3: Remove database entities
            _context.PostTags.RemoveRange(post.PostTags);
            _context.Comments.RemoveRange(post.Comments);
            _context.Posts.Remove(post);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _notification.Success("Post and all associated images deleted successfully");
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _notification.Error($"Error deleting post: {ex.Message}");
            return RedirectToAction("Index");
        }
    }
    catch (Exception ex)
    {
        _notification.Error($"Unexpected error: {ex.Message}");
        return RedirectToAction("Index");
    }
}

// ✅ NEW METHOD: Extract and delete content images from post HTML
private async Task DeleteContentImagesFromPost(string postDescription)
{
    try
    {
        // Pattern to match content image URLs: /content-images/{filename}
        var pattern = @"/content-images/([a-zA-Z0-9\-_\.]+)";
        var matches = System.Text.RegularExpressions.Regex.Matches(postDescription, pattern);
        
        var deletedFiles = new List<string>();
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var fileName = match.Groups[1].Value;
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "content-images", fileName);
                
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    deletedFiles.Add(fileName);
                }
            }
        }
        
        if (deletedFiles.Any())
        {
            _logger.LogInformation($"Deleted {deletedFiles.Count} content images: {string.Join(", ", deletedFiles)}");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting content images from post");
        // Don't throw - we don't want image deletion failure to prevent post deletion
    }
}

        [HttpGet]
        public async Task<IActionResult> Edit(int id)  // GET - displays the form
        {
            var post = await _context.Posts!
                .Include(p => p.PostTags)
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
                TagInput = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name))
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreatePostVM vm)
        {
            if (!ModelState.IsValid)
            {
                _notification.Error("Please fix validation errors");
                return View(vm);
            }

            try
            {
                var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);

                // ✅ STEP 1: Handle file upload FIRST (OUTSIDE transaction) - Same as Create
                string? newThumbnailUrl = null;
                string? oldThumbnailUrl = null;

                if (vm.Thumbnail != null)
                {
                    newThumbnailUrl = await SafeUploadThumbnailAsync(vm.Thumbnail);
                    if (newThumbnailUrl == null)
                    {
                        _notification.Warning("New thumbnail upload failed, keeping existing thumbnail");
                    }
                }

                // ✅ STEP 2: Single database operation - Same pattern as Create
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var post = await _context.Posts!
                        .Include(p => p.PostTags)
                        .FirstOrDefaultAsync(x => x.Id == vm.Id);

                    if (post == null)
                    {
                        _notification.Error("Post not found");
                        return View(vm);
                    }

                    // Store old thumbnail URL for cleanup - Same as Delete cleanup logic
                    oldThumbnailUrl = post.ThumbnailUrl;

                    // Update post properties
                    post.Title = vm.Title;
                    post.ShortDescription = vm.ShortDescription;
                    post.Description = vm.Description;

                    // Use the already uploaded file
                    if (!string.IsNullOrEmpty(newThumbnailUrl))
                    {
                        post.ThumbnailUrl = newThumbnailUrl;
                    }

                    // ✅ FIXED: Update tags using the same batch method as Create
                    _context.PostTags.RemoveRange(post.PostTags);

                    if (!string.IsNullOrEmpty(vm.TagInput))
                    {
                        await ProcessTagsInBatch(post.Id, vm.TagInput);
                    }

                    await _context.SaveChangesAsync(); // Single save - Same as Create
                    await transaction.CommitAsync();

                    // ✅ Clean up old thumbnail AFTER successful database update - Same as Delete
                    if (!string.IsNullOrEmpty(newThumbnailUrl) && !string.IsNullOrEmpty(oldThumbnailUrl))
                    {
                        SafeDeleteThumbnail(oldThumbnailUrl);
                    }

                    _notification.Success("Post updated successfully");
                    return RedirectToAction("Index"); // Simple redirect - Same as Create/Delete
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // ✅ Clean up new thumbnail if database operation failed - Same as Create
                    if (!string.IsNullOrEmpty(newThumbnailUrl))
                    {
                        SafeDeleteThumbnail(newThumbnailUrl);
                    }

                    _notification.Error($"Error updating post: {ex.Message}");
                    return View(vm);
                }
            }
            catch (Exception ex)
            {
                _notification.Error($"Unexpected error: {ex.Message}");
                return View(vm);
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { error = "No file uploaded" });

            try
            {
                // Validate file size for content images
                if (file.Length > 10 * 1024 * 1024) // 10MB limit
                    return Json(new { error = "File too large. Maximum 10MB allowed." });

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "content-images");

                Directory.CreateDirectory(uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var imageUrl = $"/content-images/{uniqueFileName}";
                return Json(new { location = imageUrl });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Safe thumbnail upload with comprehensive error handling
        private async Task<string> SafeUploadThumbnailAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _notification.Error("No file selected");
                    return null;
                }

                // Validate file size (2MB limit for testing - reduce from 5MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    _notification.Error("File too large. Maximum 2MB allowed for thumbnails.");
                    return null;
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
                {
                    _notification.Error($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
                    return null;
                }

                string uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                var thumbnailsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "thumbnails");

                // Ensure directory exists
                Directory.CreateDirectory(thumbnailsFolder);

                var filePath = Path.Combine(thumbnailsFolder, uniqueFileName);

                // ✅ ASYNC FILE OPERATIONS (FIXES CONNECTION TIMEOUT)
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await file.CopyToAsync(fileStream); // ✅ Async copy
                    await fileStream.FlushAsync(); // ✅ Async flush
                }

                return uniqueFileName;
            }
            catch (Exception ex)
            {
                _notification.Error($"Thumbnail upload failed: {ex.Message}");
                return null;
            }
        }

        // Safe method to delete old thumbnails
        private void SafeDeleteThumbnail(string thumbnailUrl)
        {
            if (string.IsNullOrEmpty(thumbnailUrl)) return;

            try
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "thumbnails", thumbnailUrl);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the application
                Console.WriteLine($"Warning: Could not delete old thumbnail {thumbnailUrl}: {ex.Message}");
            }
        }

        // Safe redirect to prevent crashes
        private async Task<IActionResult> SafeRedirectToAction(string actionName)
        {
            try
            {
                // Small delay to ensure operations complete
                await Task.Delay(100);

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();

                return RedirectToAction(actionName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redirect failed: {ex.Message}");
                // Fallback redirect
                return Redirect("/Admin/Post");
            }
        }

        [HttpGet]
        public IActionResult TestLogging()
        {
            System.Diagnostics.Debug.WriteLine("🎯🎯🎯 TEST LOG FIRED 🎯🎯🎯");
            _logger.LogInformation("📢 ILOGGER TEST MESSAGE");
            Console.WriteLine("🖥️ CONSOLE WRITELINE TEST MESSAGE");

            _notification.Success("Test logging completed! Check your Debug output.");
            return RedirectToAction("Index");
        }
    }
}