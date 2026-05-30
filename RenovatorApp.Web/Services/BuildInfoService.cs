namespace RenovatorApp.Web.Services;

public sealed class BuildInfoService
{
    private readonly IConfiguration _configuration;

    public BuildInfoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string DisplayVersion
    {
        get
        {
            var commitSha = _configuration["RAILWAY_GIT_COMMIT_SHA"];
            var environmentName = _configuration["RAILWAY_ENVIRONMENT_NAME"];

            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return "Build: local";
            }

            var shortSha = commitSha.Length > 7 ? commitSha[..7] : commitSha;
            return string.IsNullOrWhiteSpace(environmentName)
                ? $"Build: {shortSha}"
                : $"Build: {shortSha} / {environmentName}";
        }
    }
}
