using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Amazon.CDK.CustomResources;
using Constructs;
using EventDrivenCdk.SharedConstruct;

namespace EventDrivenCdk.Frontend
{
    public class FrontendApiServiceProps
    {
        public EventBus CentralEventBridge { get; set; }
    }

    public class FrontendApiService : Construct
    {
        public FrontendApiService(Construct scope, string id, FrontendApiServiceProps props) : base(scope, id)
        {
            // Define the table to support the storage first API pattern.
            var apiTable = new Table(scope, "StorageFirstInput", new TableProps()
            {
                TableName = "EventDrivenCDKApiStore",
                PartitionKey = new Attribute()
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            var apiEventPublishing = WorkflowStep.StoreApiData(this, apiTable)
                // Publish the new request event
                .Next(WorkflowStep.PublishNewApiRequestEvent(this, props.CentralEventBridge))
                // Format the HTTP response to return to the front end
                .Next(WorkflowStep.FormatStateForHttpResponse(this));

            // Define the business workflow to integrate with the HTTP request, generate the case id
            // store and publish.
            var stateMachine = new DefaultStateMachine(this, "ApiStateMachine",
                // Generate a case id that can be returned to the frontend
                WorkflowStep.GenerateCaseId(this, apiTable, "CaseId")
                    .AddCatch(WorkflowStep.HandleMissingReviewIdError(this, apiTable, apiEventPublishing), new CatchProps()
                    {
                        Errors = new string[1] {"DynamoDB.AmazonDynamoDBException"},
                        ResultPath = "$.errorDetails"
                    })
                // Store the API data
                .Next(apiEventPublishing), StateMachineType.EXPRESS);
            
            stateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[1] {"dynamodb:PutItem"},
                Resources = new string[1] {apiTable.TableArn}
            }));
            stateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[1] {"events:PutEvents"},
                Resources = new string[1] {props.CentralEventBridge.EventBusArn}
            }));

            // Create the API
            var api = new StepFunctionsRestApi(this, "StepFunctionsRestApi", new StepFunctionsRestApiProps
            {
                StateMachine = stateMachine,
            });

            var output = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps()
            {
                ExportName = "APIEndpoint",
                Description = "The endpoint for the created API",
                Value = api.Url
            });
        }
    }
}