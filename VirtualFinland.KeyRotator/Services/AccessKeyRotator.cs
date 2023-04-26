using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;

namespace VirtualFinland.KeyRotator.Services;

class AccessKeyRotator
{
    ILambdaLogger _logger;
    public AccessKeyRotator(ILambdaContext context)
    {
        _logger = context.Logger;
    }

    public AccessKey? RotateAccessKey(InputArgs inputObject)
    {
        AccessKey? accessKey = null;
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
        _logger.LogLine($"Acces keys count: {accessKeys.Count}");
        accessKeys.ForEach(x => _logger.LogLine($"Key: {x.AccessKeyId}"));

        if (accessKeys.Count > 2)
        {
            // Key rotation setup does not support more than 2 keys at a time
            throw new ArgumentException("Too many keys");
        }
        else if (accessKeys.Count <= 1)
        {
            // Release new key 
            accessKey = iamClient.CreateAccessKeyAsync(new Amazon.IdentityManagement.Model.CreateAccessKeyRequest()
            {
                UserName = inputObject.IAMUserName
            }).Result.AccessKey;
            _logger.LogLine($"New key created: {accessKey.AccessKeyId}");
        }
        else
        {
            // Invalidate or delete the oldest key
            var oldestKey = accessKeys.Last();
            _logger.LogLine($"Oldest key: {oldestKey.AccessKeyId}");

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
        _logger.LogLine($"Key rotation completed.");

        return accessKey;
    }
}