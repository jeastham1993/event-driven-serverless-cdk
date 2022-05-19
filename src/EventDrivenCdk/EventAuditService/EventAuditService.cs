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
            var apiTable = new Table(this, "EventAuditStore", new TableProps()
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

            var sfnWorkflow = new DynamoPutItem(this, "StoreEventData", new DynamoPutItemProps()
            {
                Table = apiTable,
                Item = new Dictionary<string, DynamoAttributeValue>(3)
                {
                    { "PK", DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewId")) },
                    { "SK", DynamoAttributeValue.FromString(JsonPath.Format("{}#{}", JsonPath.StringAt("$.time"), JsonPath.StringAt("$.detail.type"))) },
                    { "Data", DynamoAttributeValue.MapFromJsonPath("$.detail")}
                }
            });

            var stateMachine =
                new DefaultStateMachine(this, "EventAuditStateMachine", sfnWorkflow, StateMachineType.EXPRESS);

            CentralEventBus.AddRule(this, "EventAuditRule",
                new string[2] {"event-driven-cdk.api", "event-driven-cdk.sentiment-analysis"}, stateMachine);
        }
    }
}