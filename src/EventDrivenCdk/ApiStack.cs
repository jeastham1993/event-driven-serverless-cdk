using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace EventDrivenCdk
{
    public class ApiStackProps
    {
        public EventBus CentralEventBridge { get; set; }
    }

    public class ApiStack : Construct
    {
        public ApiStack(Construct scope, string id, ApiStackProps props) : base(scope, id)
        {
            var apiTable = new Table(this, "StorageFirstInput", new TableProps()
            {
                TableName = "EventDrivenCDKApiStore",
                PartitionKey = new Attribute()
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            var putItem = new DynamoPutItem(this, "StoreApiInput", new DynamoPutItemProps()
                {
                    Table = apiTable,
                    ResultPath = "$.output",
                    Item = new Dictionary<string, DynamoAttributeValue>(1)
                    {
                        {"PK", DynamoAttributeValue.FromString(JsonPath.StringAt("$.body.reviewIdentifier"))},
                        {
                            "Data", DynamoAttributeValue.FromMap(new Dictionary<string, DynamoAttributeValue>(3)
                            {
                                {
                                    "reviewIdentifier",
                                    DynamoAttributeValue.FromString(JsonPath.StringAt("$.body.reviewIdentifier"))
                                },
                                {
                                    "emailAddress",
                                    DynamoAttributeValue.FromString(JsonPath.StringAt("$.body.emailAddress"))
                                },
                                {
                                    "reviewContents",
                                    DynamoAttributeValue.FromString(JsonPath.StringAt("$.body.reviewContents"))
                                },
                            })
                        }
                    },
                })
                .Next(new EventBridgePutEvents(this, "PublishEvent", new EventBridgePutEventsProps()
                {
                    Entries = new EventBridgePutEventsEntry[1]
                    {
                        new EventBridgePutEventsEntry
                        {
                            Detail = TaskInput.FromJsonPathAt("$.body"),
                            DetailType = "new-review",
                            Source = "event-driven-cdk.api",
                            EventBus = props.CentralEventBridge
                        }
                    }
                }));

            var stateMachine = new StateMachine(this, "ApiStateMachine", new StateMachineProps
            {
                Definition = putItem,
                StateMachineType = StateMachineType.EXPRESS,
                Logs = new LogOptions()
                {
                    Destination = new LogGroup(this, "StepFunctionsLogGroup", new LogGroupProps()
                    {
                        Retention = RetentionDays.ONE_DAY,
                        LogGroupName = "ApiStateMachineLogGroups"
                    }),
                    Level = LogLevel.ALL
                },
                TracingEnabled = true
            });
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

            var api = new StepFunctionsRestApi(this, "StepFunctionsRestApi", new StepFunctionsRestApiProps
            {
                StateMachine = stateMachine,
            });
        }
    }
}