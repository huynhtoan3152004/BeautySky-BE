﻿using System;
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
            return await _context.Blogs.ToListAsync();
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
                    UpdatedDate = DateTime.UtcNow
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
                    blog.ImgURL = $"https://{_bucketName}.s3.amazonaws.com/{keyName}";
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
                    ImgURL = blog.ImgURL
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
        public async Task<IActionResult> PutBlog(int id, Blog blog)
        {
            if (id != blog.BlogId) return BadRequest();

            _context.Entry(blog).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Blogs.Any(e => e.BlogId == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();

            _context.Blogs.Remove(blog);
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

    }
}
