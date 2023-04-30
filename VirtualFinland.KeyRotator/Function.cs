using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services;

namespace VirtualFinland.KeyRotator;

public class Function
{
    public async Task FunctionHandler(ILambdaContext context)
    {
        var logger = context.Logger;
        var settings = ResolveSettings();
        var rotator = new AccessKeyRotator(settings, logger);
        var credentialsPublisher = new CredentialsPublisher(settings, logger);

        var newKey = rotator.RotateAccessKey();
        if (newKey != null)
        {
            // Publish new key to the pipelines
            await credentialsPublisher.PublishAccessKey(newKey);
        }
        context.Logger.LogInformation("Key rotations completed");
    }

    Settings ResolveSettings()
    {
        var inputObject = new Settings()
        {
            IAMUserName = Environment.GetEnvironmentVariable("CICD_BOT_IAM_USER_NAME") ?? string.Empty,
            Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? string.Empty,
            SecretName = Environment.GetEnvironmentVariable("SECRET_NAME") ?? string.Empty,
            SecretRegion = Environment.GetEnvironmentVariable("SECRET_REGION") ?? string.Empty,
            GitHubOrganizationName = Environment.GetEnvironmentVariable("GITHUB_ORGANIZATION_NAME") ?? "Virtual-Finland-Development"
        };

        return inputObject;
    }
}

public record Settings
{
    public string IAMUserName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string SecretName { get; set; } = string.Empty;
    public string SecretRegion { get; set; } = string.Empty;
    public string GitHubOrganizationName { get; set; } = string.Empty;
}
