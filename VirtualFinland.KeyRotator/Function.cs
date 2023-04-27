using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services;

namespace VirtualFinland.KeyRotator;

public class Function
{
    public void FunctionHandler(ILambdaContext context)
    {
        var inputArgs = ParseInputArgs();
        var rotator = new AccessKeyRotator(context);
        var credentialsPublisher = new CredentialsPublisher(context);

        var newKey = rotator.RotateAccessKey(inputArgs);
        if (newKey != null)
        {
            // Publish new key to the pipelines
            credentialsPublisher.PublishAccessKey(newKey, inputArgs.Environment);
        }
    }

    InputArgs ParseInputArgs()
    {
        var inputObject = new InputArgs();


        if (string.IsNullOrEmpty(inputObject.IAMUserName))
        {
            inputObject.IAMUserName = Environment.GetEnvironmentVariable("CICD_BOT_IAM_USER_NAME") ?? string.Empty;
        }
        if (string.IsNullOrEmpty(inputObject.IAMUserName))
        {
            throw new ArgumentException("IAMUserName not defined");
        }

        if (string.IsNullOrEmpty(inputObject.Environment))
        {
            inputObject.Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? string.Empty;
        }
        if (string.IsNullOrEmpty(inputObject.Environment))
        {
            throw new ArgumentException("Environment not defined");
        }

        return inputObject;
    }
}

public record InputArgs
{
    public string IAMUserName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}
