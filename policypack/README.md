# Policy pack for AWS Config

This policy pack will create a new AWS Config rule that checks for the presence of a tag on a resource.

## Apply updates

- Update `index.ts`
- Manually update version in `package.json`
- Publish: `pulumi policy publish virtualfinland`
- Enable: `pulumi policy enable virtualfinland/vfd-aws-policy-pack latest`

## Additional resources

- [Pulumi Policy as Code](https://www.pulumi.com/docs/guides/crossguard/)
