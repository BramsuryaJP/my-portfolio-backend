using System;
using System.IO;
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
  public class ProjectsController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProjectsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
      _context = context;
      _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects()
    {
      var projects = await _context.Projects.ToListAsync();
      return Ok(new { data = projects });
    }

    [HttpGet("paged")]
    public async Task<ActionResult<IEnumerable<Project>>> GetPagedProjects([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
      if (page < 1 || limit < 1)
      {
        return BadRequest(new { message = "Invalid page or limit. Both must be greater than 0." });
      }

      var totalCount = await _context.Projects.CountAsync();
      var totalPages = (int)Math.Ceiling((double)totalCount / limit);

      var projects = await _context.Projects
          .OrderByDescending(project => project.Id)
          .Skip((page - 1) * limit)
          .Take(limit)
          .ToListAsync();

      var response = new
      {
        data = projects,
        currentPage = page,
        limit,
        totalCount,
        totalPages
      };

      return Ok(response);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Project>> CreateProject([FromForm] CreateProjectDto createProjectDto)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized("Invalid token");
      }

      if (createProjectDto == null || string.IsNullOrEmpty(createProjectDto.Name))
      {
        return BadRequest("Project name cannot be empty");
      }

      if (await _context.Projects.AnyAsync(project => project.Name.ToLower() == createProjectDto.Name.ToLower()))
      {
        return BadRequest(new { message = "Project already exists" });
      }

      var project = new Project
      {
        Name = createProjectDto.Name,
        DescriptionEn = createProjectDto.DescriptionEn,
        DescriptionIna = createProjectDto.DescriptionIna,
        Tags = createProjectDto.Tags ?? new List<string>()
      };

      _context.Projects.Add(project);
      await _context.SaveChangesAsync();

      var response = new
      {
        message = "Project created successfully",
        project
      };
      return CreatedAtAction(nameof(GetProjects), new { id = project.Id }, response);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromForm] UpdateProjectDto updateProjectDto)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized(new { message = "Invalid token" });
      }

      var existingProject = await _context.Projects.FindAsync(id);
      if (existingProject == null)
      {
        return NotFound();
      }

      existingProject.Name = updateProjectDto.Name ?? existingProject.Name;
      existingProject.DescriptionEn = updateProjectDto.DescriptionEn ?? existingProject.DescriptionEn;
      existingProject.DescriptionIna = updateProjectDto.DescriptionIna ?? existingProject.DescriptionIna;
      existingProject.Tags = updateProjectDto.Tags ?? existingProject.Tags;

      try
      {
        await _context.SaveChangesAsync();
      }
      catch (DbUpdateConcurrencyException)
      {
        if (!ProjectExists(id))
        {
          return NotFound();
        }
        else
        {
          throw;
        }
      }

      var response = new
      {
        message = "Project updated successfully",
        project = existingProject
      };
      return Ok(response);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized(new { message = "Invalid token" });
      }

      var project = await _context.Projects.FindAsync(id);
      if (project == null)
      {
        return NotFound();
      }

      _context.Projects.Remove(project);
      await _context.SaveChangesAsync();

      var response = new
      {
        message = "Project deleted successfully",
        project
      };
      return Ok(response);
    }

    [Authorize]
    [HttpPost("delete-multiple")]
    public async Task<IActionResult> DeleteMultipleProjects([FromBody] List<int> projectIds)
    {
      var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId))
      {
        return Unauthorized(new { message = "Invalid token" });
      }

      if (projectIds == null || projectIds.Count == 0)
      {
        return BadRequest(new { message = "No project IDs provided" });
      }

      var projectsToDelete = await _context.Projects
          .Where(p => projectIds.Contains(p.Id))
          .ToListAsync();

      if (projectsToDelete.Count == 0)
      {
        return NotFound(new { message = "No projects found with the provided IDs" });
      }

      _context.Projects.RemoveRange(projectsToDelete);
      await _context.SaveChangesAsync();

      var response = new
      {
        message = $"{projectsToDelete.Count} projects deleted successfully",
        deletedProjects = projectsToDelete
      };
      return Ok(response);
    }

    private bool ProjectExists(int id)
    {
      return _context.Projects.Any(e => e.Id == id);
    }
  }

  public class CreateProjectDto
  {
    public required string Name { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionIna { get; set; }
    public List<string>? Tags { get; set; }
  }

  public class UpdateProjectDto
  {
    public string? Name { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionIna { get; set; }
    public List<string>? Tags { get; set; }
  }
}