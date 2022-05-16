using System.Collections.Generic;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EventBus = Amazon.CDK.AWS.Events.EventBus;

namespace EventDrivenCdk.CustomerContactService
{
    public class CustomerContactServiceProps
    {
        public EventBus CentralEventBus { get; set; }
    }
    
    public class CustomerContactService : Construct
    {
        public CustomerContactService(Construct scope, string id, CustomerContactServiceProps props) : base(scope, id)
        {
            var customerContactTable = new Table(this, "CustomerContactClaim", new TableProps()
            {
                TableName = "CustomerContactTable",
                PartitionKey = new Attribute()
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            var negativeReviewNotification = new Topic(this, "ReviewNotificationTopic", new TopicProps()
            {
                DisplayName = "Negative Review Notification",
                TopicName = "NegativeReviewNotification"
            });
            negativeReviewNotification.AddSubscription(new EmailSubscription("", new EmailSubscriptionProps()
            {
                
            }));
            
            var awaitingClaimQueue = new Queue(this, "AwaitingClaimQueue", new QueueProps()
            {
                QueueName = "AwaitingClaim"
            });

            var workflow = new SnsPublish(this, "NotifyNewBadReview", new SnsPublishProps()
            {
                Topic = negativeReviewNotification,
                Message = TaskInput.FromText("There has been a new negative review"),
                ResultPath = "$.snsResult"
            }).Next(
                new SqsSendMessage(this, "QueueForClaim", new SqsSendMessageProps()
                    {
                        Queue = awaitingClaimQueue,
                        MessageBody = TaskInput.FromObject(new Dictionary<string, object>()
                        {
                            {"Token", JsonPath.TaskToken},
                            {
                                "Payload", new Dictionary<string, object>()
                                {
                                    {"emailAddress", JsonPath.StringAt("$.detail.emailAddress")},
                                    {"reviewContent", JsonPath.StringAt("$.detail.reviewContents")},
                                    {"originalReviewContents", JsonPath.StringAt("$.detail.originalReviewContents")},
                                    {"reviewId", JsonPath.StringAt("$.detail.reviewId")},
                                }
                            },
                        }),
                        ResultPath = "$.claimResponse",
                        IntegrationPattern = IntegrationPattern.WAIT_FOR_TASK_TOKEN,
                    })
                    .Next(new DynamoPutItem(this, "StoreCustomerServiceClaim", new DynamoPutItemProps()
                    {
                        Table = customerContactTable,
                        ResultPath = "$.output",
                        Item = new Dictionary<string, DynamoAttributeValue>(1)
                        {
                            {"PK", DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewId"))},
                            {
                                "Data", DynamoAttributeValue.FromMap(new Dictionary<string, DynamoAttributeValue>(3)
                                {
                                    {
                                        "reviewIdentifier",
                                        DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewIdentifier"))
                                    },
                                    {
                                        "claimedBy",
                                        DynamoAttributeValue.FromString(JsonPath.StringAt("$.claimResponse.ClaimedBy"))
                                    },
                                    {
                                        "reviewId",
                                        DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewId"))
                                    },
                                    {
                                        "emailAddress",
                                        DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.emailAddress"))
                                    },
                                    {
                                        "reviewContents",
                                        DynamoAttributeValue.FromString(JsonPath.StringAt("$.detail.reviewContents"))
                                    },
                                })
                            }
                        },
                    })));

            var stateMachine = new StateMachine(this, "CustomerContactWorkflow", new StateMachineProps
            {
                Definition = workflow,
                StateMachineType = StateMachineType.STANDARD
            });

            var rule = new Rule(this, "NegativeReviewRule", new RuleProps()
            {
                EventBus = props.CentralEventBus,
                RuleName = "NegativeReviewRule",
                EventPattern = new EventPattern()
                {
                    DetailType = new string[1] {"negative-review"},
                    Source = new string[1] {"event-driven-cdk.sentiment-analysis"},
                },
                Targets = new IRuleTarget[1]
                {
                    new SfnStateMachine(stateMachine)
                }
            });
        }
    }
}