// Controllers/AuthController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPortfolioBackend.Data;
using MyPortfolioBackend.Models;
using MyPortfolioBackend.Services;

namespace MyPortfolioBackend.Controllers
{
  [EnableCors("MyAllowedOrigins")]
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;

    public AuthController(ApplicationDbContext context, JwtService jwtService)
    {
      _context = context;
      _jwtService = jwtService;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetUserData()
    {
      // The [Authorize] attribute will ensure a 401 is returned if there's no valid token

      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      var userEmail = User.FindFirstValue(ClaimTypes.Email);
      var userName = User.FindFirstValue(ClaimTypes.Name);

      if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userName))
      {
        return Unauthorized("Invalid token");
      }

      var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

      if (user == null)
      {
        return NotFound("User not found");
      }

      return Ok(new
      {
        Email = userEmail,
        Username = userName
      });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterModel model)
    {
      if (await _context.Users.AnyAsync(u => u.Username == model.Username))
      {
        return BadRequest("Username already exists");
      }

      var user = new User
      {
        Email = model.Email,
        Username = model.Username,
        Password = HashPassword(model.Password)
      };

      _context.Users.Add(user);
      await _context.SaveChangesAsync();

      return Ok("User registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginModel model)
    {
      var user = await _context.Users.FirstOrDefaultAsync(u =>
          u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

      if (user == null || !VerifyPassword(model.Password, user.Password))
      {
        return Unauthorized(new { message = "Invalid username/email or password" });
      }

      var token = _jwtService.GenerateToken(user);

      Response.Cookies.Append("token", token, new CookieOptions
      {
        HttpOnly = true,
        Secure = true, // for HTTPS
        SameSite = SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddDays(1) // Set to expire in 1 day
      });

      return Ok(new
      {
        message = "Login successful",
        username = user.Username,
        email = user.Email,
        token
      });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
      Response.Cookies.Delete("token", new CookieOptions
      {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict
      });

      return Ok(new { message = "Logged out successfully" });
    }

    private string HashPassword(string password)
    {
      return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hashedPassword)
    {
      return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
  }


  public class RegisterModel
  {
    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
  }

  public class LoginModel
  {
    public required string UsernameOrEmail { get; set; }
    public required string Password { get; set; }
  }
}