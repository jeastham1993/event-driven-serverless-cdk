using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EventDrivenCdk.SharedConstruct;

namespace EventDrivenCdk.EventAuditService
{
    public class EventAuditService : Construct
    {
        public EventAuditService(Construct scope, string id) : base(scope, id)
        {
            // Create table to store event audit data.
            var dataStore = new DataStorage(
                this,
                "EventAuditStore",
                new DataStorageProps("EventAuditStore"));

            var auditEventProcessor = new AuditEventProcessor(
                this,
                "AuditEventProcessor",
                new AuditEventProcessorProps(dataStore.Table));

            var queryProcessor = new AuditQueryProcessor(
                this,
                "QueryProcessor",
                new AuditQueryProcessorProps(dataStore.Table));

            // Add rule to the central event bus.
            MessageBus.SubscribeTo(this, 
                ruleName: "EventAuditRule",
                eventSource: new string[4] {"event-driven-cdk.api", "event-driven-cdk.sentiment-analysis", "event-driven-cdk.notifications", "event-driven-cdk.customer-service"},
                target: auditEventProcessor.Processor);

            var output = new CfnOutput(this, "AuditApiEndpoint", new CfnOutputProps()
            {
                ExportName = "AuditAPIEndpoint",
                Description = "The endpoint for the created audit API",
                Value = queryProcessor.Endpoint
            });
        }
    }
}