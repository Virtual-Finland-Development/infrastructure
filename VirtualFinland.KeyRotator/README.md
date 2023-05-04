# Virtual Finland Key Rotator

A Pulumi project that provisions a key rotator for AWS IAM users and roles that are used for CI/CD-pipelines.

## Description

The provisioned key rotator is an AWS Lambda function that is triggered by a CloudWatch event. The event is scheduled to trigger the Lambda function once a day. The Lambda function will then rotate the access keys for the IAM users and roles that are specified in the configuration.

The access keys are rotated by creating a new key and then first invalidating and then deleting the old one in separate schedule events -> with a scheduler frequency of one (1) day a new key is created and published every third day. The new key is published to the Github repository as an environment secret.

The target Github repositories are defined at runtime by fetching the organization repositories from the Github API. The repositories are then filtered by the ones that have a given deployment environment configured. The credentials are stored as Github secrets in the repositories deployment environment.

## Development guide

The project is build using .NET 6.0 and C#, and is using the Pulumi .NET SDK.

Build the project:

```
dotnet build ./VirtualFinland.KeyRotator
```

Execute unit tests:

```
dotnet test ./VirtualFinland.KeyRotator.UnitTests
```

Deployment (provisioning) of the infrastructure feature is done using the Pulumi CLI tool:

```
pulumi -C ./VirtualFinland.Infrastructure up
```

## Reference Documentation

The project is based on the Pulumi documentation and examples found here:

- https://www.pulumi.com/blog/managing-aws-credentials-on-cicd-part-1/
- https://www.pulumi.com/blog/managing-aws-credentials-on-cicd-part-2/
- https://www.pulumi.com/blog/managing-aws-credentials-on-cicd-part-3/
