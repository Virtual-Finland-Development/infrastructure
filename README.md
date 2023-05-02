# Virtual Finland Common Infrastructure

This repostory is responsible for generating common resources for the Virtual Finland project.

The resource management is done using Infrastructure as Code tools **[Pulumi](https://www.pulumi.com/)**. This is done by using .NET and C# for the selected technology and programming language.

Presently all provisioned are done to AWS and has the following resources:

- [AWS VPC](./VirtualFinland.Infrastructure/Stacks/VFDStack.cs) - Virtual Private Cloud for the project
- [AWS IAM users and roles, and a key rotator](./VirtualFinland.Infrastructure/Stacks/Features/KeyRotator.cs) - CI/CD-pipeline credentials management

## Infrastructure provisioning

The provisioning happens using a combination of the Pulumi Service and Github Pulumi Actions. The process is automatic triggered by changes in the main branch.

## Development guide

If new to Pulumi, then read start here: https://www.pulumi.com/docs/get-started/aws/

Other good to know documentation:  
https://www.pulumi.com/docs/intro/concepts/how-pulumi-works/  
https://www.pulumi.com/docs/intro/concepts/stack/  
https://www.pulumi.com/docs/intro/concepts/secrets/  
https://www.pulumi.com/docs/intro/concepts/config/  
https://www.pulumi.com/docs/intro/concepts/inputs-outputs/

### Project structure

- Stacks folder: Contains stacks that are to be provisioned by the Pulumi IaC tool
- Testing folder: Contains tests on the stacks that are to be provisioned
- Pulumi primary configration file: **Pulumi.yml**
- Pulumi stack configuration files: **Pulumi.\*.yml**

### Pulumi basic commands

- "pulumi preview": reads you stacks and generates a preview of to be provisioned resources
- "pulumi up": Same as preview but will start to preform the actual resources provisioning (create or update) after manual acceptance
- "pulumi destroy": Will destroy the provisioned resources
- "pulumi stack select **mystackname**": Will swap to a different stack
