# Virtual Finland Common Infrastructure

This repostory is responsible for generating common resources for the Virtual Finland project.

The resource management is done using Infrastructure as Code tools **[Pulumi](https://www.pulumi.com/)**. This is done by using .NET and C# for the selected technology and programming language.

Presently all provisioned are done to AWS and has the following resources:

- [AWS IAM Role & OIDC Provider](./VirtualFinland.Infrastructure/Features/Deployer.cs) - CI/CD-pipeline credentials management
- [AWS SES](./Docs/README.email-setup.md) - Email sending

## Infrastructure provisioning

The provisioning happens using a combination of the Pulumi Service and Github Pulumi Actions. The process is automatic triggered by changes in the main branch.

## Organization policy

The organization policy is defined in the [./policypack](./policypack) folder. The policy is applied using the Pulumi Policy as Code tool.

Read more about the policy tool here: https://www.pulumi.com/docs/guides/crossguard/

## CI/CD pipeline authentication

For the CI/CD pipeline to be able to provision resources to AWS, it needs to be authenticated. This is done by using the OpenID Connect (OIDC) provider in AWS IAM.

Instructions for setting up the GitHub workflow can be found in the [./Docs/README-cicd-setup.md](./Docs/README-cicd-setup.md) file.

## Development guide

If new to Pulumi, then read start here: https://www.pulumi.com/docs/get-started/aws/

Other good to know documentation:  
https://www.pulumi.com/docs/intro/concepts/how-pulumi-works/  
https://www.pulumi.com/docs/intro/concepts/stack/  
https://www.pulumi.com/docs/intro/concepts/secrets/  
https://www.pulumi.com/docs/intro/concepts/config/  
https://www.pulumi.com/docs/intro/concepts/inputs-outputs/

### Pulumi basic commands

- "pulumi preview": reads you stacks and generates a preview of to be provisioned resources
- "pulumi up": Same as preview but will start to preform the actual resources provisioning (create or update) after manual acceptance
- "pulumi destroy": Will destroy the provisioned resources
- "pulumi stack select **mystackname**": Will swap to a different stack
