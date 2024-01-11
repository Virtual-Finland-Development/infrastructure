# AWS Simple Email Service (SES) - Email Sending Service

The SES is deployed and managed in the infrastructure project so all Virtual Finland projects can easily send emails from organization domain using SES client.

## Deployment dependencies

The SES depends on a domain zone being registered in AWS Route53. Currently\* the domain zone is managed by the [Access Finland MVP](https://github.com/Virtual-Finland-Development/access-finland) project and thus there's a circular dependecy between the infrastructure and access-finland projects: the access-finland requires infrastructure to be deployed first and the infrastructure requires access-finland to be deployed before the SES can be deployed.

There's also a dependency between the different deployment stacks in the project. For example when the domain zone is created in the `mvp-staging` stack, but the `dev` stack also needs to send emails, then the `dev` infrastructure-stack needs to be deployed after the `mvp-staging` stack. 

\* The setup would be easier if the domain zone would be managed in the infrastructure project, but as the zone is already in use, it's maybe not worth the effort to migrate it to the infrastructure project.

## Deployment configuration

The principal settings for the email sending service are defined in the pulumi stack configuration (eg. Pulumi.yml, Pulumi.dev.yml etc.) files under the namespace `ses`. 

Available settings are:

- `ses:domain-name`: The domain name used for sending emails. The domain name must be registered in AWS Route53.
- `ses:create-domain-records`: Boolean value for creating the domain verification records in AWS Route53. If set to false, the domain verification records must be created manually or by some other stack (which owns the domain).
- `ses:also-create-domain-records-for`: List of tuples of stack and domain names that rely on the current stack for domain records management.
  - the syntax for the list item is `stack-name:domain-name`
- `ses:domain-zone-id`: The AWS Route53 zone id for the domain name. If not set, the zone id is fetched from the access-finland projects pulumi outputs.

## Post-deployment steps

After the SES stack has been deployed, the domain name must be verified by AWS. The verification records should have been created automatically by the stack, but it might take up to 72 hours for the verification to be completed. After the verification, the SES should be upgraded to production use by requesting a production access from AWS. The production access request can be done from the [AWS SES console](https://console.aws.amazon.com/ses/).