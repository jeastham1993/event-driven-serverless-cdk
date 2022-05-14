using System.Collections.Generic;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EventBus = Amazon.CDK.AWS.Events.EventBus;
using LogGroupProps = Amazon.CDK.AWS.Events.Targets.LogGroupProps;

namespace EventDrivenCdk
{
    public class SentimentAnalysisServiceStackProps
    {
        public EventBus CentralEventBus { get; set; }
    }

    public class SentimentAnalysisServiceStack : Construct
    {
        public SentimentAnalysisServiceStack(Construct scope, string id, SentimentAnalysisServiceStackProps props) :
            base(scope, id)
        {
            var sentimentAnalysisChain = new CallAwsService(this, "CallSentimentAnalysis", new CallAwsServiceProps()
            {
                Service = "comprehend",
                Action = "detectSentiment",
                Parameters = new Dictionary<string, object>(2)
                    {
                        {"LanguageCode", "en"},
                        {
                            "Text", JsonPath.StringAt("$.reviewContents")
                        }
                    },
                IamResources = new string[1] { "*" },
                ResultPath = "$.SentimentResult"
            }).Next(new Choice(this, "SentimentChoice")
                    .When(Condition.NumberGreaterThan("$.SentimentResult.SentimentScore.Positive", 0.95), new EventBridgePutEvents(this, "PublishPositiveEvent", new EventBridgePutEventsProps()
                    {
                        Entries = new EventBridgePutEventsEntry[1]
                        {
                            new EventBridgePutEventsEntry
                            {
                                Detail = TaskInput.FromObject(new Dictionary<string, object>(7)
                                {
                                    { "dominantLanguage", JsonPath.StringAt("$.dominantLanguage") },
                                    { "reviewIdentifier", JsonPath.StringAt("$.reviewIdentifier") },
                                    { "reviewId", JsonPath.StringAt("$.reviewId") },
                                    { "emailAddress", JsonPath.StringAt("$.emailAddress") },
                                    { "reviewContents", JsonPath.StringAt("$.reviewContents") },
                                    { "originalReviewContents", JsonPath.StringAt("$.originalReviewContents") },
                                    { "type", "positiveReview" },
                                }),
                                DetailType = "positive-review",
                                Source = "event-driven-cdk.sentiment-analysis",
                                EventBus = props.CentralEventBus
                            }
                        }
                    }))
                    .When(Condition.NumberGreaterThan("$.SentimentResult.SentimentScore.Negative", 0.95), new EventBridgePutEvents(this, "PublishNegativeEvent", new EventBridgePutEventsProps()
                    {
                        Entries = new EventBridgePutEventsEntry[1]
                        {
                            new EventBridgePutEventsEntry
                            {
                                Detail = TaskInput.FromObject(new Dictionary<string, object>(4)
                                {
                                    { "dominantLanguage", JsonPath.StringAt("$.dominantLanguage") },
                                    { "reviewIdentifier", JsonPath.StringAt("$.reviewIdentifier") },
                                    { "reviewId", JsonPath.StringAt("$.reviewId") },
                                    { "emailAddress", JsonPath.StringAt("$.emailAddress") },
                                    { "reviewContents", JsonPath.StringAt("$.reviewContents") },
                                    { "originalReviewContents", JsonPath.StringAt("$.originalReviewContents") },
                                    { "type", "negativeReview" },
                                }),
                                DetailType = "negative-review",
                                Source = "event-driven-cdk.sentiment-analysis",
                                EventBus = props.CentralEventBus
                            }
                        }
                    }))
                    .Otherwise(new Pass(this, "UnknownSentiment")));

            var analyseSentiment = new CallAwsService(this, "DetectReviewLanguage", new CallAwsServiceProps()
            {
                Service = "comprehend",
                Action = "detectDominantLanguage",
                Parameters = new Dictionary<string, object>(2)
                    {
                        {
                            "Text", JsonPath.StringAt("$.detail.reviewContents")
                        }
                    },
                IamResources = new string[1] { "*" },
                ResultPath = "$.DominantLanguage"
            })
                .Next(new Pass(this, "FormatResult", new PassProps()
                {
                    Parameters = new Dictionary<string, object>(4)
                    {
                        { "dominantLanguage", JsonPath.StringAt("$.DominantLanguage.Languages[0].LanguageCode") },
                        { "reviewIdentifier", JsonPath.StringAt("$.detail.reviewIdentifier") },
                        { "reviewId", JsonPath.StringAt("$.detail.reviewId") },
                        { "emailAddress", JsonPath.StringAt("$.detail.emailAddress") },
                        { "reviewContents", JsonPath.StringAt("$.detail.reviewContents") },
                        { "originalReviewContents", JsonPath.StringAt("$.detail.reviewContents") },
                    }
                }))
                .Next(new Choice(this, "TranslateNonEnLanguage")
                    .When(Condition.Not(Condition.StringEquals(JsonPath.StringAt("$.dominantLanguage"), "en")), new CallAwsService(this, "TranslateNonEn", new CallAwsServiceProps()
                    {
                        Service = "translate",
                        Action = "translateText",
                        Parameters = new Dictionary<string, object>(3)
                        {
                            { "SourceLanguageCode", JsonPath.StringAt("$.dominantLanguage") },
                            { "TargetLanguageCode", "en" },
                            { "Text", JsonPath.StringAt("$.reviewContents") },
                        },
                        IamResources = new string[1] { "*" },
                        ResultPath = "$.Translation"
                    })
                    .Next(new Pass(this, "AddTranslatedTextToState", new PassProps()
                    {
                        Parameters = new Dictionary<string, object>(4)
                    {
                        { "dominantLanguage", JsonPath.StringAt("$.dominantLanguage") },
                        { "reviewIdentifier", JsonPath.StringAt("$.reviewIdentifier") },
                        { "reviewId", JsonPath.StringAt("$.reviewId") },
                        { "emailAddress", JsonPath.StringAt("$.emailAddress") },
                        { "reviewContents", JsonPath.StringAt("$.Translation.TranslatedText") },
                        { "originalReviewContents", JsonPath.StringAt("$.reviewContents") },
                    }
                    }))
                    .Next(sentimentAnalysisChain))
                    .Otherwise(sentimentAnalysisChain));
            
            var stateMachine = new StateMachine(this, "SentimentAnalysisStateMachine", new StateMachineProps
            {
                Definition = analyseSentiment,
                StateMachineType = StateMachineType.STANDARD
            });

            var rule = new Rule(this, "TriggerSentimentAnalysisRule", new RuleProps()
            {
                EventBus = props.CentralEventBus,
                RuleName = "NewReviewEvent",
                EventPattern = new EventPattern()
                {
                    DetailType = new string[1] {"new-review"},
                    Source = new string[1] {"event-driven-cdk.api"},
                },
                Targets = new IRuleTarget[1]
                {
                    new SfnStateMachine(stateMachine)
                }
            });
        }
    }
}