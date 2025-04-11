using Azure.Core;
using BeautySky.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly ProjectSwpContext _context;

        public OrdersController(ProjectSwpContext context)
        {
            _context = context;
        }
        [HttpGet("orders/myOrders")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Order>>> GetMyOrders()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("Invalid token or missing userId claim.");
            }

            var userId = int.Parse(userIdClaim);

            var orders = await _context.Orders
                .Include(o => o.OrderProducts)
                    .ThenInclude(op => op.Product)
                .Include(o => o.User)
                .Include(o => o.Promotion)
                .Where(o => o.UserId == userId)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.DiscountAmount,
                    o.FinalAmount,
                    o.Status,
                    o.CancelledDate,
                    o.CancelledReason,
                    User = new
                    {
                        o.User.UserId,
                        o.User.FullName,
                        o.User.Phone,
                        o.User.Address
                    },
                    Promotion = o.Promotion != null ? new
                    {
                        o.Promotion.PromotionId,
                        o.Promotion.DiscountPercentage
                    } : null,
                    OrderProducts = o.OrderProducts.Select(op => new
                    {
                        op.ProductId,
                        op.Product.ProductName,
                        op.Product.Price,
                        op.Quantity,
                        op.TotalPrice
                    })
                })
                .ToListAsync();
            //var completedOrdersCount = await _context.Orders.CountAsync(o => o.UserId == userId && o.Status == "Completed");
            //var userPoints = completedOrdersCount; // Mỗi đơn hàng "Completed" = 1 điểm
            //var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            //if (user != null)
            //{
            //    user.Point = userPoints;  // Cập nhật số điểm cho người dùng
            //    _context.Users.Update(user);
            //    await _context.SaveChangesAsync();
            //}

            if (!orders.Any())
            {
                return NotFound("Không tìm thấy đơn hàng nào.");
            }

            return Ok(orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderProducts)
                .Include(o => o.User)
                .Include(o => o.Payment)
                    .ThenInclude(p => p.PaymentType) // Thêm dòng này để lấy thông tin PaymentType
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.DiscountAmount,
                    o.FinalAmount,
                    o.Status,
                    o.PaymentId,
                    o.CancelledDate,
                    o.CancelledReason,
                    Payment = o.Payment != null ? new
                    {
                        o.Payment.PaymentId,
                        o.Payment.PaymentTypeId,
                        o.Payment.PaymentStatus,
                        PaymentType = new
                        {
                            o.Payment.PaymentType.PaymentTypeId,
                            o.Payment.PaymentType.PaymentTypeName
                        }
                    } : null,
                    User = new
                    {
                        o.User.UserId,
                        o.User.FullName,
                        o.User.Phone,
                        o.User.Address
                    },
                    OrderProducts = o.OrderProducts.Select(op => new
                    {
                        op.ProductId,
                        op.Quantity
                    })
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("orders/{orderId}")]
        [Authorize]
        public async Task<ActionResult<Order>> GetOrderDetail(int orderId)
        {
            // Lấy userId từ token
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("Invalid token or missing userId claim.");
            }

            var userId = int.Parse(userIdClaim);

            // Lấy chi tiết đơn hàng kèm theo thông tin sản phẩm và người dùng
            var order = await _context.Orders
                .Include(o => o.OrderProducts)
                    .ThenInclude(op => op.Product)
                .Include(o => o.User)
                .Include(o => o.Promotion)
                .Where(o => o.OrderId == orderId && o.UserId == userId)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.DiscountAmount,
                    o.FinalAmount,
                    o.Status,
                    User = new
                    {
                        o.User.UserId,
                        o.User.FullName,
                        o.User.Phone,
                        o.User.Address
                    },
                    Promotion = o.Promotion != null ? new
                    {
                        o.Promotion.PromotionId,
                        o.Promotion.DiscountPercentage
                    } : null,
                    OrderProducts = o.OrderProducts.Select(op => new
                    {
                        op.ProductId,
                        op.Product.ProductName,
                        op.Product.Price,
                        op.Quantity,
                        op.TotalPrice
                    })
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound("Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.");
            }

            return Ok(order);
        }


        [Authorize] // Bảo vệ API, chỉ cho phép người dùng đã đăng nhập
        [HttpPost("order-products")]
        public async Task<IActionResult> CreateOrder(int? promotionID, List<OrderProductRequest> products)
        {
            var userIdClaim = HttpContext.User.FindFirst("userId");
            if (userIdClaim == null)
            {
                return Unauthorized("Không tìm thấy thông tin người dùng.");
            }

            var userID = int.Parse(userIdClaim.Value);
            var user = await _context.Users.FindAsync(userID);

            if (user == null)
            {
                return NotFound("User not found");
            }

            // Lấy số điểm của người dùng
            var userPoints = user.Point;

            var totalAmount = 0m;
            var orderProducts = new List<OrderProduct>();

            foreach (var item in products)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductID);
                if (product == null)
                {
                    return NotFound($"Sản phẩm với ID {item.ProductID} không tồn tại");
                }

                if (product.Quantity < item.Quantity)
                {
                    return BadRequest($"Sản phẩm {product.ProductName} không đủ số lượng (còn {product.Quantity} cái)");
                }

                var itemTotal = product.Price * item.Quantity;
                totalAmount += itemTotal;

                orderProducts.Add(new OrderProduct
                {
                    ProductId = product.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = itemTotal
                });

                // Giảm số lượng sản phẩm
                product.Quantity -= item.Quantity;
                
            }

            decimal discountAmount = 0m;
            if (promotionID.HasValue)
            {
                var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.PromotionId == promotionID);
                if (promotion != null)
                {
                    // Kiểm tra nếu điểm người dùng đủ để sử dụng khuyến mãi
                    if (promotion.Quantity <= 0 || promotion.IsActive == false)
                    {
                        return BadRequest("Out of Promotion or Promotion is inactive");
                    }
                    else if (userPoints < promotion.DiscountPercentage)
                    {
                        return BadRequest("You do not have enough points to use this promotion");
                    }

                    // Tính mức giảm giá và trừ điểm người dùng
                    discountAmount = totalAmount * (promotion.DiscountPercentage / 100);
                    user.Point -= Convert.ToInt32(promotion.DiscountPercentage); // Trừ điểm tương ứng
                    promotion.Quantity--;
                    _context.Promotions.Update(promotion);
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    return BadRequest("Invalid promotion");
                }
            }
            var finalAmount = totalAmount - discountAmount;

            var order = new Order
            {
                UserId = userID,
                OrderDate = DateTime.Now,
                TotalAmount = totalAmount,
                PromotionId = promotionID,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                Status = "Pending"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var orderProduct in orderProducts)
            {
                orderProduct.OrderId = order.OrderId;
                _context.OrderProducts.Add(orderProduct);
            }

            await _context.SaveChangesAsync();

            return Ok(new { order.OrderId, order.Status, totalAmount, discountAmount, finalAmount });
        }


        [HttpPost("cancel/{orderId}")]
        [Authorize]
        public async Task<IActionResult> CancelOrder(int orderId, [FromBody] string cancelReason) // Thay đổi parameter
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("Invalid token or missing userId claim.");
            }

            // Kiểm tra lý do hủy
            if (string.IsNullOrEmpty(cancelReason))
            {
                return BadRequest("Vui lòng nhập lý do hủy đơn hàng.");
            }

            var userId = int.Parse(userIdClaim);

            var order = await _context.Orders
                .Include(o => o.OrderProducts)
                .ThenInclude(op => op.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound("Không tìm thấy đơn hàng hoặc bạn không có quyền hủy đơn hàng này.");
            }

            if (order.Status != "Pending")
            {
                return BadRequest("Chỉ có thể hủy đơn hàng ở trạng thái chờ xử lý.");
            }

            try
            {
                // Hoàn trả lại số lượng sản phẩm
                foreach (var orderProduct in order.OrderProducts)
                {
                    if (orderProduct.Product != null)
                    {
                        orderProduct.Product.Quantity += orderProduct.Quantity;
                    }
                }

                if (order.PromotionId.HasValue)
                {
                    var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.PromotionId == order.PromotionId);
                    if (promotion != null)
                    {
                        var user = await _context.Users.FindAsync(userId);
                        // Hoàn lại điểm cho người dùng
                        user.Point += Convert.ToInt32(promotion.DiscountPercentage);
                        // Tăng lại số lượng khuyến mãi sau khi hủy
                        promotion.Quantity++;
                        _context.Promotions.Update(promotion);
                        _context.Users.Update(user);
                    }
                }

                // Cập nhật trạng thái đơn hàng với lý do từ FE
                order.Status = "Cancelled";
                order.CancelledDate = DateTime.Now;
                order.CancelledReason = cancelReason; // Sử dụng lý do từ request

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Đơn hàng đã được hủy thành công.",
                    orderId = order.OrderId,
                    status = order.Status,
                    cancelledDate = order.CancelledDate,
                    cancelledReason = order.CancelledReason
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi hủy đơn hàng.", error = ex.Message });
            }
        }

    }
    public class OrderProductRequest
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
    }
}