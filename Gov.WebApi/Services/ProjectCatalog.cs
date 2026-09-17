namespace Gov.WebApi.Services;

public sealed class ProjectCatalog(IConfiguration configuration)
{
    public IReadOnlyList<string> GetProjectNames()
    {
        return configuration
            .GetSection("Projects")
            .Get<string[]>()
            ?? [];
    }
}
