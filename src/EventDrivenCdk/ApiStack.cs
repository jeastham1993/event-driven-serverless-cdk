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

            var sfnWorkflow = new DynamoUpdateItem(this, "GenerateCaseId", new DynamoUpdateItemProps()
            {
                Table = apiTable,
                ReturnValues = DynamoReturnValues.UPDATED_NEW,
                UpdateExpression = "set IDvalue = IDvalue + :val",
                ExpressionAttributeValues = new Dictionary<string, DynamoAttributeValue>(1)
                {
                    {":val", DynamoAttributeValue.FromNumber(1)}
                },
                Key = new Dictionary<string, DynamoAttributeValue>(1)
                {
                    {"PK", DynamoAttributeValue.FromString("reviewId")},
                },
                ResultSelector = new Dictionary<string, object>()
                {
                    {"reviewId", DynamoAttributeValue.FromString(JsonPath.StringAt("$.Attributes.IDvalue.N"))}
                },
                ResultPath = "$.reviewIdentifier",
            }).Next(new DynamoPutItem(this, "StoreApiInput", new DynamoPutItemProps()
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
                                    "reviewId",
                                    DynamoAttributeValue.FromString(JsonPath.StringAt("$.reviewIdentifier.reviewId.attributeValue.S"))
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
                            Detail = TaskInput.FromObject(new Dictionary<string, object>(1)
                            {
                                {"reviewId", JsonPath.StringAt("$.reviewIdentifier.reviewId.attributeValue.S") },
                                {"reviewIdentifier", JsonPath.StringAt("$.body.reviewIdentifier") },
                                {"emailAddress", JsonPath.StringAt("$.body.emailAddress") },
                                {"reviewContents", JsonPath.StringAt("$.body.reviewContents") }
                            }),
                            DetailType = "new-review",
                            Source = "event-driven-cdk.api",
                            EventBus = props.CentralEventBridge
                        }
                    },
                    ResultPath = "$.eventOutput",
                }))
                .Next(new Pass(this, "FormatHTTPresponse", new PassProps()
                {
                    Parameters = new Dictionary<string, object>(4)
                    {
                        {"reviewId", JsonPath.StringAt("$.reviewIdentifier.reviewId.attributeValue.S") },
                        {"reviewIdentifier", JsonPath.StringAt("$.body.reviewIdentifier") },
                        {"emailAddress", JsonPath.StringAt("$.body.emailAddress") },
                        {"reviewContents", JsonPath.StringAt("$.body.reviewContents") }
                    }
                })));

            var stateMachine = new StateMachine(this, "ApiStateMachine", new StateMachineProps
            {
                Definition = sfnWorkflow,
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