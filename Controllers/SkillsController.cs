using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPortfolioBackend.Data;
using MyPortfolioBackend.Models;

namespace MyPortfolioBackend.Controllers
{
  [EnableCors("MyAllowedOrigins")]
  [ApiController]
  [Route("api/[controller]")]
  public class SkillsController : ControllerBase
  {
    private readonly ApplicationDbContext _context;

    public SkillsController(ApplicationDbContext context)
    {
      _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Skill>>> GetSkills()
    {
      var skills = await _context.Skills.ToListAsync();
      return Ok(new { data = skills });
    }

    [HttpGet("paged")]
    public async Task<ActionResult<IEnumerable<Skill>>> GetPagedSkills([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
      if (page < 1 || limit < 1)
      {
        return BadRequest(new { message = "Invalid page or limit. Both must be greater than 0." });
      }

      var totalCount = await _context.Skills.CountAsync();
      var totalPages = (int)Math.Ceiling((double)totalCount / limit);

      var skills = await _context.Skills
          .OrderByDescending(skill => skill.Id)
          .Skip((page - 1) * limit)
          .Take(limit)
          .ToListAsync();

      var response = new
      {
        data = skills,
        currentPage = page,
        limit,
        totalCount,
        totalPages
      };

      return Ok(response);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Skill>> CreateSkill(CreateSkillDto createSkillDto)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized("Invalid token");
      }

      if (createSkillDto == null || string.IsNullOrEmpty(createSkillDto.Name))
      {
        return BadRequest("Skill name cannot be empty");
      }

      if (await _context.Skills.AnyAsync(skill => skill.Name.ToLower() == createSkillDto.Name.ToLower()))
      {
        return BadRequest(new { message = "Skill already exists" });
      }

      var skill = new Skill { Name = createSkillDto.Name };

      _context.Skills.Add(skill);
      await _context.SaveChangesAsync();

      var response = new
      {
        message = "Skill created successfully",
        skill
      };
      return CreatedAtAction(nameof(GetSkills), new { id = skill.Id }, response);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSkill(int id, UpdateSkillDto updateSkillDto)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized(new { message = "Invalid token" });
      }

      if (string.IsNullOrEmpty(updateSkillDto.Name))
      {
        return BadRequest(new { message = "Updated skill name cannot be empty" });
      }

      var existingSkill = await _context.Skills.FindAsync(id);
      if (existingSkill == null)
      {
        return NotFound();
      }

      try
      {
        existingSkill.Name = updateSkillDto.Name;
        await _context.SaveChangesAsync();
      }
      catch (DbUpdateConcurrencyException)
      {
        if (!SkillExists(id))
        {
          return NotFound();
        }
        else
        {
          throw;
        }
      }

      var updatedSkill = await _context.Skills.FindAsync(id);
      var response = new
      {
        message = "Skill updated successfully",
        skill = updatedSkill
      };
      return Ok(response);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSkill(int id)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized(new { message = "Invalid token" });
      }

      var skill = await _context.Skills.FindAsync(id);
      if (skill == null)
      {
        return NotFound();
      }

      _context.Skills.Remove(skill);
      await _context.SaveChangesAsync();

      var response = new
      {
        message = "Skill deleted successfully",
        skill
      };
      return Ok(response);
    }

    [Authorize]
    [HttpPost("delete-multiple")]
    public async Task<IActionResult> DeleteSkills([FromBody] int[] ids)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized(new { message = "Invalid token" });
      }

      var deletedSkills = new List<Skill>();

      foreach (var id in ids)
      {
        var skill = await _context.Skills.FindAsync(id);
        if (skill != null)
        {
          _context.Skills.Remove(skill);
          deletedSkills.Add(skill);
        }
      }

      await _context.SaveChangesAsync();

      var response = new
      {
        message = "Skills deleted successfully",
      };
      return Ok(response);
    }

    private bool SkillExists(int id)
    {
      return _context.Skills.Any(e => e.Id == id);
    }
  }

  public class CreateSkillDto
  {
    public required string Name { get; set; }
  }

  public class UpdateSkillDto
  {
    public required string Name { get; set; }
  }
}