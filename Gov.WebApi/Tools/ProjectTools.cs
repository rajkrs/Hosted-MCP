using System.ComponentModel;
using Gov.WebApi.Services;
using ModelContextProtocol.Server;

namespace Gov.WebApi.Tools;

[McpServerToolType]
public sealed class ProjectTools(ProjectCatalog projectCatalog)
{
    [McpServerTool(Name = "get_projects")]
    [Description("Gets the names of all projects available in the government project catalog.")]
    public IReadOnlyList<string> GetProjects()
    {
        return projectCatalog.GetProjectNames();
    }
}
