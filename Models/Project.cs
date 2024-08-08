namespace MyPortfolioBackend.Models
{
  public class Project
  {
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Image { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public string? Description { get; set; }
  }
}