using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services.Github;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    Settings _settings;
    ILambdaLogger _logger;
    GitHubService _github;

    public CredentialsPublisher(Settings settings, ILambdaContext context)
    {
        _settings = settings;
        _logger = context.Logger;
        _github = new GitHubService(settings, context);
    }

    public async Task PublishAccessKey(AccessKey accessKey)
    {
        var projects = GetProjects();
        var environment = _settings.Environment;
        var organizationName = "Virtual-Finland-Development";

        foreach (var project in projects)
        {
            _logger.LogLine($"Publishing key {accessKey.AccessKeyId} to project {project.Name}..");

            await _github.CreateOrUpdateEnvironmentSecret(
                organizationName,
                project.Name,
                environment,
                "AWS_ACCESS_KEY_ID",
                accessKey.AccessKeyId
            );

            await _github.CreateOrUpdateEnvironmentSecret(
                organizationName,
                project.Name,
                environment,
                "AWS_ACCESS_KEY_SECRET",
                accessKey.SecretAccessKey
            );

            _logger.LogLine("Key published");
        }
    }

    List<Project> GetProjects()
    {
        return new List<Project>
        {
            new Project { Name = "status-info-api" },
        };
    }
}

public record Project
{
    public string Name { get; init; } = "";
}