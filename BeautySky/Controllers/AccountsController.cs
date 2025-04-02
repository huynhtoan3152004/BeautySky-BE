using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BeautySky.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using BeautySky.DTO;
using System.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly ProjectSwpContext _context;
        private readonly IConfiguration _configuration;

        public AccountsController(ProjectSwpContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/Accounts/Register
        [HttpPost("Register")]
        public async Task<ActionResult<User>> Register([FromBody] User user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Kiểm tra nếu người dùng đã tồn tại trong hệ thống
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == user.UserName || u.Email == user.Email);
            if (existingUser != null)
            {
                return BadRequest("Tên người dùng hoặc email đã tồn tại.");
            }

            // Kiểm tra mật khẩu và xác nhận mật khẩu có trùng khớp hay không
            if (user.Password != user.ConfirmPassword)
            {
                return BadRequest("Mật khẩu và xác nhận mật khẩu không khớp.");
            }

            // Check Email
            var emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.(com|vn|net|org|edu|gov|info)$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(user.Email, emailRegex))
            {
                return BadRequest("Email không hợp lệ.");
            }

            user.RoleId = 1; // Customer
            user.Point = 0;
            user.IsActive = true;
            user.DateCreate = DateTime.UtcNow;
            _context.Users.Add(user);
            if (!TryValidateModel(user))
            {
                return BadRequest(ModelState);
            }
            await _context.SaveChangesAsync();
            return Ok("Register success");
        }

        [HttpPost("Login")]
        public async Task<ActionResult<User>> Login([FromBody] LoginRequest loginRequest)
        {
            // Kiểm tra xem có nhập UserName hay Email
            if (string.IsNullOrEmpty(loginRequest.UserName) && string.IsNullOrEmpty(loginRequest.Email))
            {
                return BadRequest("Bạn cần nhập tên đăng nhập hoặc email.");
            }

            // Tìm người dùng theo UserName hoặc Email
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    (u.UserName == loginRequest.UserName || u.Email == loginRequest.Email) &&
                    u.Password == loginRequest.Password);

            // Kiểm tra nếu không tìm thấy người dùng hoặc mật khẩu sai
            
            if (user == null)
            {
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không chính xác.");
            }
            if (user.IsActive == false)
            {
                return Unauthorized("Tài khoản của bạn đã bị vô hiệu hóa.");
            }
            user.Password = null;
            var token = GenerateJwtToken(user);

            return Ok(new { Token = token, RoleId = user.RoleId});
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
