using System.Collections.Generic;
using System.Text.Json;
using Pulumi;
using Pulumi.Aws.CloudWatch;
using Pulumi.Aws.CloudWatch.Inputs;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.Lambda.Inputs;
using Pulumi.Aws.SecretsManager;

namespace VirtualFinland.Infrastructure.Stacks.Features;

//
// IAM user for CI/CD pipelines
//
public class KeyRotator
{
    public User InitializeCICDBotUser(string environment, InputMap<string> tags)
    {
        var botUser = new User($"cicd-bot-user-{environment}", new UserArgs()
        {
            Tags = tags,
            ForceDestroy = true
        });
        var botUserGroup = new Group($"cicd-bots-group-{environment}", new GroupArgs()
        {
        });
        var botUserGroupMembership = new GroupMembership($"cicd-bots-group-membership-{environment}", new GroupMembershipArgs()
        {
            Group = botUserGroup.Name,
            Users = new InputList<string>()
            {
                botUser.Name
            }
        });

        var currentAwsIdentity = Output.Create(Pulumi.Aws.GetCallerIdentity.InvokeAsync());
        new GroupPolicy($"cicd-bots-group-policy-{environment}", new GroupPolicyArgs()
        {
            Group = botUserGroup.Name,
            Policy = currentAwsIdentity.Apply(r => $@"{{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {{
                        ""Sid"": ""GrantRoleAccess"",
                        ""Action"": [
                            ""sts:AssumeRole""
                        ],
                        ""Effect"": ""Allow"",
                        ""Resource"": ""arn:aws:iam::{r.AccountId}:role/*""
                    }}
                ]
            }}")
        });

        return botUser;
    }

    /// <summary>
    /// Setup role and policy for updating stacks, the role is assumed by the CI/CD bot user
    /// </summary>
    /// <TODO>
    /// Would need specific policies for each stack
    /// </TODO>
    public Role InitializeStackUpdaterRoleAndPolicy(string environment, InputMap<string> tags)
    {
        var currentAwsIdentity = Output.Create(Pulumi.Aws.GetCallerIdentity.InvokeAsync());

        // Temporary role for updating stacks, the control assuming user must be tagged with the same environment
        var stackUpdaterRole = new Role($"cicd-stack-updater-role-{environment}", new RoleArgs
        {
            Description = "Broad role for updating Pulumi stacks",
            MaxSessionDuration = 30 * 60,
            AssumeRolePolicy = currentAwsIdentity.Apply(r => $@"{{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {{
                        ""Sid"": ""GrantBroadRoleAccess"",
                        ""Action"": ""sts:AssumeRole"",
                        ""Effect"": ""Allow"",
                        ""Principal"": {{
                            ""AWS"": ""arn:aws:iam::{r.AccountId}:root""
                        }},
                        ""Condition"": {{
                            ""StringEquals"": {{
                                ""aws:PrincipalTag/vfd-stack"": ""{environment}""
                            }}
                        }}
                    }}
                ]
            }}"),
            Tags = tags,
        });

        // Temporary policy for updating stacks
        var stackUpdaterPolicy = new Policy($"cicd-stack-updater-policy-{environment}", new PolicyArgs
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
        new RolePolicyAttachment($"cicd-stack-updater-policy-attachment-{environment}", new()
        {
            Role = stackUpdaterRole.Name,
            PolicyArn = stackUpdaterPolicy.Arn,
        });

        return stackUpdaterRole;
    }

    public void InitializeRotatorLambdaScheduler(User botUser, Role roleToAssume, string environment, InputMap<string> tags)
    {
        //
        // Setup roles and policies
        //
        var keyRotatorExecRole = new Role($"cicd-key-rotator-exec-role-{environment}", new RoleArgs
        {
            AssumeRolePolicy = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                { "Version", "2012-10-17" },
                {
                    "Statement", new[]
                    {
                        new Dictionary<string, object?>
                        {
                            { "Action", "sts:AssumeRole" },
                            { "Effect", "Allow" },
                            { "Sid", "" },
                            {
                                "Principal", new Dictionary<string, object?>
                                {
                                    { "Service", "lambda.amazonaws.com" }
                                }
                            }
                        }
                    }
                }
            })
        });

        var keyRotatorPolicy = new Pulumi.Aws.Iam.Policy($"cicd-key-rotator-policy-{environment}", new()
        {
            Description = "Allow full control of the IAM user's access keys.",
            PolicyDocument = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["Version"] = "2012-10-17",
                ["Statement"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["Action"] = new[]
                        {
                            "iam:CreateAccessKey",
                            "iam:DeleteAccessKey",
                            "iam:ListAccessKeys",
                            "iam:UpdateAccessKey",
                        },
                        ["Effect"] = "Allow",
                        ["Resource"] = "*",
                    },
                },
            }),
        });

        // Attach policy to role
        new RolePolicyAttachment($"cicd-key-rotator-policy-attachment-{environment}", new()
        {
            Role = keyRotatorExecRole.Name,
            PolicyArn = keyRotatorPolicy.Arn,
        });

        // Create / attach to secret manager
        var secretManagerName = $"VirtualFinland.KeyRotator-{environment}";
        var secretsManager = new Secret(secretManagerName, new()
        {
            Name = secretManagerName, // Static reference name for the lambda
            Description = "Github credentials for IAM access key updates",
            Tags = {
                { "Project", "infrastructure" },
            },
        });

        // Secrets manager policy
        var keyRotatorSecretsManagerPolicy = new Pulumi.Aws.Iam.Policy($"cicd-key-rotator-secrets-manager-policy-{environment}", new()
        {
            Description = "Read permissions to the secrets manager",
            PolicyDocument = Output.Format($@"{{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {{
                        ""Effect"": ""Allow"",
                        ""Action"": [
                            ""secretsmanager:GetSecretValue""
                        ],
                        ""Resource"": [
                            ""{secretsManager.Arn}""
                        ]
                    }}
                ]
            }}"),
        });
        new RolePolicyAttachment($"cicd-key-rotator-secrets-manager-policy-attachment-{environment}", new()
        {
            Role = keyRotatorExecRole.Name,
            PolicyArn = keyRotatorSecretsManagerPolicy.Arn,
        });

        // Attach basic execution role so that lambda can write logs
        var keyRotatorBasicExecutionPolicyAttachment = new RolePolicyAttachment($"cicd-key-rotator-basic-execution-policy-attachment-{environment}", new()
        {
            Role = keyRotatorExecRole.Name,
            PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole",
        });

        //
        // Setup lambda function and scheduler for it
        //
        var config = new Pulumi.Config("aws");
        var currentRegion = config.Require("region");
        var artifactPath = "../VirtualFinland.KeyRotator/release";
        var keyRotator = new Function($"cicd-key-rotator-{environment}", new FunctionArgs
        {
            Role = keyRotatorExecRole.Arn,
            Runtime = "dotnet6",
            Handler = "VirtualFinland.KeyRotator::VirtualFinland.KeyRotator.Function::FunctionHandler",
            Timeout = 240,
            MemorySize = 128,
            Code = new FileArchive(artifactPath),
            Tags = tags,
            Environment = new FunctionEnvironmentArgs
            {
                Variables =
                {
                    { "CICD_BOT_IAM_USER_NAME", botUser.Name },
                    { "CICD_BOT_IAM_ROLE_TO_ASSUME", roleToAssume.Arn },
                    { "ENVIRONMENT", environment },
                    { "SECRET_NAME", secretManagerName },
                    { "SECRET_REGION", currentRegion },
                }
            },
        });
        var schedulerRule = new EventRule($"cicd-key-rotator-schedule-{environment}", new EventRuleArgs
        {
            Description = "Schedule key rotations",
            ScheduleExpression = "cron(30 5 * * ? *)",
            Tags = tags,

        });
        new EventTarget($"cicd-key-rotator-target-{environment}", new EventTargetArgs
        {
            Rule = schedulerRule.Name,
            Arn = keyRotator.Arn,
            RetryPolicy = new EventTargetRetryPolicyArgs
            {
                MaximumEventAgeInSeconds = 240,
                MaximumRetryAttempts = 0
            }
        });
        new Permission($"cicd-key-rotator-permission-{environment}", new()
        {
            Action = "lambda:InvokeFunction",
            Function = keyRotator.Name,
            Principal = "events.amazonaws.com",
            SourceArn = schedulerRule.Arn,
        });

    }
}