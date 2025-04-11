using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautySky.Models;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using Microsoft.AspNetCore.Authorization;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromotionsController : ControllerBase
    {
        private readonly ProjectSwpContext _context;

        public PromotionsController(ProjectSwpContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetPromotions()
        {
            var promotions = await _context.Promotions.ToListAsync();

            bool changesMade = false; // Kiểm tra xem có thay đổi nào không
            DateTime currentTime = DateTime.Now; // Lấy thời gian theo local

            foreach (var promo in promotions)
            {
                if (promo.StartDate > currentTime || promo.EndDate < currentTime)
                {
                    // Nếu chưa đến hạn hoặc đã hết hạn thì tắt IsActive
                    if (promo.IsActive)
                    {
                        promo.IsActive = false;
                        changesMade = true;
                    }
                }
                else if (promo.StartDate <= currentTime && promo.EndDate >= currentTime)
                {
                    // Nếu khuyến mãi đang trong thời gian hiệu lực thì bật IsActive
                    if (!promo.IsActive)
                    {
                        promo.IsActive = true;
                        changesMade = true;
                    }
                }
            }

            if (changesMade)
            {
                await _context.SaveChangesAsync(); // Chỉ lưu nếu có thay đổi
            }


            // Trả về tất cả khuyến mãi đang hoạt động
            return Ok(promotions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Promotion>> GetPromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }
            return promotion;
        }

        [HttpPost]
        public async Task<ActionResult<Promotion>> CreatePromotion(Promotion promotion)
        {
            if (promotion.DiscountPercentage < 0)
            {
                return BadRequest("DiscountPercentage can not be negative");
            }
            if (promotion.Quantity < 0)
            {
                return BadRequest("DiscountPercentage can not be negative");
            }
            if (promotion.StartDate < DateTime.Now)
            {
                return BadRequest("StartDate can not be before current date");
            }
            if (promotion.EndDate < promotion.StartDate)
            {
                return BadRequest("EndDate cannot be before StartDate");
            }
            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();
            return Ok("Add promotion success");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePromotion(int id, [FromBody] Promotion updatedPromotion)
        {
            var existingPromotion = await _context.Promotions.FindAsync(id);
            if (existingPromotion == null)
            {
                return NotFound("Promotion not found");
            }
            if (updatedPromotion.DiscountPercentage < 0)
            {
                return BadRequest("DiscountPercentage can not be negative");
            }
            if (updatedPromotion.Quantity < 0)
            {
                return BadRequest("Quantity can not be negative");
            }
            if (updatedPromotion.StartDate < DateTime.Now)
            {
                return BadRequest("StartDate can not be before current date");
            }

            if (!string.IsNullOrEmpty(updatedPromotion.PromotionName))
                existingPromotion.PromotionName = updatedPromotion.PromotionName;

            if (updatedPromotion.DiscountPercentage > 0)
                existingPromotion.DiscountPercentage = updatedPromotion.DiscountPercentage;

            if (updatedPromotion.Quantity > 0)
                existingPromotion.Quantity = updatedPromotion.Quantity;

            if (updatedPromotion.StartDate >= DateTime.Now)
                existingPromotion.StartDate = updatedPromotion.StartDate;

            if (updatedPromotion.EndDate >= DateTime.Now || updatedPromotion.EndDate < DateTime.Now)
                 existingPromotion.EndDate = updatedPromotion.EndDate;

            if (updatedPromotion.IsActive != null)
                existingPromotion.IsActive = updatedPromotion.IsActive;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "Concurrency error occurred while updating the Question.");
            }

            return Ok("Update Successful");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound("Promotion not found");
            }

            promotion.IsActive = false;
            _context.Promotions.Update(promotion);
            await _context.SaveChangesAsync();
            return Ok("Deleted successfully");
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.PromotionId == id);
        }


        [HttpGet("myPromotions")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetMyPromotions()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("Invalid token or missing userId claim.");
            }
            
            var userId = int.Parse(userIdClaim);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Lấy danh sách các khuyến mãi còn hạn và thỏa mãn điều kiện điểm người dùng
            var availablePromotions = await _context.Promotions
                .Where(p => p.IsActive == true && p.EndDate >= DateTime.Now && p.StartDate <= DateTime.Now) // Khuyến mãi còn hạn và có IsActive là true
                .Where(p => p.DiscountPercentage <= user.Point) // Khuyến mãi có tỷ lệ giảm = số điểm người dùng có
                .Select(p => new
                {
                    p.PromotionId,
                    p.PromotionName,
                    p.DiscountPercentage,
                    p.StartDate,
                    p.EndDate,
                    p.Quantity
                })
                .ToListAsync();

            if (availablePromotions.Count == 0)
            {
                return NotFound("No promotions can be used");
            }
            var respone = new
            {
                UserPoint = user.Point,
                Promotion = availablePromotions
            };
            return Ok(respone);
        }
    }
}
