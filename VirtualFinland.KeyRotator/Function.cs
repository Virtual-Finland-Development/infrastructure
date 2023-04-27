using System.Text.Json;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace VirtualFinland.KeyRotator;

public class Function
{
    public void FunctionHandler(string input, ILambdaContext context)
    {
        var inputArgs = ParseInputArgs(input, context);
        var rotator = new AccessKeyRotator(context);
        var credentialsPublisher = new CredentialsPublisher(context);

        var newKey = rotator.RotateAccessKey(inputArgs);
        if (newKey != null)
        {
            // Publish new key to the pipelines
            credentialsPublisher.PublishAccessKey(newKey, inputArgs.Environment);
        }
    }

    InputArgs ParseInputArgs(string input, ILambdaContext context)
    {
        var inputObject = new InputArgs();

        if (!string.IsNullOrEmpty(input))
        {
            context.Logger.LogLine($"Raw input: {input}");
            var parsedInput = JsonSerializer.Deserialize<InputArgs>(input);
            if (parsedInput != null)
            {
                inputObject = parsedInput;
            }
        }

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
