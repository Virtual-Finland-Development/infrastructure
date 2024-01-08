using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Aws.Iam;
using Amazon.IdentityManagement.Model;
using Amazon.IdentityManagement;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Features;

//
// IAM role for CI/CD pipelines
//
public class Deployer
{
    public static async Task<Pulumi.Aws.Iam.Role> InitializeGitHubOIDCProvider(StackSetup setup)
    {
        // GitHub OIDC provider configuration
        var githubConfig = new Config("github");
        var githubOrganization = githubConfig.Require("organization");
        var githubIssuerUrl = githubConfig.Require("oidc-issuer");
        var githubIssuerUrlWithoutProtocol = githubIssuerUrl.Replace("https://", "");
        // @see: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc_verify-thumbprint.html
        var githubThumbprints = githubConfig.RequireObject<List<string>>("oidc-thumbprints");
        var githubClientIds = new List<string> { githubConfig.Require("oidc-client-id") };

        // Create an OIDC provider for GitHub
        var openIdConnectProviderName = "github-oidc-provider"; // Using a static name as there can only be one per aws account
        var currentAwsAccount = Pulumi.Aws.GetCallerIdentity.InvokeAsync();
        var currentOidcProviderId = $"arn:aws:iam::{currentAwsAccount.Result.AccountId}:oidc-provider/{githubIssuerUrlWithoutProtocol}";

        // Check if the OIDC provider already exists
        var iamClient = new AmazonIdentityManagementServiceClient();
        var request = new ListOpenIDConnectProvidersRequest();
        var response = await iamClient.ListOpenIDConnectProvidersAsync(request);
        var existingOidcProvider = response.OpenIDConnectProviderList.Find(provider => provider.Arn == currentOidcProviderId);

        OpenIdConnectProvider? githubOidcProvider;
        if (existingOidcProvider == null)
        {
            // Create a new OIDC provider
            githubOidcProvider = new OpenIdConnectProvider(openIdConnectProviderName, new OpenIdConnectProviderArgs
            {
                Url = githubIssuerUrl,
                ClientIdLists = githubClientIds,
                ThumbprintLists = githubThumbprints,
                Tags = setup.SharedResourceTags,
            });
        }
        else
        {
            // Use the existing OIDC provider
            githubOidcProvider = OpenIdConnectProvider.Get(openIdConnectProviderName, currentOidcProviderId);
        }

        // Create an IAM role assumable by the GitHub OIDC provider
        // @see: https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect
        var githubRole = new Pulumi.Aws.Iam.Role(setup.NameResource("github-oidc-role"), new RoleArgs
        {
            Description = "Temporary admin role for GitHub Actions",
            Tags = setup.Tags,
            MaxSessionDuration = 60 * 60, // 1 hour
            AssumeRolePolicy = Output.JsonSerialize(Output.Create(new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Action = "sts:AssumeRoleWithWebIdentity",
                        Effect = "Allow",
                        Principal = new { Federated = githubOidcProvider.Arn },
                        Condition = new Dictionary<string, object>
                        {
                            { "ForAllValues:StringEquals",  new Dictionary<string, object>
                                {
                                    { $"{githubIssuerUrlWithoutProtocol}:aud", githubClientIds[0] },
                                    { $"{githubIssuerUrlWithoutProtocol}:repository_owner", githubOrganization },
                                    { $"{githubIssuerUrlWithoutProtocol}:environment", setup.Environment }
                                }
                            },
                            { "ForAllValues:StringLike",  new Dictionary<string, object>
                                {
                                    { $"{githubIssuerUrlWithoutProtocol}:sub", $"repo:{githubOrganization}/*" }
                                }
                            }
                        }
                    }
                }
            })),
        });

        // Temporary policy for updating stacks
        var githubStackUpdaterPolicy = new Policy(setup.NameResource("github-deployer-policy"), new PolicyArgs
        {
            Description = "Broad policy for updating Pulumi stacks",
            PolicyDocument = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                { "Version", "2012-10-17" },
                {
                    "Statement", new[]
                    {
                        new Dictionary<string, object?>
                        {
                            { "Sid", "GrantAdminAccess" },
                            { "Action", "*" },
                            { "Effect", "Allow" },
                            { "Resource", "*" }
                        },
                    }
                }
            }),
            Tags = setup.Tags,
        });

        // Attach policy to role
        _ = new RolePolicyAttachment(setup.NameResource("github-deployer-policy-attachment"), new()
        {
            Role = githubRole.Name,
            PolicyArn = githubStackUpdaterPolicy.Arn,
        });

        return githubRole;
    }
}