namespace EventDrivenCdk.Frontend;

using System.Collections.Generic;

using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;

using Constructs;

using EventDrivenCdk.SharedConstruct;

using Environment = System.Environment;

public class FrontendApiServicePropsImprovedProps
{
    public EventBus CentralEventBridge { get; set; }
}

public class FrontendApiServicePropsImproved : Construct
{
    public FrontendApiServicePropsImproved(
        Construct scope,
        string id,
        FrontendApiServicePropsImprovedProps props) : base(
        scope,
        id)
    {
        // Define the table to support the storage first API pattern.
        var apiTable = new Table(
            scope,
            "StorageFirstInput",
            new TableProps
            {
                TableName = "EventDrivenCDKApiStore",
                PartitionKey = new Attribute
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

        var integrationRole = new Role(
            this,
            "SqsApiGatewayIntegrationRole",
            new RoleProps
            {
                AssumedBy = new ServicePrincipal("apigateway.amazonaws.com")
            });

        var queue = new Queue(
            this,
            "FrontendQueue",
            new QueueProps
            {
                Encryption = QueueEncryption.UNENCRYPTED
            });

        queue.GrantSendMessages(integrationRole);

        var integration = new AwsIntegration(
            new AwsIntegrationProps
            {
                Service = "sqs",
                Path = $"{Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT")}/{queue.QueueName}",
                IntegrationHttpMethod = "POST",
                Options = new IntegrationOptions
                {
                    CredentialsRole = integrationRole,
                    RequestParameters = new Dictionary<string, string>(1)
                    {
                        { "integration.request.header.Content-Type", "'application/x-www-form-urlencoded'" }
                    },
                    RequestTemplates = new Dictionary<string, string>(1)
                    {
                        { "application/json", "Action=SendMessage&MessageBody=$input.body" }
                    },
                    IntegrationResponses = new List<IIntegrationResponse>(3)
                    {
                        new IntegrationResponse
                        {
                            StatusCode = "200"
                        },
                        new IntegrationResponse
                        {
                            StatusCode = "400"
                        },
                        new IntegrationResponse
                        {
                            StatusCode = "500"
                        },
                    }.ToArray()
                }
            });

        var frontendApi = new RestApi(
            this,
            "FrontendApi",
            new RestApiProps());

        frontendApi.Root.AddMethod(
            "POST",
            integration,
            new MethodOptions
            {
                MethodResponses = new[]
                {
                    new MethodResponse { StatusCode = "200" },
                    new MethodResponse { StatusCode = "400" },
                    new MethodResponse { StatusCode = "500" }
                }
            });

        // Define the business workflow to integrate with the HTTP request, generate the case id
        // store and publish.
        // Abstract the complexities of each Workflow Step behind a method call of legibility
        var stepFunction = new DefaultStateMachine(
            this,
            "ApiStateMachine",
            new Map(
                this,
                "LoopInputRecords",
                new MapProps
                {
                    InputPath = JsonPath.EntirePayload
                }).Iterator(
                new Pass(
                        this,
                        "ParseSQSInput",
                        new PassProps
                        {
                            Parameters = new Dictionary<string, object>(1)
                            {
                                { "parsed.$", "States.StringToJson($.body)" }
                            },
                            OutputPath = JsonPath.StringAt("$.parsed")
                        }).Next(
                        // Generate a case id that can be returned to the frontend
                        new Pass(
                            scope,
                            "GenerateCaseId",
                            new PassProps
                            {
                                Parameters = new Dictionary<string, object>(4)
                                {
                                    { "payload", JsonPath.EntirePayload },
                                    { "uuid.$", "States.UUID()" },
                                }
                            }))
                    // Store the API data
                    .Next(
                        new DynamoPutItem(
                                scope,
                                "StoreApiInput",
                                new DynamoPutItemProps
                                {
                                    Table = apiTable,
                                    ResultPath = "$.output",
                                    Item = new Dictionary<string, DynamoAttributeValue>(1)
                                    {
                                        { "PK", DynamoAttributeValue.FromString(JsonPath.StringAt("$.uuid")) },
                                        {
                                            "Data", DynamoAttributeValue.FromMap(
                                                new Dictionary<string, DynamoAttributeValue>(3)
                                                {
                                                    {
                                                        "reviewIdentifier",
                                                        DynamoAttributeValue.FromString(JsonPath.StringAt("$.uuid"))
                                                    },
                                                    {
                                                        "reviewId",
                                                        DynamoAttributeValue.FromString(
                                                            JsonPath.StringAt("$.uuid"))
                                                    },
                                                    {
                                                        "emailAddress",
                                                        DynamoAttributeValue.FromString(
                                                            JsonPath.StringAt("$.payload.emailAddress"))
                                                    },
                                                    {
                                                        "reviewContents",
                                                        DynamoAttributeValue.FromString(
                                                            JsonPath.StringAt("$.payload.reviewContents"))
                                                    },
                                                })
                                        }
                                    },
                                })
                            // Publish the new request event
                            .Next(
                                new EventBridgePutEvents(
                                    scope,
                                    "PublishEvent",
                                    new EventBridgePutEventsProps
                                    {
                                        Entries = new EventBridgePutEventsEntry[1]
                                        {
                                            new EventBridgePutEventsEntry
                                            {
                                                Detail = TaskInput.FromObject(
                                                    new Dictionary<string, object>(1)
                                                    {
                                                        { "reviewId", JsonPath.StringAt("$.uuid") },
                                                        { "reviewIdentifier", JsonPath.StringAt("$.uuid") },
                                                        { "emailAddress", JsonPath.StringAt("$.payload.emailAddress") },
                                                        {
                                                            "reviewContents",
                                                            JsonPath.StringAt("$.payload.reviewContents")
                                                        },
                                                        { "type", "newReview" },
                                                    }),
                                                DetailType = "newReview",
                                                Source = "event-driven-cdk.api",
                                                EventBus = props.CentralEventBridge
                                            }
                                        },
                                        ResultPath = "$.eventOutput",
                                    }))
                            // Format the HTTP response to return to the front end
                            .Next(
                                new Pass(
                                    scope,
                                    "FormatHTTPresponse",
                                    new PassProps
                                    {
                                        Parameters = new Dictionary<string, object>(4)
                                        {
                                            { "reviewId", JsonPath.StringAt("$.uuid") },
                                            { "reviewIdentifier", JsonPath.StringAt("$.uuid") },
                                            { "emailAddress", JsonPath.StringAt("$.payload.emailAddress") },
                                            { "reviewContents", JsonPath.StringAt("$.payload.reviewContents") }
                                        }
                                    })))),
            StateMachineType.EXPRESS);

        apiTable.GrantReadWriteData(stepFunction);

        var sourcePolicy = new PolicyDocument(
            new PolicyDocumentProps
            {
                Statements = new[]
                {
                    new PolicyStatement(
                        new PolicyStatementProps
                        {
                            Resources = new[] { queue.QueueArn },
                            Actions = new[] { "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes" },
                            Effect = Effect.ALLOW
                        })
                }
            });

        var targetPolicy = new PolicyDocument(
            new PolicyDocumentProps
            {
                Statements = new[]
                {
                    new PolicyStatement(
                        new PolicyStatementProps
                        {
                            Resources = new[] { stepFunction.StateMachineArn },
                            Actions = new[] { "states:StartExecution" },
                            Effect = Effect.ALLOW
                        })
                }
            });

        var pipeRole = new Role(
            this,
            "PipeRole",
            new RoleProps
            {
                AssumedBy = new ServicePrincipal("pipes.amazonaws.com"),
                InlinePolicies = new Dictionary<string, PolicyDocument>(2)
                {
                    { "SourcePolicy", sourcePolicy },
                    { "TargetPolicy", targetPolicy }
                }
            });

        var pipe = new CfnPipe(
            this,
            "Pipe",
            new CfnPipeProps
            {
                RoleArn = pipeRole.RoleArn,
                Source = queue.QueueArn,
                SourceParameters = new CfnPipe.PipeSourceParametersProperty
                {
                    SqsQueueParameters = new CfnPipe.PipeSourceSqsQueueParametersProperty
                    {
                        BatchSize = 1,
                        MaximumBatchingWindowInSeconds = 5
                    }
                },
                Target = stepFunction.StateMachineArn,
                TargetParameters = new CfnPipe.PipeTargetParametersProperty
                {
                    StepFunctionStateMachineParameters = new CfnPipe.PipeTargetStateMachineParametersProperty
                    {
                        InvocationType = "FIRE_AND_FORGET"
                    }
                }
            });

        var output = new CfnOutput(
            this,
            "ApiEndpoint",
            new CfnOutputProps
            {
                ExportName = "APIEndpoint",
                Description = "The endpoint for the created API",
                Value = frontendApi.Url
            });
    }
}