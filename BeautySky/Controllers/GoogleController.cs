using BeautySky.DTO;
using BeautySky.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GoogleController : ControllerBase
    {

        private readonly ProjectSwpContext _context;
        private readonly IConfiguration _configuration;

        public GoogleController(ProjectSwpContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleRespone") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-respone")]
        public async Task<IActionResult> GoogleRespone()
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!result.Succeeded)
                return BadRequest("Google Authentication Failed");

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            if (email == null)
                return BadRequest("Email not found in Google response.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            var token = GenerateJwtToken(user);

            // Tạo URL redirect về frontend với thông tin xác thực
            var frontendUrl = "http://localhost:5173/google-callback"; // URL của trang callback trong React
            var queryString = $"?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}&roleId={user.RoleId}";

            // Redirect về frontend
            return Redirect(frontendUrl + queryString);
        }


        [HttpPost("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return NotFound("Không tìm thấy User");

            user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? user.Phone ?? "Chưa cập nhật" : model.Phone;
            user.Address = string.IsNullOrWhiteSpace(model.Address) ? user.Address ?? "Chưa cập nhật" : model.Address;
            if (_context.Entry(user).State == EntityState.Modified)
            {
                await _context.SaveChangesAsync();
                return Ok("Cập nhật thành công");
            }
            return Ok("Không có thay đổi");
        }


        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            string roleName = "Customer";
            if (user.RoleId.HasValue)
            {
                roleName = _context.Roles
                    .Where(r => r.RoleId == user.RoleId)
                    .Select(r => r.RoleName)
                    .FirstOrDefault() ?? "Customer";
            }

            var claims = new List<Claim>
            {
                new Claim("userId", user.UserId.ToString()),
                new Claim("name", user.UserName),
                new Claim("email", user.Email),
                new Claim("role", roleName),
                new Claim("phone", user.Phone),
                new Claim("address", user.Address),
                new Claim("point", user.Point.ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(3),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}