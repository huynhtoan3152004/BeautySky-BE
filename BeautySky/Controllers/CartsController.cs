using BeautySky.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[Route("api/[controller]")]
[ApiController]
public class CartsController : ControllerBase
{
    private readonly ProjectSwpContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string SessionCartKey = "Cart";

    public CartsController(ProjectSwpContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetCarts()
    {
        int? userId = GetUserId();


        if (userId == null)
        {
            // Lấy giỏ hàng từ session cho guest
            var cartList = GetCartFromSession();
            foreach (var item in cartList)
            {
                var product = await _context.Products
                    .Include(p => p.ProductsImages)
                    .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                if (product != null)
                {
                    item.TotalPrice = product.Price * item.Quantity;
                    item.Product = product;
                }
            }

            var sessionCartItems = cartList.Select(c => new {
                c.CartId,
                c.ProductId,
                c.Quantity,
                c.TotalPrice,
                ProductName = c.Product?.ProductName,
                Price = c.Product?.Price,
                ProductImage = c.Product?.ProductsImages.FirstOrDefault()?.ImageUrl
            });

            return Ok(new
            {
                TotalPrice = cartList.Sum(c => c.TotalPrice),
                Items = sessionCartItems
            });
        }

        // Lấy giỏ hàng từ database cho user đã đăng nhập
        var userCart = await _context.Carts
            .Include(c => c.Product)
                .ThenInclude(p => p.ProductsImages)
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.CartId,
                c.UserId,
                c.ProductId,
                c.Quantity,
                TotalPrice = c.Quantity * c.Product.Price,
                ProductName = c.Product.ProductName,
                Price = c.Product.Price,
                ProductImage = c.Product.ProductsImages.FirstOrDefault().ImageUrl
            })
            .ToListAsync();

        return Ok(new
        {
            UserId = userId,
            TotalPrice = userCart.Sum(c => c.TotalPrice),
            Items = userCart
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncSessionCartToDatabase()
    {
        int? userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized("Người dùng chưa đăng nhập");
        }

        try
        {
            var sessionCart = GetCartFromSession();
            if (sessionCart.Any())
            {
                foreach (var item in sessionCart)
                {
                    var product = await _context.Products
                        .Include(p => p.ProductsImages)
                        .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                    if (product == null) continue;

                    var existingCartItem = await _context.Carts
                        .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == item.ProductId);

                    if (existingCartItem != null)
                    {
                        existingCartItem.Quantity += item.Quantity;
                        existingCartItem.TotalPrice = existingCartItem.Quantity * product.Price;
                    }
                    else
                    {
                        var newCartItem = new Cart
                        {
                            UserId = userId.Value,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            TotalPrice = item.Quantity * product.Price
                        };
                        _context.Carts.Add(newCartItem);
                    }
                }

                await _context.SaveChangesAsync();
                ClearSessionCart(); // Xóa session cart sau khi đồng bộ
            }

            return Ok(new { message = "Giỏ hàng đã được đồng bộ thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi đồng bộ giỏ hàng", error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> PostCart(Cart cart)
    {
        var product = await _context.Products
            .Include(p => p.ProductsImages)
            .FirstOrDefaultAsync(p => p.ProductId == cart.ProductId);

        if (product == null) return NotFound("Sản phẩm không tồn tại.");

        if (cart.Quantity <= 0)
        {
            return BadRequest("Không thể thêm số lượng bé hơn hoặc bằng 0");
        }

        if (cart.Quantity > product.Quantity)
        {
            return BadRequest($"Số lượng sản phẩm vượt quá số lượng trong kho. Số lượng còn lại là {product.Quantity}.");
        }

        if (product.Expire < DateTime.Now)
        {
            return BadRequest($"Sản phẩm đã hết hạn {product.Expire}");
        }

        if (product.IsActive == false)
        {
            return BadRequest($"Sản phẩm đang không hoạt động {product.IsActive}");
        }
        cart.TotalPrice = product.Price * cart.Quantity;
        int? userId = GetUserId();

        if (userId == null)
        {
            var cartList = GetCartFromSession();
            var existingItem = cartList.FirstOrDefault(c => c.ProductId == cart.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity = cart.Quantity;
                existingItem.TotalPrice = cart.TotalPrice;
                existingItem.Product = product;
            }
            else
            {
                cart.CartId = cartList.Count > 0 ? cartList.Max(c => c.CartId) + 1 : 1;
                cart.Product = product;
                cartList.Add(cart);
            }

            SaveCartToSession(cartList);

            return Ok(new
            {
                message = "Sản phẩm đã thêm vào giỏ hàng tạm thời.",
                cart = new
                {
                    cart.CartId,
                    cart.ProductId,
                    cart.Quantity,
                    cart.TotalPrice,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    ProductImage = product.ProductsImages.FirstOrDefault()?.ImageUrl
                }
            });
        }

        // Xử lý cho user đã đăng nhập
        cart.UserId = userId.Value;
        var dbCartItem = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == cart.ProductId);

        if (dbCartItem != null)
        {
            dbCartItem.Quantity = cart.Quantity;
            dbCartItem.TotalPrice = cart.TotalPrice;
        }
        else
        {
            _context.Carts.Add(cart);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Sản phẩm đã thêm vào giỏ hàng.",
            cart = new
            {
                cart.CartId,
                cart.ProductId,
                cart.Quantity,  
                cart.TotalPrice,
                ProductName = product.ProductName,
                Price = product.Price,
                ProductImage = product.ProductsImages.FirstOrDefault()?.ImageUrl
            }
        });
    }

    [HttpPut]
    public async Task<IActionResult> PutCart(Cart cart)
    {
        var product = await _context.Products
            .Include(p => p.ProductsImages)
            .FirstOrDefaultAsync(p => p.ProductId == cart.ProductId);

        if (product == null) return NotFound("Sản phẩm không tồn tại.");

        if (cart.Quantity <= 0)
        {
            return BadRequest("Không thể chỉnh sửa số lượng bé hơn hoặc bằng 0");
        }

        if (cart.Quantity > product.Quantity)
        {
            return BadRequest($"Số lượng sản phẩm vượt quá số lượng trong kho. Số lượng còn lại là {product.Quantity}.");
        }

        if (product.Expire < DateTime.Now)
        {
            return BadRequest($"Sản phẩm đã hết hạn {product.Expire}");
        }

        if (product.IsActive == false)
        {
            return BadRequest($"Sản phẩm đang không hoạt động {product.IsActive}");
        }
        cart.TotalPrice = product.Price * cart.Quantity;
        int? userId = GetUserId();

        if (userId == null)
        {
            var cartList = GetCartFromSession();
            var existingItem = cartList.FirstOrDefault(c => c.ProductId == cart.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity = cart.Quantity;
                existingItem.TotalPrice = cart.TotalPrice;
                existingItem.Product = product;
            }
            else
            {
                cart.CartId = cartList.Count > 0 ? cartList.Max(c => c.CartId) + 1 : 1;
                cart.Product = product;
                cartList.Add(cart);
            }

            SaveCartToSession(cartList);

            return Ok(new
            {
                message = "Giỏ hàng tạm thời đã được cập nhật.",
                cart = new
                {
                    cart.CartId,
                    cart.ProductId,
                    cart.Quantity,
                    cart.TotalPrice,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    ProductImage = product.ProductsImages.FirstOrDefault()?.ImageUrl
                }
            });
        }

        cart.UserId = userId.Value;
        var dbCartItem = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == cart.ProductId);

        if (dbCartItem != null)
        {
            dbCartItem.Quantity = cart.Quantity;
            dbCartItem.TotalPrice = cart.TotalPrice;
        }
        else
        {
            _context.Carts.Add(cart);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Giỏ hàng đã được cập nhật.",
            cart = new
            {
                cart.CartId,
                cart.ProductId,
                cart.Quantity,
                cart.TotalPrice,
                ProductName = product.ProductName,
                Price = product.Price,
                ProductImage = product.ProductsImages.FirstOrDefault()?.ImageUrl
            }
        });
    }

