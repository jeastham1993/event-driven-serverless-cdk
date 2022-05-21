using System.Collections.Generic;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace EventDrivenCdk.Frontend
{
    public static class WorkflowStep
    {
        public static DynamoUpdateItem GenerateCaseId(Construct scope, Table apiTable)
        {
            return new DynamoUpdateItem(scope, "GenerateCaseId", new DynamoUpdateItemProps()
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
            });
        }

        public static DynamoPutItem StoreApiData(Construct scope, Table apiTable)
        {
            return new DynamoPutItem(scope, "StoreApiInput", new DynamoPutItemProps()
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
                                DynamoAttributeValue.FromString(
                                    JsonPath.StringAt("$.reviewIdentifier.reviewId.attributeValue.S"))
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
            });
        }

        public static EventBridgePutEvents PublishNewApiRequestEvent(Construct scope, EventBus publishTo)
        {
            return new EventBridgePutEvents(scope, "PublishEvent", new EventBridgePutEventsProps()
            {
                Entries = new EventBridgePutEventsEntry[1]
                {
                    new EventBridgePutEventsEntry
                    {
                        Detail = TaskInput.FromObject(new Dictionary<string, object>(1)
                        {
                            {"reviewId", JsonPath.StringAt("$.reviewIdentifier.reviewId.attributeValue.S")},
                            {"reviewIdentifier", JsonPath.StringAt("$.body.reviewIdentifier")},
                            {"emailAddress", JsonPath.StringAt("$.body.emailAddress")},
                            {"reviewContents", JsonPath.StringAt("$.body.reviewContents")},
                            {"type", "newReview"},
                        }),
                        DetailType = "newReview",
                        Source = "event-driven-cdk.api",
                        EventBus = publishTo
                    }
                },
                ResultPath = "$.eventOutput",
            });
        }

        public static Pass FormatStateForHttpResponse(Construct scope)
        {
            return new Pass(scope, "FormatHTTPresponse", new PassProps()
            {
                Parameters = new Dictionary<string, object>(4)
                {
                    {"reviewId", JsonPath.StringAt("$.reviewIdentifier.reviewId.attributeValue.S")},
                    {"reviewIdentifier", JsonPath.StringAt("$.body.reviewIdentifier")},
                    {"emailAddress", JsonPath.StringAt("$.body.emailAddress")},
                    {"reviewContents", JsonPath.StringAt("$.body.reviewContents")}
                }
            });
        }
    }
}