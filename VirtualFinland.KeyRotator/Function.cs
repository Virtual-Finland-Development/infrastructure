using System.Text.Json;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace VirtualFinland.KeyRotator;

public class Function
{
    public void FunctionHandler(string input, ILambdaContext context)
    {
        context.Logger.LogLine($"Raw input: {input}");
        var inputObject = JsonSerializer.Deserialize<InputArgs>(input);
        if (inputObject == null)
        {
            throw new ArgumentException("Invalid input");
        }

        var iamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient();

        // Obtain the access keys for the user
        var accessKeys = iamClient.ListAccessKeysAsync(new Amazon.IdentityManagement.Model.ListAccessKeysRequest()
        {
            UserName = inputObject.IAMUserName
        }).Result.AccessKeyMetadata.OrderByDescending(x => x.CreateDate).ToList();

        if (accessKeys == null)
        {
            throw new ArgumentException("No keys found");
        }

        // Sort keys by creation date, newest first
        context.Logger.LogLine($"Acces keys count: {accessKeys.Count}");
        accessKeys.ForEach(x => context.Logger.LogLine($"Key: {x.AccessKeyId}"));

        if (accessKeys.Count > 2)
        {
            // Key rotation setup does not support more than 2 keys at a time
            throw new ArgumentException("Too many keys");
        }
        else if (accessKeys.Count <= 1)
        {
            // Release new key 
            var newKey = iamClient.CreateAccessKeyAsync(new Amazon.IdentityManagement.Model.CreateAccessKeyRequest()
            {
                UserName = inputObject.IAMUserName
            }).Result.AccessKey;
            context.Logger.LogLine($"New key created: {newKey.AccessKeyId}");

            // Publish new key to the key channels
            // @TODO
        }
        else
        {
            // Invalidate or delete the oldest key
            var oldestKey = accessKeys.Last();
            context.Logger.LogLine($"Oldest key: {oldestKey.AccessKeyId}");

            if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Active)
            {
                // Invalidate the key
                iamClient.UpdateAccessKeyAsync(new Amazon.IdentityManagement.Model.UpdateAccessKeyRequest()
                {
                    UserName = inputObject.IAMUserName,
                    AccessKeyId = oldestKey.AccessKeyId,
                    Status = Amazon.IdentityManagement.StatusType.Inactive
                }).Wait();
            }
            else if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Inactive)
            {
                // Delete the key
                iamClient.DeleteAccessKeyAsync(new Amazon.IdentityManagement.Model.DeleteAccessKeyRequest()
                {
                    UserName = inputObject.IAMUserName,
                    AccessKeyId = oldestKey.AccessKeyId
                }).Wait();
            }
            else
            {
                throw new ArgumentException("Unknown key status");
            }
        }
        context.Logger.LogLine($"Key rotation completed.");
    }
}

public record InputArgs
{
    public string IAMUserName { get; init; } = "";
}