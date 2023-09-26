using System.Collections.Generic;
using System.Text.Json;
using Pulumi;
using Pulumi.Aws.Iam;

namespace VirtualFinland.Infrastructure.Stacks.Features;

//
// IAM role for CI/CD pipelines
//
public class Deployer
{
    public Role InitializeGitHubOIDCProvider(string environment, Dictionary<string, string> tags, Dictionary<string, string> sharedResourceTags)
    {
        // GitHub OIDC provider configuration
        var githubConfig = new Config("github");
        var githubOrganization = githubConfig.Require("organization");
        var githubIssuerUrl = githubConfig.Require("oidc-issuer");
        var githubIssuerUrlWithoutProtocol = githubIssuerUrl.Replace("https://", "");
        // @see: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc_verify-thumbprint.html
        var githubThumbprints = (githubConfig.Get("oidc-thumbprints") ?? throw new KeyNotFoundException("Missing setting: oidc-thumbprints")).Split(",");
        var githubClientIds = new List<string> { githubConfig.Require("oidc-client-id") };

        // Create an OIDC provider for GitHub
        var openIdConnectProviderName = "github-oidc-provider";
        var currentAwsAccount = Pulumi.Aws.GetCallerIdentity.InvokeAsync();
        var currentOidcProviderId = $"arn:aws:iam::{currentAwsAccount.Result.AccountId}:oidc-provider/{githubIssuerUrlWithoutProtocol}";
        var existingOidcProvider = GetOpenidConnectProvider.InvokeAsync(new GetOpenidConnectProviderArgs
        {
            Url = githubIssuerUrl,
        });


        // Resolve existing resource with advice from issue: https://github.com/pulumi/pulumi/issues/3364#issuecomment-1267034580
        OpenIdConnectProvider? githubOidcProvider;
        if (existingOidcProvider == null)
        {
            githubOidcProvider = new OpenIdConnectProvider(openIdConnectProviderName, new OpenIdConnectProviderArgs
            {
                Url = githubIssuerUrl,
                ClientIdLists = githubClientIds,
                ThumbprintLists = githubThumbprints,
                Tags = sharedResourceTags,
            });
        }
        else
        {
            githubOidcProvider = OpenIdConnectProvider.Get(openIdConnectProviderName, currentOidcProviderId);
        }

        // Create an IAM role assumable by the GitHub OIDC provider
        // @see: https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect
        var githubRole = new Role($"github-oidc-role-{environment}", new RoleArgs
        {
            Description = "Temporary admin role for GitHub Actions",
            Tags = tags,
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
                                    { $"{githubIssuerUrlWithoutProtocol}:environment", environment }
                                }
                            },
                            { "ForAllValues:StringLike",  new Dictionary<string, object>
                                {
                                    { $"{githubIssuerUrlWithoutProtocol}:sub", $"repo:{githubOrganization}/*:environment:{environment}" }
                                }
                            }
                        }
                    }
                }
            })),
        });

        // Temporary policy for updating stacks
        var githubStackUpdaterPolicy = new Policy($"github-deployer-policy-{environment}", new PolicyArgs
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
            Tags = tags,
        });

        // Attach policy to role
        new RolePolicyAttachment($"github-deployer-policy-attachment-{environment}", new()
        {
            Role = githubRole.Name,
            PolicyArn = githubStackUpdaterPolicy.Arn,
        });

        return githubRole;
    }
}