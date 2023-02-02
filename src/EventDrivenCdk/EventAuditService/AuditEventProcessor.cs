namespace EventDrivenCdk.EventAuditService;

using System.Collections.Generic;

using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;

using Constructs;

using EventDrivenCdk.SharedConstruct;

public record AuditEventProcessorProps(Table Table);

public class AuditEventProcessor : Construct
{
    public DefaultStateMachine Processor { get; private set; }
    public AuditEventProcessor(
        Construct scope,
        string id,
        AuditEventProcessorProps props) : base(
        scope,
        id)
    {
        // Simple workflow to take event bridge input and store in dynamodb.
        var sfnWorkflow = new DynamoPutItem(this, "StoreEventData", new DynamoPutItemProps()
        {
            Table = props.Table,
            Item = new Dictionary<string, DynamoAttributeValue>(3)
            {
                { "PK", DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewId")) },
                { "SK", DynamoAttributeValue.FromString(JsonPath.Format("{}#{}", JsonPath.StringAt("$.time"), JsonPath.StringAt("$.detail.type"))) },
                { "Data", DynamoAttributeValue.MapFromJsonPath("$.detail")}
            }
        });
        
        var stateMachine =
            new DefaultStateMachine(this, "EventAuditStateMachine", sfnWorkflow, StateMachineType.EXPRESS);

        this.Processor = stateMachine;
    }
}