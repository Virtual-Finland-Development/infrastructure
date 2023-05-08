# Example configuration for CI/CD

This example shows how to configure the github workflow CI/CD pipeline authentication for the project.

Example workflow file: [./.github/workflows/test.yml](./.github/workflows/test.yml)

Relevant parts of the workflow file:

Permissions for the github workflow job:

```yaml
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
          aws-region: ${{ secrets.AWS_REGION }}
          role-to-assume: ${{ steps.infra-iam-role.outputs.resource-output }}
          role-session-name: infrastructure-test
      ...snip...
```

Explanation of the steps:

- Get IAM role from Pulumi: This step will get the IAM role ARN from the Pulumi stack output
- Configure AWS credentials: This step will configure the AWS credentials for the AWS CLI to use the IAM role from the previous step

## Related references

- https://github.com/aws-actions/configure-aws-credentials
- https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect
- https://www.pulumi.com/registry/packages/aws/api-docs/iam/openidconnectprovider/
