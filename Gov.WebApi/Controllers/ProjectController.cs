using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Gov.WebApi.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Gov.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "McpAccess")]
public class ProjectController(ProjectCatalog projectCatalog) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Lists all available projects",
        Description = "Returns the same project names exposed to MCP clients by the get_projects tool.",
        OperationId = "GetProjects",
        Tags = ["Projects"])]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetAll()
    {
        return Ok(projectCatalog.GetProjectNames());
    }
}
