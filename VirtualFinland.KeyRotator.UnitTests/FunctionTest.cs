using Xunit;
using Amazon.Lambda.TestUtilities;
using Moq;
using Amazon.IdentityManagement;
using static VirtualFinland.KeyRotator.Function;
using Amazon.IdentityManagement.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Moq.Protected;
using System.Net;

namespace VirtualFinland.KeyRotator.Tests;

public class FunctionTest
{
    [Fact]
    public async Task TestLambdaFunctionCall_KeepOneCreateOne()
    {
        // Mock the IAM client
        var mockIamClient = new Mock<AmazonIdentityManagementServiceClient>();
        mockIamClient
            .Setup(mock => mock.ListAccessKeysAsync(It.IsAny<ListAccessKeysRequest>(), CancellationToken.None))
            .Returns(Task.FromResult(new ListAccessKeysResponse()
            {
                AccessKeyMetadata = new List<AccessKeyMetadata>()
                {
                    new AccessKeyMetadata()
                    {
                        AccessKeyId = "test",
                        Status = Amazon.IdentityManagement.StatusType.Active,
                        CreateDate = DateTime.Now
                    }
                }
            }));

        mockIamClient
            .Setup(mock => mock.CreateAccessKeyAsync(It.IsAny<CreateAccessKeyRequest>(), CancellationToken.None))
            .Returns(Task.FromResult(new CreateAccessKeyResponse()
            {
                AccessKey = new AccessKey()
                {
                    AccessKeyId = "testId",
                    SecretAccessKey = "testSecret",
                    Status = Amazon.IdentityManagement.StatusType.Active,
                    CreateDate = DateTime.Now,
                }
            }));

        // Mock the secrets manager client
        var mockSecretsManagerClient = new Mock<IAmazonSecretsManager>();
        mockSecretsManagerClient
            .Setup(mock => mock.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), CancellationToken.None))
            .Returns(Task.FromResult(
                new GetSecretValueResponse()
                {
                    SecretString = "{\"CICD_BOT_GITHUB_ACCESS_TOKEN\": \"test\"}"
                }
            ));

        // Mock the http client factory
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            ).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[{\"id\": 12345679, \"name\": \"test-repository\"}]"),
            }).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]"),
            }).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"id\": 98765432, \"name\": \"test-environment\"}"),
            }).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"key_id\": \"98-76-54-32\", \"key\": \"dGVzdC1wdWJsaWMta2V5LXRpcy0zMi1ieXRlcy1sZW4=\"}"), // base64 encoded: test-public-key-tis-32-bytes-len
            }).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
            }).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"key_id\": \"98-76-54-32\", \"key\": \"dGVzdC1wdWJsaWMta2V5LXRpcy0zMi1ieXRlcy1sZW4=\"}"), // base64 encoded: test-public-key-tis-32-bytes-len
            }).ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(mock => mock.CreateClient(It.IsAny<string>())).Returns(new HttpClient(httpMessageHandlerMock.Object));

        // Create the function and run it
        var function = new Function(mockIamClient.Object, mockSecretsManagerClient.Object, mockHttpClientFactory.Object);
        var context = new TestLambdaContext();
        var requestInput = new LambdaEventInput()
        {
            Environment = "test-environment",
            GitHubOrganizationName = "test-organization",
            GitHubRepositoryNames = "test-repository",
        };


        await function.FunctionHandler(requestInput, context);
    }
}