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

    public AccessKey? RotateAccessKey(Settings settings)
    {
        AccessKey? accessKey = null;
        var iamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient();

        // Obtain the access keys for the user
        var accessKeys = iamClient.ListAccessKeysAsync(new Amazon.IdentityManagement.Model.ListAccessKeysRequest()
        {
            UserName = settings.IAMUserName
        }).Result.AccessKeyMetadata.OrderByDescending(x => x.CreateDate).ToList();

        if (accessKeys == null)
        {
            throw new ArgumentException("No keys found");
        }

        // Sort keys by creation date, newest first
        _logger.LogLine($"Access keys found: {accessKeys.Count}");

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
                UserName = settings.IAMUserName
            }).Result.AccessKey;
            _logger.LogLine($"New key created: {accessKey.AccessKeyId}");
        }
        else
        {
            // Invalidate or delete the oldest key
            var newestKey = accessKeys.First();
            var oldestKey = accessKeys.Last();
            _logger.LogLine($"Kept the newest key: {newestKey.AccessKeyId}");

            if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Active)
            {
                iamClient.UpdateAccessKeyAsync(new Amazon.IdentityManagement.Model.UpdateAccessKeyRequest()
                {
                    UserName = settings.IAMUserName,
                    AccessKeyId = oldestKey.AccessKeyId,
                    Status = Amazon.IdentityManagement.StatusType.Inactive
                }).Wait();
                _logger.LogLine($"Invalidated the oldest key: {oldestKey.AccessKeyId}");
            }
            else if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Inactive)
            {
                iamClient.DeleteAccessKeyAsync(new Amazon.IdentityManagement.Model.DeleteAccessKeyRequest()
                {
                    UserName = settings.IAMUserName,
                    AccessKeyId = oldestKey.AccessKeyId
                }).Wait();
                _logger.LogLine($"Deleted the oldest key: {oldestKey.AccessKeyId}");
            }
            else
            {
                throw new ArgumentException("Unknown key status");
            }
        }

        return accessKey;
    }
}