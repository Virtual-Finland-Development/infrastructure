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
    public Role InitializeGitHubOIDCProvider(string environment, InputMap<string> tags)
    {
        // GitHub OIDC provider configuration
        var githubIssuerUrl = "https://token.actions.githubusercontent.com";
        // https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc_verify-thumbprint.html
        var githubThumbprints = new List<string> { "6938FD4D98BAB03FAADB97B34396831E3780AEA1" };
        var githubOrganization = "Virtual-Finland-Development";

        // Create an OIDC provider for GitHub
        var githubOidcProvider = new OpenIdConnectProvider($"githubOidcProvider-{environment}", new OpenIdConnectProviderArgs
        {
            Url = githubIssuerUrl,
            ClientIdLists = new List<string> { "sts.amazonaws.com" },
            ThumbprintLists = githubThumbprints,
            Tags = tags
        });

        // Create an IAM role assumable by the GitHub OIDC provider
        // https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect
        var githubRole = new Role($"githubRole-{environment}", new RoleArgs
        {
            Description = "Temporary admin role for GitHub Actions",
            Tags = tags,
            MaxSessionDuration = 30 * 60,
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
                        Condition = new
                        {
                            StringEquals = new Dictionary<string, object>
                            {
                                { $"{githubIssuerUrl}:aud", "sts.amazonaws.com" },
                                { $"{githubIssuerUrl}:sub", $"repo:{githubOrganization}/*" },
                                { $"{githubIssuerUrl}:environment", $"{environment}" }
                            }
                        }
                    }
                }
            })),
        });

        // Temporary policy for updating stacks
        var githubStackUpdaterPolicy = new Policy($"githubStackUpdaterPolicy-{environment}", new PolicyArgs
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
        new RolePolicyAttachment($"githubStackUpdaterPolicyAttachment-{environment}", new()
        {
            Role = githubRole.Name,
            PolicyArn = githubStackUpdaterPolicy.Arn,
        });

        return githubRole;
    }
}