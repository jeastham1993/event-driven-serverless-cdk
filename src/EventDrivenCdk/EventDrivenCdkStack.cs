using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.AutoScaling;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SAM;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Constructs;
using EventDrivenCdk.CustomerContactService;
using EventDrivenCdk.NotificationService;
using EventDrivenCdk.ReviewAnalysisService;
using Newtonsoft.Json.Linq;

namespace EventDrivenCdk
{
    public class EventDrivenCdkStack : Stack
    {
        internal EventDrivenCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var sharedStack = new SharedResources(this, "SharedResources");

            var api = new ApiStack(this, "ApiStack", new ApiStackProps()
            {
                CentralEventBridge = sharedStack.CentralEventBus
            });
            
            var sentimentAnalysis = new ReviewAnalysisService.ReviewAnalysisService(this, "SentimentAnalysis", new ReviewAnalysisServiceProps()
            {
                CentralEventBus = sharedStack.CentralEventBus
            });

            var eventAuditor = new EventAuditService(this, "EventAuditService", new EventAuditServiceProps()
            {
                CentralEventBus = sharedStack.CentralEventBus
            });

            var notificationService = new NotificationService.NotificationService(this, "NotificationService",
                new NotificationServiceProps()
                {
                    CentralEventBus = sharedStack.CentralEventBus
                });

            var customerContactService = new CustomerContactService.CustomerContactService(this,
                "CustomerContactService", new CustomerContactServiceProps()
                {
                    CentralEventBus = sharedStack.CentralEventBus
                });

            // var orderIdTable = new Table(this, "OrderIdTable", new TableProps()
            // {
            //     PartitionKey = new Attribute()
            //     {
            //         Name = "PK",
            //         Type = AttributeType.STRING
            //     },
            //     BillingMode = BillingMode.PAY_PER_REQUEST
            // });
            //
            // var stepFunctionRole = new Role(this, "Step Function Role", new RoleProps()
            // {
            //     AssumedBy = new ServicePrincipal("states.amazonaws.com"),
            //     InlinePolicies = new Dictionary<string, PolicyDocument>(1)
            //     {
            //         {
            //             "OrderStateMachinePolicy", new PolicyDocument(new PolicyDocumentProps()
            //             {
            //                 Statements = new PolicyStatement[]
            //                 {
            //                     new PolicyStatement(new PolicyStatementProps()
            //                     {
            //                         Actions = new[]
            //                         {
            //                             "sns:Publish"
            //                         },
            //                         Resources = new[]
            //                         {
            //                             snsTopic.TopicArn
            //                         }
            //                     }),
            //                     new PolicyStatement(new PolicyStatementProps()
            //                     {
            //                         Actions = new[]
            //                         {
            //                             "dynamodb:PutItem",
            //                             "dynamodb:UpdateItem",
            //                             "dynamodb:BatchWriteItem"
            //                         },
            //                         Resources = new[]
            //                         {
            //                             orderIdTable.TableArn,
            //                             $"{orderIdTable.TableArn}/index/*"
            //                         }
            //                     })
            //                 }
            //             })
            //         }
            //     }
            // });
            //
            // var stepFunction = new CfnStateMachine(this, "TestStateMachine", new CfnStateMachineProps()
            // {
            //     Name = "TestStateMachine",
            //     Type = "STANDARD",
            //     Definition = JObject.Parse(File.ReadAllText("./src/EventDrivenCDK/statemachine/asl.json")),
            //     DefinitionSubstitutions = new Dictionary<string, string>(2)
            //     {
            //         {"SNS_TOPIC", snsTopic.TopicArn},
            //         {"TABLE_NAME", orderIdTable.TableName}
            //     },
            //     Role = stepFunctionRole.RoleArn
            // });
        }
    }
}