using System.Text.Json.Serialization;
using Amazon.IdentityManagement;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using VirtualFinland.KeyRotator.Services;

namespace VirtualFinland.KeyRotator;

public class Function
{
    AmazonIdentityManagementServiceClient _iamClient;
    IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Default constructor that Lambda will invoke on instantiation.
    /// </summary>
    public Function()
    {
        _iamClient = new AmazonIdentityManagementServiceClient();
        var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        _httpClientFactory = serviceProvider.GetService<IHttpClientFactory>() ?? throw new NullReferenceException("IHttpClientFactory is null");
    }

    /// <summary>
    /// Override for unit testing
    /// </summary>
    public Function(AmazonIdentityManagementServiceClient iamClient, IHttpClientFactory httpClientFactory)
    {
        _iamClient = iamClient;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// A Lambda function that rotates the access key for the IAM user. Full cycle is 3 runs: create, invalidate old, delete old
    /// </summary>
    [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public async Task FunctionHandler(LambdaEventInput input, ILambdaContext context)
    {
        var logger = context.Logger;
        var settings = ResolveSettings(input);
        var rotator = new AccessKeyRotator(_iamClient, settings, logger);
        var credentialsPublisher = new CredentialsPublisher(_httpClientFactory, settings, logger);

        var newKey = await rotator.RotateAccessKey();
        if (newKey != null)
        {
            // Publish new key to the pipelines
            await credentialsPublisher.PublishAccessKey(newKey);
        }
        context.Logger.LogInformation("Key rotations completed");
    }

    public Settings ResolveSettings(LambdaEventInput input)
    {
        var inputObject = new Settings()
        {
            IAMUserName = Environment.GetEnvironmentVariable("CICD_BOT_IAM_USER_NAME") ?? string.Empty,
            Environment = input.Environment ?? Environment.GetEnvironmentVariable("ENVIRONMENT") ?? string.Empty,
            SecretName = Environment.GetEnvironmentVariable("SECRET_NAME") ?? string.Empty,
            SecretRegion = Environment.GetEnvironmentVariable("SECRET_REGION") ?? string.Empty,
            GitHubOrganizationName = input.GitHubOrganizationName ?? Environment.GetEnvironmentVariable("GITHUB_ORGANIZATION_NAME") ?? "Virtual-Finland-Development",
            GitHubRepositoryNames = input.GitHubRepositoryNames?.Split(',')?.ToList() ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_NAMES")?.Split(',')?.ToList() ?? new List<string>()
        };

        return inputObject;
    }

    public record LambdaEventInput
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
