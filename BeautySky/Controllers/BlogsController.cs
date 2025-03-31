using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautySky.Models;
using Amazon.S3;
using Amazon.S3.Model;
using BeautySky.DTO;


namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogsController : ControllerBase
    {
        private readonly ProjectSwpContext _context;
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName = "beautysky";

        public BlogsController(ProjectSwpContext context, IAmazonS3 amazonS3)
        {
            _context = context;
            _amazonS3 = amazonS3;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Blog>>> GetBlogs()
        {
            return await _context.Blogs.Where(b => b.IsActive).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Blog>> GetBlog(int id)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();
            return blog;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<object>> PostBlog([FromForm] BlogDTO blogDTO)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var blog = new Blog
                {
                    Title = blogDTO.Title,
                    Content = blogDTO.Content,
                    AuthorId = blogDTO.AuthorId,
                    Status = blogDTO.Status,
                    SkinType = blogDTO.SkinType,
                    Category = blogDTO.Category,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                    IsActive = blogDTO.IsActive == true
                };

                if (blogDTO.File != null && blogDTO.File.Length > 0)
                {
                    string keyName = $"blogs/{Guid.NewGuid()}_{blogDTO.File.FileName}";
                    using (var stream = blogDTO.File.OpenReadStream())
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyName,
                            InputStream = stream,
                            ContentType = blogDTO.File.ContentType
                        };
                        await _amazonS3.PutObjectAsync(putRequest);
                    }
                    blog.ImgUrl = $"https://{_bucketName}.s3.amazonaws.com/{keyName}";
                }

                _context.Blogs.Add(blog);
                await _context.SaveChangesAsync();

                var response = new
                {
                    Title = blog.Title,
                    Content = blog.Content,
                    AuthorId = blog.AuthorId,
                    SkinType = blog.SkinType,
                    Category = blog.Category,
                    ImgURL = blog.ImgUrl
                };

                return CreatedAtAction("GetBlog", new { id = blog.BlogId }, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating blog: {ex}");
                return StatusCode(500, "An error occurred while creating the blog.");
            }
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutBlog(int id, [FromForm] BlogDTO blogDTO)
        {
            try
            {
                var blog = await _context.Blogs.FindAsync(id);
                if (blog == null) return NotFound();

                // Cập nhật thông tin blog
                blog.Title = blogDTO.Title;
                blog.Content = blogDTO.Content;
                blog.AuthorId = blogDTO.AuthorId;
                blog.Status = blogDTO.Status;
                blog.SkinType = blogDTO.SkinType;
                blog.Category = blogDTO.Category;
                blog.UpdatedDate = DateTime.UtcNow;
                blog.IsActive = blogDTO.IsActive ?? blog.IsActive;

                // Xử lý upload file mới nếu có
                if (blogDTO.File != null && blogDTO.File.Length > 0)
                {
                    string keyName = $"blogs/{Guid.NewGuid()}_{blogDTO.File.FileName}";
                    using (var stream = blogDTO.File.OpenReadStream())
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyName,
                            InputStream = stream,
                            ContentType = blogDTO.File.ContentType
                        };
                        await _amazonS3.PutObjectAsync(putRequest);
                    }
                    blog.ImgUrl = $"https://{_bucketName}.s3.amazonaws.com/{keyName}";
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Blogs.Any(e => e.BlogId == id))
                    return NotFound();
                else
                    throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating blog: {ex}");
                return StatusCode(500, "An error occurred while updating the blog.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();

            blog.IsActive = false;
            _context.Blogs.Update(blog);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        [HttpGet("byCategory/{category}")]
        public async Task<ActionResult<IEnumerable<Blog>>> GetBlogsByCategory(string category)
        {
            var blogs = await _context.Blogs.Where(b => b.Category == category).ToListAsync();
            if (!blogs.Any()) return NotFound();
            return blogs;
        }

        [HttpGet("bySkinType/{skinType}")]
        public async Task<ActionResult<IEnumerable<Blog>>> GetBlogsBySkinType(string skinType)
        {
            var blogs = await _context.Blogs.Where(b => b.SkinType == skinType).ToListAsync();
            if (!blogs.Any()) return NotFound();
            return blogs;
        }
        [HttpGet("sreach-title")]
        public IActionResult GetBlogs(string? title)
        {
            var blogs = string.IsNullOrEmpty(title)
                ? _context.Blogs.ToList()
                : _context.Blogs.Where(b => b.Title.Contains(title)).ToList();

            return Ok(blogs);
        }

    }
}
