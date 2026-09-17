using Gov.WebApi.Services;
using Gov.WebApi.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

var tenantId = builder.Configuration["Authentication:TenantId"]
    ?? throw new InvalidOperationException("Authentication:TenantId is required.");
var apiClientId = builder.Configuration["Authentication:ApiClientId"]
    ?? throw new InvalidOperationException("Authentication:ApiClientId is required.");
var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
var oauthResource = $"api://{apiClientId}";
var mcpScope = $"{oauthResource}/Mcp.Access";
var publicBaseUrl = builder.Configuration["PublicBaseUrl"]?.TrimEnd('/')
    ?? throw new InvalidOperationException("PublicBaseUrl is required.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<ProjectCatalog>();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Accept both v2.0 tokens (issued when the app registration exposes an API
            // scope, e.g. api://{ApiClientId}/Mcp.Access) and v1.0 tokens (issued when no
            // scope has been configured yet and a token is requested for the raw client ID,
            // e.g. via `az account get-access-token --resource <ApiClientId>`). This keeps
            // local testing working against a freshly created app registration that has no
            // exposed scope yet.
            ValidIssuers = [authority, $"https://sts.windows.net/{tenantId}/"],
            ValidAudiences = [apiClientId, $"api://{apiClientId}"],
            NameClaimType = "name"
        };
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = new()
        {
            Resource = oauthResource,
            ResourceDocumentation = $"{publicBaseUrl}/docs",
            AuthorizationServers = { authority },
            //ScopesSupported = { mcpScope }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
       // policy.RequireClaim("scp", "Mcp.Access");
    });

    // Require authentication for every endpoint by default; endpoints that must stay
    // public (health check, docs redirect, etc.) opt out explicitly with AllowAnonymous().
    options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<ProjectTools>();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Government REST and MCP API",
        Version = "v1",
        Description = """
            This application exposes the same project catalog through two interfaces:

            - **REST API:** `GET /api/project` for browsers and conventional HTTP clients.
            - **Hosted MCP server:** `/mcp` using the MCP Streamable HTTP transport.
            - **MCP tool:** `get_projects` returns every configured project name.
            - **Authentication:** Microsoft Entra ID OAuth with delegated `Mcp.Access`.

            When deployed to Azure App Service at `https://<app-name>.azurewebsites.net`,
            configure MCP clients with:

            `https://<app-name>.azurewebsites.net/mcp`

            The MCP endpoint uses JSON-RPC and is intended for MCP clients. Use the REST
            operation below with **Try it out** for direct browser testing.
            """
    });
    options.AddServer(new OpenApiServer
    {
        Url = "/",
        Description = "Current host"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "docs";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Government API v1");
    options.DocumentTitle = "Government API Documentation";
    options.EnableTryItOutByDefault();
});
app.UseReDoc(options =>
{
    options.RoutePrefix = "reference";
    options.SpecUrl = "/swagger/v1/swagger.json";
    options.DocumentTitle = "Government API Reference";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/docs"))
    .ExcludeFromDescription()
    .AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck")
    .WithSummary("Checks whether the REST and MCP host is running.")
    .WithTags("Service")
    .AllowAnonymous();
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization("McpAccess")
    .RequireAuthorization("McpAccess");

app.Run();
