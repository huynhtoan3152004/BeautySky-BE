using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautySky.Models;
using BeautySky.DTO;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ProjectSwpContext _context;

        public UsersController(ProjectSwpContext context)
        {
            _context = context;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return user;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest updatedUser)
        {
            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null)
            {
                return NotFound(new { message = "User not found." });
            }
            if (updatedUser.Point < 0)
                return BadRequest("Point can not negative");

            if (!string.IsNullOrEmpty(updatedUser.UserName))
                existingUser.UserName = updatedUser.UserName;
            if (!string.IsNullOrEmpty(updatedUser.FullName))
                existingUser.FullName = updatedUser.FullName;

            if (!string.IsNullOrEmpty(updatedUser.Email))
            {
                var emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.(com|vn|net|org|edu|gov|info)$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(updatedUser.Email, emailRegex))
                {
                    return BadRequest(new { message = "Email không hợp lệ. Chỉ chấp nhận đuôi: .com, .vn, .net, .org, .edu, .gov, .info" });
                }
                existingUser.Email = updatedUser.Email;
            }           

            if (!string.IsNullOrEmpty(updatedUser.Password))
                existingUser.Password = updatedUser.Password;

            if (!string.IsNullOrEmpty(updatedUser.ConfirmPassword))
                existingUser.ConfirmPassword = updatedUser.ConfirmPassword;

            if (updatedUser.Password != updatedUser.ConfirmPassword)
            {
                return BadRequest("Mật khẩu và xác nhận mật khẩu không khớp.");
            }

            if (updatedUser.RoleId.HasValue)
                existingUser.RoleId = updatedUser.RoleId;

            if (!string.IsNullOrEmpty(updatedUser.Phone))
                existingUser.Phone = updatedUser.Phone;

            if (!string.IsNullOrEmpty(updatedUser.Address))
                existingUser.Address = updatedUser.Address;

            if (updatedUser.IsActive.HasValue)
                existingUser.IsActive = updatedUser.IsActive;
            if (updatedUser.Point >0)
                existingUser.Point = updatedUser.Point;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Update Successful", user = existingUser });
        }

        // POST: api/Users
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
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
            user.IsActive = true;
            user.DateCreate = DateTime.UtcNow;
            _context.Users.Add(user);
            if (!TryValidateModel(user))
            {
                return BadRequest(ModelState);
            }
            await _context.SaveChangesAsync();
            return Ok(user);
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            user.IsActive = false;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        
        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }
    }
}
