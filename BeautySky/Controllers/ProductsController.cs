using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautySky.Models;
using Amazon.S3.Model;
using Amazon.S3;
using Newtonsoft.Json;
using BeautySky.DTO;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ProjectSwpContext _context;
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName = "beautysky";

        public ProductsController(ProjectSwpContext context, IAmazonS3 amazonS3)
        {
            _context = context;
            _amazonS3 = amazonS3;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProducts(
            int? id = null,
            string? sortBy = null,
            string? order = null,
            string? name = null,
            string? categoryName = null,
            string? skinTypeName = null)
        {
            IQueryable<Product> products = _context.Products.Include(p => p.ProductsImages).Include(p => p.Reviews).Include(p => p.Category).Include(p => p.SkinType);
            var expiredProducts = await _context.Products.Where(p => p.Expire <= DateTime.Now && p.IsActive).ToListAsync();
            if (expiredProducts.Any())
            {
                foreach (var product in expiredProducts)
                {
                    product.IsActive = false; // Set IsActive = false nếu sản phẩm đã hết hạn
                }
                await _context.SaveChangesAsync();
            }
            if (id.HasValue)
            {
                var product = await products.FirstOrDefaultAsync(p => p.ProductId == id);
                if (product == null)
                {
                    return NotFound("Product not found.");
                }

                var rating = product.Reviews.Any() ? product.Reviews.Average(r => r.Rating) : (double?)null;

                return Ok(new
                {
                    product.ProductId,
                    product.ProductName,
                    product.Price,
                    product.Description,
                    product.Quantity,
                    product.CategoryId,
                    product.Ingredient,
                    CategoryName = product.Category?.CategoryName,
                    product.SkinTypeId,
                    SkinTypeName = product.SkinType?.SkinTypeName,
                    Rating = rating,
                    product.Expire,
                    productsImages = product.ProductsImages
                });

            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                products = products.Where(p => p.ProductName.Contains(name));
                if (!await products.AnyAsync())
                {
                    return NotFound("No products found matching the search criteria.");
                }
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                products = products.Where(p => p.Category != null && p.Category.CategoryName.Contains(categoryName));
            }

            if (!string.IsNullOrWhiteSpace(skinTypeName))
            {
                products = products.Where(p => p.SkinType != null && p.SkinType.SkinTypeName.Contains(skinTypeName));
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                switch (sortBy.ToLower())
                {
                    case "productname":
                        products = (order?.ToLower() == "desc") ? products.OrderByDescending(p => p.ProductName) : products.OrderBy(p => p.ProductName);
                        break;
                    case "price":
                        products = (order?.ToLower() == "desc") ? products.OrderByDescending(p => p.Price) : products.OrderBy(p => p.Price);
                        break;
                    default:
                        return BadRequest("Invalid sortBy parameter. Use 'ProductName' or 'Price'.");
                }
            }

            var productList = await products.Select(p => new
            {
                p.ProductId,
                p.ProductName,
                p.Price,
                p.Description,
                p.Ingredient,
                p.Quantity,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.CategoryName : null,
                p.SkinTypeId,
                SkinTypeName = p.SkinType != null ? p.SkinType.SkinTypeName : null,
                Rating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : (double?)null,
                p.Expire,
                p.IsActive,
                productsImages = p.ProductsImages              
            }).ToListAsync();

            return Ok(productList);
        }


        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<Product>> PostProduct([FromForm] ProductDTO ProductDTO)
        {
            if (ProductDTO.Expire <= DateTime.Now)
            {
                return BadRequest("Do not expire the product before the current time.");
            }
            var isDuplicate = await _context.Products.AnyAsync(p => p.ProductName == ProductDTO.ProductName);
            if (isDuplicate)
            {
                return BadRequest("Product name already exists.");
            }

            if (ProductDTO.Price == null || ProductDTO.Quantity == null || ProductDTO.CategoryId == null || ProductDTO.SkinTypeId == null)
            {
                return BadRequest("Price, and Quantity, CategoryId, SkinTypeId are required.");
            }

            if (!ModelState.IsValid || ProductDTO.Price < 0 || ProductDTO.Quantity < 0)
            {
                if (ProductDTO.Price < 0)
                {
                    ModelState.AddModelError("Price", "Price cannot be negative");
                }
                if (ProductDTO.Quantity < 0)
                {
                    ModelState.AddModelError("Quantity", "Quantity cannot be negative");
                }
                return BadRequest(ModelState);
            }

            try
            {
                var product = new Product
                {
                    ProductName = ProductDTO.ProductName,
                    Price = ProductDTO.Price ?? 0,
                    Quantity = ProductDTO.Quantity ?? 0,
                    Description = ProductDTO.Description,
                    Ingredient = ProductDTO.Ingredient,
                    CategoryId = ProductDTO.CategoryId,
                    SkinTypeId = ProductDTO.SkinTypeId,
                    Expire = ProductDTO.Expire,
                    IsActive = ProductDTO.IsActive == true

                };

                if (ProductDTO.File != null && ProductDTO.File.Length > 0)
                {
                    string keyName = $"products/{Guid.NewGuid()}_{ProductDTO.File.FileName}";
                    using (var stream = ProductDTO.File.OpenReadStream())
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyName,
                            InputStream = stream,
                            ContentType = ProductDTO.File.ContentType
                        };
                        await _amazonS3.PutObjectAsync(putRequest);
                    }

                    string fileUrl = $"https://beautysky.s3.amazonaws.com/{keyName}";
                    product.ProductsImages = new List<ProductsImage>
            {
                new ProductsImage { ImageUrl = fileUrl, ImageDescription = ProductDTO.ImageDescription }
            };
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                return Ok("Add product success");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating product: {ex}");
                return StatusCode(500, "An error occurred while creating the product.");
            }
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutProduct(int id, [FromForm] ProductDTO ProductDTO)
        {
            //var isDuplicate = await _context.Products.AnyAsync(p => p.ProductName == ProductDTO.ProductName);
            //if (isDuplicate)
            //{
            //    return BadRequest("Product name already exists.");
            //}
            if (ProductDTO.Expire <= DateTime.Now)
            {
                return BadRequest("Do not expire the product before the current time.");
            }
            else
            {
                ProductDTO.IsActive = true;
            }
            if (!ModelState.IsValid || ProductDTO.Price < 0 || ProductDTO.Quantity < 0)
            {
                if (ProductDTO.Price < 0)
                {
                    ModelState.AddModelError("Price", "Price cannot be negative");
                }
                if (ProductDTO.Quantity < 0)
                {
                    ModelState.AddModelError("Quantity", "Quantity cannot be negative");
                }
                return BadRequest(ModelState);
            }

            try
            {
                var product = await _context.Products.Include(p => p.ProductsImages).FirstOrDefaultAsync(p => p.ProductId == id);
                if (product == null)
                {
                    return NotFound("Product not found.");
                }

                product.ProductName = ProductDTO.ProductName ?? product.ProductName;
                product.Price = ProductDTO.Price ?? product.Price;
                product.Quantity = ProductDTO.Quantity ?? product.Quantity;
                product.Description = ProductDTO.Description ?? product.Description;
                product.Ingredient = ProductDTO.Ingredient ?? product.Ingredient;
                product.CategoryId = ProductDTO.CategoryId ?? product.CategoryId;
                product.SkinTypeId = ProductDTO.SkinTypeId ?? product.SkinTypeId;
                product.Expire = ProductDTO.Expire ?? product.Expire;
                product.IsActive = ProductDTO.IsActive ?? product.IsActive;



                if (ProductDTO.File != null && ProductDTO.File.Length > 0)
                {
                    var existingImage = product.ProductsImages.FirstOrDefault();
                    if (existingImage != null)
                    {
                        // Xóa ảnh cũ trên S3
                        var deleteRequest = new DeleteObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = existingImage.ImageUrl.Replace("https://beautysky.s3.amazonaws.com/", "")
                        };
                        await _amazonS3.DeleteObjectAsync(deleteRequest);

                        string keyName = $"products/{Guid.NewGuid()}_{ProductDTO.File.FileName}";
                        using (var stream = ProductDTO.File.OpenReadStream())
                        {
                            var putRequest = new PutObjectRequest
                            {
                                BucketName = _bucketName,
                                Key = keyName,
                                InputStream = stream,
                                ContentType = ProductDTO.File.ContentType
                            };
                            await _amazonS3.PutObjectAsync(putRequest);
                        }

                        string fileUrl = $"https://beautysky.s3.amazonaws.com/{keyName}";
                        existingImage.ImageUrl = fileUrl;
                        existingImage.ImageDescription = ProductDTO.ImageDescription ?? existingImage.ImageDescription;
                    }
                    else
                    {
                        string keyName = $"products/{Guid.NewGuid()}_{ProductDTO.File.FileName}";
                        using (var stream = ProductDTO.File.OpenReadStream())
                        {
                            var putRequest = new PutObjectRequest
                            {
                                BucketName = _bucketName,
                                Key = keyName,
                                InputStream = stream,
                                ContentType = ProductDTO.File.ContentType
                            };
                            await _amazonS3.PutObjectAsync(putRequest);
                        }

                        string fileUrl = $"https://beautysky.s3.amazonaws.com/{keyName}";
                        product.ProductsImages.Add(new ProductsImage
                        {
                            ImageUrl = fileUrl,
                            ImageDescription = ProductDTO.ImageDescription
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok("Update Sucess");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating product: {ex}");
                return StatusCode(500, "An error occurred while updating the product.");
            }
        }

        [HttpPut("reactivate/{id}")]
        public async Task<IActionResult> ReactivateProduct(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return NotFound("Product not found.");
                }

                // Cập nhật trạng thái IsActive
                product.IsActive = true;

                await _context.SaveChangesAsync();
                return Ok("Product reactivated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reactivating product: {ex}");
                return StatusCode(500, "An error occurred while reactivating the product.");
            }
        }



        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.Include(p => p.ProductsImages).FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                return NotFound("Product not found");
            }

            //foreach (var image in product.ProductsImages)
            //{
            //    var deleteRequest = new DeleteObjectRequest { BucketName = _bucketName, Key = $"products/{Path.GetFileName(image.ImageUrl)}" };
            //    await _amazonS3.DeleteObjectAsync(deleteRequest);
            //    _context.ProductsImages.Remove(image);
            //}
            product.IsActive = false;
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return Ok("Deleted successfully");
        }
    }
}
