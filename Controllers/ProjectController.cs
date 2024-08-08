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
        Description = createProjectDto.Description,
        Tags = createProjectDto.Tags ?? new List<string>()
      };

      if (createProjectDto.Image != null)
      {
        project.Image = await SaveImage(createProjectDto.Image);
      }

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
      existingProject.Description = updateProjectDto.Description ?? existingProject.Description;
      existingProject.Tags = updateProjectDto.Tags ?? existingProject.Tags;

      if (updateProjectDto.Image != null)
      {
        if (!string.IsNullOrEmpty(existingProject.Image))
        {
          DeleteImage(existingProject.Image);
        }
        var imagePath = await SaveImage(updateProjectDto.Image);
        existingProject.Image = imagePath;
      }

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

      if (!string.IsNullOrEmpty(project.Image))
      {
        DeleteImage(project.Image);
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

      foreach (var project in projectsToDelete)
      {
        if (!string.IsNullOrEmpty(project.Image))
        {
          DeleteImage(project.Image);
        }
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

    private async Task<string> SaveImage(IFormFile image)
    {
      var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "Projects");
      if (!Directory.Exists(uploadsFolder))
      {
        Directory.CreateDirectory(uploadsFolder);
      }

      var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
      var filePath = Path.Combine(uploadsFolder, uniqueFileName);

      using (var fileStream = new FileStream(filePath, FileMode.Create))
      {
        await image.CopyToAsync(fileStream);
      }

      return $"/uploads/Projects/{uniqueFileName}"; // Return the relative path
    }

    private void DeleteImage(string imagePath)
    {
      if (string.IsNullOrEmpty(imagePath))
      {
        return; // Exit the method if imagePath is null or empty
      }

      var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "Projects");
      var fullPath = Path.Combine(uploadsFolder, Path.GetFileName(imagePath));

      if (System.IO.File.Exists(fullPath))
      {
        System.IO.File.Delete(fullPath);
      }
    }
  }

  public class CreateProjectDto
  {
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public IFormFile? Image { get; set; }
  }

  public class UpdateProjectDto
  {
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public IFormFile? Image { get; set; }
  }
}