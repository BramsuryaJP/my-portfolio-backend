// Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPortfolioBackend.Data;
using MyPortfolioBackend.Models;
using MyPortfolioBackend.Services;

namespace MyPortfolioBackend.Controllers
{
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

    [Authorize]
    [HttpGet("protected")]
    public IActionResult Protected()
    {
      return Ok("This is a protected endpoint");
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
      return Ok(new
      {
        message = "Login successful",
        username = user.Username,
        email = user.Email,
        token
      });
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