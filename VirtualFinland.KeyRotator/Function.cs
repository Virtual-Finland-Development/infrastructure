using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services;

namespace VirtualFinland.KeyRotator;

public class Function
{
    [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public async Task FunctionHandler(CloudwatchEventInput input, ILambdaContext context)
    {

        var logger = context.Logger;
        var settings = ResolveSettings(input);
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

    Settings ResolveSettings(CloudwatchEventInput input)
    {
        var inputObject = new Settings()
        {
            IAMUserName = Environment.GetEnvironmentVariable("CICD_BOT_IAM_USER_NAME") ?? string.Empty,
            Environment = input.Environment ?? Environment.GetEnvironmentVariable("ENVIRONMENT") ?? string.Empty,
            SecretName = Environment.GetEnvironmentVariable("SECRET_NAME") ?? string.Empty,
            SecretRegion = Environment.GetEnvironmentVariable("SECRET_REGION") ?? string.Empty,
            GitHubOrganizationName = Environment.GetEnvironmentVariable("GITHUB_ORGANIZATION_NAME") ?? "Virtual-Finland-Development",
            GitHubRepositoryNames = input.GitHubRepositoryNames?.Split(',')?.ToList() ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_NAMES")?.Split(',')?.ToList() ?? new List<string>()
        };

        return inputObject;
    }

    public record CloudwatchEventInput
    {
        [JsonPropertyName("ENVIRONMENT")]
        public string? Environment { get; set; }
        [JsonPropertyName("GITHUB_ORGANIZATION_NAME")]
        public string? GitHubOrganizationName { get; set; }
        [JsonPropertyName("GITHUB_REPOSITORY_NAMES")]
        public string? GitHubRepositoryNames { get; set; }
    }
}

public record Settings
{
    public string IAMUserName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string SecretName { get; set; } = string.Empty;
    public string SecretRegion { get; set; } = string.Empty;
    public string GitHubOrganizationName { get; set; } = string.Empty;
    public List<string> GitHubRepositoryNames { get; set; } = new List<string>();
}
