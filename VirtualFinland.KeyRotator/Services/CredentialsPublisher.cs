using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    ILambdaLogger _logger;
    public CredentialsPublisher(ILambdaContext context)
    {
        _logger = context.Logger;
    }

    public void PublishAccessKey(AccessKey accessKey)
    {
        var projects = GetProjects();
        foreach (var project in projects)
        {
            _logger.LogLine($"Publishing key {accessKey.AccessKeyId} to project {project.Name}");

        }
    }

    List<Project> GetProjects()
    {
        return new List<Project>
        {
            new Project { Name = "virtual-finland" },
            new Project { Name = "testbed-api" }, 
            // ...
        };
    }
}

public record Project
{
    public string Name { get; init; } = "";
}