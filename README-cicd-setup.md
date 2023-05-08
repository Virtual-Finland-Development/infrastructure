# Example configuration for CI/CD

This example shows how to configure the github workflow CI/CD pipeline authentication for the project.

## Example

Permissions for the github workflow job:

```yaml
# These permissions are needed to interact with GitHub's OIDC Token endpoint.
permissions:
  id-token: write
  contents: read
```

Authentication steps:

```yaml
jobs:
  ...snip...
  steps:
      ...snip...
      - name: Get IAM role from Pulumi
        uses: Virtual-Finland-Development/pulumi-outputs-action@v1
        id: infra-iam-role
        with:
          organization: virtualfinland
          project: infrastructure
          stack: dev
          resource: DeployerIAMRole
          access-token: ${{ secrets.PULUMI_ACCESS_TOKEN }}
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-region: eu-north-1
          role-to-assume: ${{ steps.infra-iam-role.outputs.resource-output }}
          role-session-name: infrastructure-test
      ...snip...
```

Explanation of the steps:

- **Get IAM role from Pulumi**: This step will get the IAM role ARN from the Pulumi stack output
  - uses the [Virtual-Finland-Development/pulumi-outputs-action](https://github.com/Virtual-Finland-Development/pulumi-outputs-action) to retrieve the infrastructure stack output
- **Configure AWS credentials**: This step will configure the AWS credentials using the IAM role
  - role-to-assume: This is the IAM role ARN from the previous step
  - role-session-name: This is used to identify the session in AWS CloudTrail logs

Full example: [./.github/workflows/test.yml](./.github/workflows/test.yml)

## Related references

- https://github.com/aws-actions/configure-aws-credentials
- https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect
- https://www.pulumi.com/registry/packages/aws/api-docs/iam/openidconnectprovider/
