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
    public class NewsController : ControllerBase
    {
        private readonly ProjectSwpContext _context;
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName = "beautysky";

        public NewsController(ProjectSwpContext context, IAmazonS3 amazonS3)
        {
            _context = context;
            _amazonS3 = amazonS3;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<News>>> GetNews()
        {
            return await _context.News
                .Where(n => n.IsActive == true)  // Thêm điều kiện lọc IsActive
                .OrderByDescending(n => n.CreateDate)  // Sắp xếp theo ngày tạo mới nhất
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<News>> GetNews(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound("News not found.");
            return news;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<News>> PostNews([FromForm] NewsDTO newsDTO)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var news = new News
                {
                    Title = newsDTO.Title,
                    Content = newsDTO.Content,
                    CreateDate = newsDTO.CreateDate ?? DateTime.Now,
                    StartDate = newsDTO.StartDate,
                    EndDate = newsDTO.EndDate,
                    IsActive = newsDTO.IsActive == true
                };

                if (newsDTO.File != null && newsDTO.File.Length > 0)
                {
                    string keyName = $"news/{Guid.NewGuid()}_{newsDTO.File.FileName}";
                    using (var stream = newsDTO.File.OpenReadStream())
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyName,
                            InputStream = stream,
                            ContentType = newsDTO.File.ContentType
                        };
                        await _amazonS3.PutObjectAsync(putRequest);
                    }
                    news.ImageUrl = $"https://{_bucketName}.s3.amazonaws.com/{keyName}";
                }

                _context.News.Add(news);
                await _context.SaveChangesAsync();
                return Ok("News added successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating news: {ex}");
                return StatusCode(500, "An error occurred while creating the news.");
            }
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutNews(int id, [FromForm] NewsDTO newsDTO)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound("News not found.");

            try
            {
                news.Title = newsDTO.Title ?? news.Title;
                news.Content = newsDTO.Content ?? news.Content;
                news.StartDate = newsDTO.StartDate ?? news.StartDate;
                news.EndDate = newsDTO.EndDate ?? news.EndDate;
                news.IsActive = newsDTO.IsActive ?? news.IsActive;

                if (newsDTO.File != null && newsDTO.File.Length > 0)
                {
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = news.ImageUrl.Replace($"https://{_bucketName}.s3.amazonaws.com/", "")
                    };
                    await _amazonS3.DeleteObjectAsync(deleteRequest);

                    string keyName = $"news/{Guid.NewGuid()}_{newsDTO.File.FileName}";
                    using (var stream = newsDTO.File.OpenReadStream())
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyName,
                            InputStream = stream,
                            ContentType = newsDTO.File.ContentType
                        };
                        await _amazonS3.PutObjectAsync(putRequest);
                    }
                    news.ImageUrl = $"https://{_bucketName}.s3.amazonaws.com/{keyName}";
                }

                await _context.SaveChangesAsync();
                return Ok("News updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating news: {ex}");
                return StatusCode(500, "An error occurred while updating the news.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNews(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound("News not found.");

            //if (!string.IsNullOrEmpty(news.ImageUrl))
            //{
            //    var deleteRequest = new DeleteObjectRequest
            //    {
            //        BucketName = _bucketName,
            //        Key = news.ImageUrl.Replace($"https://{_bucketName}.s3.amazonaws.com/", "")
            //    };
            //    await _amazonS3.DeleteObjectAsync(deleteRequest);
            //}

            news.IsActive = false;
            _context.News.Update(news);
            await _context.SaveChangesAsync();
            return Ok("News deleted successfully");


           
        }
    }
}


