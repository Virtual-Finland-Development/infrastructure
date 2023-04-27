using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services;

namespace VirtualFinland.KeyRotator;

public class Function
{
    public async Task FunctionHandler(ILambdaContext context)
    {
        var settings = GetSettings();
        var rotator = new AccessKeyRotator(context);
        var credentialsPublisher = new CredentialsPublisher(settings, context);

        var newKey = rotator.RotateAccessKey(settings);
        if (newKey != null)
        {
            // Publish new key to the pipelines
            await credentialsPublisher.PublishAccessKey(newKey);
        }
        context.Logger.LogInformation("Key rotations completed");
    }

    Settings GetSettings()
    {
        var inputObject = new Settings()
        {
            IAMUserName = Environment.GetEnvironmentVariable("CICD_BOT_IAM_USER_NAME") ?? string.Empty,
            Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? string.Empty,
            SecretName = Environment.GetEnvironmentVariable("SECRET_NAME") ?? string.Empty,
            SecretRegion = Environment.GetEnvironmentVariable("SECRET_REGION") ?? string.Empty
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
}
