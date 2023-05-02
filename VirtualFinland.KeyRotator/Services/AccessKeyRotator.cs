using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;

namespace VirtualFinland.KeyRotator.Services;

class AccessKeyRotator
{
    readonly string _iamUserName;
    ILambdaLogger _logger;

    public AccessKeyRotator(Settings settings, ILambdaLogger logger)
    {
        _iamUserName = settings.IAMUserName;
        _logger = logger;
    }

    /// <summary>
    /// Rotates the access key for the IAM user. Full cycle is 3 runs: create, invalidate old, delete old
    /// </summary>
    /// <returns>
    /// The new access key if created, otherwise null
    /// </returns>
    public AccessKey? RotateAccessKey()
    {
        AccessKey? newlyCreatedAccessKey = null;
        var iamClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient();

        // Obtain the access keys for the user
        var accessKeys = RetrieveIAMAccessKeys(iamClient);
        _logger.LogInformation($"Access keys count: {accessKeys.Count}");

        if (accessKeys.Count > 2)
        {
            // Key rotation setup does not support more than 2 keys at a time
            throw new ArgumentException("Too many keys");
        }
        else if (accessKeys.Count <= 1) // Create new key 
        {
            if (accessKeys.Count == 1)
            {
                _logger.LogInformation($"Kept the old key: {accessKeys[0].AccessKeyId}");
            }

            newlyCreatedAccessKey = iamClient.CreateAccessKeyAsync(new Amazon.IdentityManagement.Model.CreateAccessKeyRequest()
            {
                UserName = _iamUserName
            }).Result.AccessKey;
            _logger.LogInformation($"New key created: {newlyCreatedAccessKey.AccessKeyId}");
        }
        else // Invalidate or delete the oldest key
        {
            var newestKey = accessKeys.First();
            var oldestKey = accessKeys.Last();
            _logger.LogInformation($"Kept the newest key: {newestKey.AccessKeyId}");

            if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Active)
            {
                iamClient.UpdateAccessKeyAsync(new Amazon.IdentityManagement.Model.UpdateAccessKeyRequest()
                {
                    UserName = _iamUserName,
                    AccessKeyId = oldestKey.AccessKeyId,
                    Status = Amazon.IdentityManagement.StatusType.Inactive
                }).Wait();
                _logger.LogInformation($"Invalidated the oldest key: {oldestKey.AccessKeyId}");
            }
            else if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Inactive)
            {
                iamClient.DeleteAccessKeyAsync(new Amazon.IdentityManagement.Model.DeleteAccessKeyRequest()
                {
                    UserName = _iamUserName,
                    AccessKeyId = oldestKey.AccessKeyId
                }).Wait();
                _logger.LogInformation($"Deleted the oldest key: {oldestKey.AccessKeyId}");
            }
            else
            {
                throw new ArgumentException("Unknown key status");
            }
        }

        return newlyCreatedAccessKey;
    }

    /// <summary>
    /// Retrieve the access keys and sort by creation date, newest first
    /// </summary>
    List<AccessKeyMetadata> RetrieveIAMAccessKeys(Amazon.IdentityManagement.AmazonIdentityManagementServiceClient iamClient)
    {
        var accessKeys = iamClient.ListAccessKeysAsync(new Amazon.IdentityManagement.Model.ListAccessKeysRequest()
        {
            UserName = _iamUserName
        }).Result.AccessKeyMetadata.OrderByDescending(x => x.CreateDate).ToList();

        if (accessKeys == null)
        {
            throw new ArgumentException("Error in retrieving access keys");
        }

        return accessKeys;
    }
}