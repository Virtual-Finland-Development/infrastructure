# AWS Simple Email Service (SES) - Email Sending Service

The SES is deployed and managed in the infrastructure project so all Virtual Finland projects can easily send emails from organization domain using SES client.

## Deployment dependencies

The SES depends on a domain zone being registered in AWS Route53. Currently\* the domain zone is managed by the [Access Finland MVP](https://github.com/Virtual-Finland-Development/access-finland) project and thus there's a circular dependecy between the infrastructure and access-finland projects: the access-finland requires infrastructure to be deployed first and the infrastructure requires access-finland to be deployed before the SES can be deployed.

There's also a dependency between the different deployment stacks in the project. For example when the domain zone is created in the `mvp-staging` stack, but the `dev` stack also needs to send emails, then the `dev` infrastructure-stack needs to be deployed after the `mvp-staging` stack. 

\* The setup would be easier if the domain zone would be managed in the infrastructure project, but it as the zone is already in use it's to be planned if it's worth the effort to migrate it to the infrastructure project.

## Deployment configuration

The principal settings for the email sending service are defined in the pulumi stack configuration (eg. Pulumi.yml, Pulumi.dev.yml etc.) files under the namespace `ses`. 

Available settings are:

- `ses:domain-name`: The domain name used for sending emails. The domain name must be registered in AWS Route53.
- `ses:domain-owned-by-stack`: The stack name that owns the domain name. The stack must be deployed before the SES stack (or the with the same deployment when applicable).
- `ses:this-stack-owns-other-domains`: List of tuples of stack and domain names that rely on the current stack for domain ownership.
  - the syntax for the list item is `stack-name:domain-name`
