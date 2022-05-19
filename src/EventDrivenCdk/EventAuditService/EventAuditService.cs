using System.Collections.Generic;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EventDrivenCdk.SharedConstruct;
using EventBus = Amazon.CDK.AWS.Events.EventBus;

namespace EventDrivenCdk.EventAuditService
{
    public class EventAuditService : Construct
    {
        public EventAuditService(Construct scope, string id) : base(scope, id)
        {
            // Create table to strore event audit data.
            var auditTable = new Table(this, "EventAuditStore", new TableProps()
            {
                TableName = "EventAuditStore",
                PartitionKey = new Attribute()
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute()
                {
                    Name = "SK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            // Simple workflow to take event bridge input and store in dynamodb.
            var sfnWorkflow = new DynamoPutItem(this, "StoreEventData", new DynamoPutItemProps()
            {
                Table = auditTable,
                Item = new Dictionary<string, DynamoAttributeValue>(3)
                {
                    { "PK", DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewId")) },
                    { "SK", DynamoAttributeValue.FromString(JsonPath.Format("{}#{}", JsonPath.StringAt("$.time"), JsonPath.StringAt("$.detail.type"))) },
                    { "Data", DynamoAttributeValue.MapFromJsonPath("$.detail")}
                }
            });

            var stateMachine =
                new DefaultStateMachine(this, "EventAuditStateMachine", sfnWorkflow, StateMachineType.EXPRESS);

            // Add rule to the central event bus.
            CentralEventBus.AddRule(this, "EventAuditRule",
                new string[2] {"event-driven-cdk.api", "event-driven-cdk.sentiment-analysis"}, stateMachine);
        }
    }
}