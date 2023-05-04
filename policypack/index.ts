import * as aws from "@pulumi/aws";
import { PolicyPack, ReportViolation, validateResourceOfType } from "@pulumi/policy";

new PolicyPack("vfd-aws-policy-pack", {
  policies: [
    {
      name: "require-mandatory-tags",
      description: "Enforces the use of mandatory resource tags.",
      enforcementLevel: "mandatory",
      validateResource: [
        validateResourceOfType(aws.ec2.Instance, (resource, args, reportViolation) => {
          requireMandatoryTags(resource.tags, reportViolation);
        }),
        validateResourceOfType(aws.ec2.Vpc, (resource, args, reportViolation) => {
          requireMandatoryTags(resource.tags, reportViolation);
        }),
        validateResourceOfType(aws.lambda.Function, (resource, args, reportViolation) => {
          requireMandatoryTags(resource.tags, reportViolation);
        }),
        validateResourceOfType(aws.s3.Bucket, (resource, args, reportViolation) => {
          requireMandatoryTags(resource.tags, reportViolation);
        }),
        validateResourceOfType(aws.cloudfront.Distribution, (resource, args, reportViolation) => {
          requireMandatoryTags(resource.tags, reportViolation);
        }),
        validateResourceOfType(aws.rds.Instance, (resource, args, reportViolation) => {
          requireMandatoryTags(resource.tags, reportViolation);
        }),
      ],
    },
  ],
});

function requireMandatoryTags(tags: any, reportViolation: ReportViolation) {
  const mandatoryTags = ["Environment", "Project"];

  if (typeof tags !== "object" || tags === null) {
    reportViolation("Missing tags");
    return;
  }

  for (const tag of mandatoryTags) {
    if (!tags[tag]) {
      reportViolation(`Missing mandatory tag: ${tag}`);
    }
  }
}
