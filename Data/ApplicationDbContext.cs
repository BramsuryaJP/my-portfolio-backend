using Microsoft.EntityFrameworkCore;
using MyPortfolioBackend.Models;

namespace MyPortfolioBackend.Data
{
  public class ApplicationDbContext : DbContext
  {
    private readonly IConfiguration _configuration;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration)
        : base(options)
    {
      _configuration = configuration;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Project> Projects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      // Seed initial user data
      var username = _configuration["InitialUserData:INITIAL_USER_USERNAME"];
      var email = _configuration["InitialUserData:INITIAL_USER_EMAIL"];
      var password = _configuration["InitialUserData:INITIAL_USER_PASSWORD"];

      if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
      {
        modelBuilder.Entity<User>().HasData(
            new User
            {
              Id = 1,
              Username = username,
              Email = email,
              Password = BCrypt.Net.BCrypt.HashPassword(password)
            }
        );
      }
    }
  }
}