    [HttpDelete("{productId}")]
    public async Task<IActionResult> DeleteCartItem(int productId)
    {
        int? userId = GetUserId();

        if (userId == null)
        {
            var cartList = GetCartFromSession();
            var itemToRemove = cartList.FirstOrDefault(c => c.ProductId == productId);
            if (itemToRemove != null)
            {
                cartList.Remove(itemToRemove);
                SaveCartToSession(cartList);
                return Ok(new { message = "Sản phẩm đã được xóa khỏi giỏ hàng tạm thời." });
            }
            return NotFound(new { message = "Sản phẩm không có trong giỏ hàng tạm thời." });
        }

        var dbCartItem = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (dbCartItem != null)
        {
            _context.Carts.Remove(dbCartItem);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Sản phẩm đã được xóa khỏi giỏ hàng." });
        }

        return NotFound(new { message = "Sản phẩm không có trong giỏ hàng của bạn." });
    }

    private int? GetUserId()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Log tất cả claims để debug
            foreach (var claim in User.Claims)
            {
                Console.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
            }

            // Kiểm tra nhiều loại claim có thể chứa userId
            string? userIdClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "userId" ||
                c.Type == "sub" ||  // Google sử dụng "sub" cho unique identifier
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            Console.WriteLine($"Found userId claim: {userIdClaim}");

            if (!string.IsNullOrEmpty(userIdClaim))
            {
                if (int.TryParse(userIdClaim, out int userId) && userId > 0)
                {
                    return userId;
                }
            }
        }
        return null;
    }

    private void SaveCartToSession(List<Cart> cartList)
    {
        var session = _httpContextAccessor.HttpContext.Session;
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            WriteIndented = true
        };
        session.SetString(SessionCartKey, JsonSerializer.Serialize(cartList, options));
    }

    private List<Cart> GetCartFromSession()
    {
        var session = _httpContextAccessor.HttpContext.Session;
        var cartData = session.GetString(SessionCartKey);
        if (string.IsNullOrEmpty(cartData)) return new List<Cart>();

        var options = new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            WriteIndented = true
        };

        try
        {
            return JsonSerializer.Deserialize<List<Cart>>(cartData, options) ?? new List<Cart>();
        }
        catch
        {
            return new List<Cart>();
        }
    }

    private void ClearSessionCart()
    {
        var session = _httpContextAccessor.HttpContext.Session;
        session.Remove(SessionCartKey);
    }
}