using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;

namespace VirtualFinland.KeyRotator.Services;

class AccessKeyRotator
{
    readonly string _iamUserName;
    ILambdaLogger _logger;
    AmazonIdentityManagementServiceClient _iamClient;

    public AccessKeyRotator(AmazonIdentityManagementServiceClient iamClient, Settings settings, ILambdaLogger logger)
    {
        _iamClient = iamClient;
        _iamUserName = settings.IAMUserName;
        _logger = logger;
    }

    /// <summary>
    /// Rotates the access key for the IAM user. Full cycle is 3 runs: create, invalidate old, delete old
    /// </summary>
    /// <returns>
    /// The new access key if created, otherwise null
    /// </returns>
    public async Task<AccessKey?> RotateAccessKey()
    {
        AccessKey? newlyCreatedAccessKey = null;

        // Obtain the access keys for the user
        var accessKeys = await RetrieveIAMAccessKeys(_iamClient);
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

            var result = await _iamClient.CreateAccessKeyAsync(new CreateAccessKeyRequest()
            {
                UserName = _iamUserName
            });

            newlyCreatedAccessKey = result.AccessKey;
            _logger.LogInformation($"New key created: {newlyCreatedAccessKey.AccessKeyId}");
        }
        else // Invalidate or delete the oldest key
        {
            var newestKey = accessKeys.First();
            var oldestKey = accessKeys.Last();
            _logger.LogInformation($"Kept the newest key: {newestKey.AccessKeyId}");

            if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Active)
            {
                await _iamClient.UpdateAccessKeyAsync(new UpdateAccessKeyRequest()
                {
                    UserName = _iamUserName,
                    AccessKeyId = oldestKey.AccessKeyId,
                    Status = Amazon.IdentityManagement.StatusType.Inactive
                });
                _logger.LogInformation($"Invalidated the oldest key: {oldestKey.AccessKeyId}");
            }
            else if (oldestKey.Status == Amazon.IdentityManagement.StatusType.Inactive)
            {
                await _iamClient.DeleteAccessKeyAsync(new DeleteAccessKeyRequest()
                {
                    UserName = _iamUserName,
                    AccessKeyId = oldestKey.AccessKeyId
                });
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
    async Task<List<AccessKeyMetadata>> RetrieveIAMAccessKeys(AmazonIdentityManagementServiceClient iamClient)
    {
        var response = await iamClient.ListAccessKeysAsync(new ListAccessKeysRequest()
        {
            UserName = _iamUserName
        });

        var accessKeys = response?.AccessKeyMetadata.OrderByDescending(x => x.CreateDate).ToList() ?? throw new ArgumentException("Error in retrieving access keys");

        return accessKeys;
    }
}