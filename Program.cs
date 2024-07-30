using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyPortfolioBackend.Data;
using MyPortfolioBackend.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
  options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
  {
    Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below.",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer"
  });

  options.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddScoped<JwtService>();

builder.Services.AddLogging(logging =>
{
  logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
  logging.AddConsole();
  logging.AddDebug();
});

builder.Services.AddCors(options =>
{
  options.AddPolicy("MyAllowedOrigins",
      policy =>
      {
        policy.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
      });
});

// Configure DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ClockSkew = TimeSpan.Zero
      };

      options.Events = new JwtBearerEvents
      {
        OnMessageReceived = context =>
        {
          if (context.Request.Cookies.ContainsKey("token"))
          {
            context.Token = context.Request.Cookies["token"];
          }
          return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
          // Skip the default logic.
          context.HandleResponse();
          context.Response.StatusCode = 401;
          context.Response.ContentType = "application/json";
          var result = JsonSerializer.Serialize("You are not authorized");
          return context.Response.WriteAsync(result);
        }
      };

    }
    );

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
  if (context.Request.Method == "OPTIONS")
  {
    context.Response.Headers.Add("Access-Control-Allow-Origin", "http://localhost:3000");
    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization");
    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
    context.Response.StatusCode = 200;
    return;
  }

  await next();
});
app.UseCors("MyAllowedOrigins");
app.Use(async (context, next) =>
{
  var token = context.Request.Cookies["token"];
  if (!string.IsNullOrEmpty(token))
  {
    context.Request.Headers.Add("Authorization", "Bearer " + token);
  }
  await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


app.Run();