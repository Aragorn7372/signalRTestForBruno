// =============================================================
// SignalR Test Server — Program.cs
//
// This is the entry point of the ASP.NET Core application.
// It sets up:
//   1. Infrastructure services (JWT options, user tracker)
//   2. SignalR with the TestHub at /hub
//   3. CORS (permissive for testing)
//   4. JWT Bearer authentication
//   5. Endpoints: GET / (health), GET /token (JWT generation), /hub (SignalR)
// =============================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using signalRTestForBruno.Hub;
using signalRTestForBruno.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register infrastructure services:
//   - JwtOptions bound from the "Jwt" config section
//   - ConnectedUserTracker as singleton (shared across all hub connections)
builder.Services.AddInfrastructure(builder.Configuration);

// Add SignalR services to the DI container.
// This registers everything needed for real-time communication.
builder.Services.AddSignalR();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.SetIsOriginAllowed(_ => true)
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials()));

// Read JWT configuration for manual use in authentication setup
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptionsConfig = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

// Configure JWT Bearer authentication
// The token is validated on every request to the hub
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Validate the token's issuer, audience, lifetime, and signing key
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptionsConfig.Issuer,
            ValidAudience = jwtOptionsConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptionsConfig.SecretKey))
        };

        // CRITICAL for SignalR: WebSocket connections cannot set the
        // standard "Authorization: Bearer" header. Instead, SignalR
        // passes the token as "?access_token=<jwt>" in the query string.
        // This event reads it from there and sets it on the context.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware pipeline order matters: CORS → Authentication → Authorization
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Health-check endpoint
app.MapGet("/", () => "SignalR server OK");

// JWT token generation endpoint for testing convenience.
// Returns a signed JWT with random user claims (name, role, favoriteColor).
// Clients call this first, then pass the token to the SignalR connection.
app.MapGet("/token", (IOptions<JwtOptions> jwtOptionsProvider) =>
{
    var opts = jwtOptionsProvider.Value;

    var random = Random.Shared;
    var names = new[] { "María García", "Carlos López", "Ana Martínez", "Pedro Sánchez", "Laura Fernández" };
    var roles = new[] { "admin", "user", "moderator", "viewer" };
    var colors = new[] { "#FF5733", "#33FF57", "#3357FF", "#F333FF", "#FF33A8" };

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new(ClaimTypes.Name, names[random.Next(names.Length)]),
        new(ClaimTypes.Role, roles[random.Next(roles.Length)]),
        new("favoriteColor", colors[random.Next(colors.Length)])
    };

    var expires = DateTime.UtcNow.AddMinutes(opts.ExpirationMinutes);
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SecretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        opts.Issuer,
        opts.Audience,
        claims,
        expires: expires,
        signingCredentials: credentials);

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new
    {
        token = tokenString,
        expiresAt = expires,
        claims = new Dictionary<string, string>
        {
            ["sub"] = claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value,
            ["name"] = claims.First(c => c.Type == ClaimTypes.Name).Value,
            ["role"] = claims.First(c => c.Type == ClaimTypes.Role).Value,
            ["favoriteColor"] = claims.First(c => c.Type == "favoriteColor").Value
        }
    });
});

// Map the SignalR hub at /hub
// Clients connect to: http://localhost:5246/hub?access_token=<jwt>
// The TestHub exposes: Broadcast, JoinGroup, LeaveGroup, GroupChat, IndividualChat, GetConnectedUsers
app.MapHub<TestHub>("/hub");

await app.RunAsync();
