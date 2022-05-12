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
            var analyseSentiment = new CallAwsService(this, "CallSentimentAnalysis", new CallAwsServiceProps()
                {
                    Service = "comprehend",
                    Action = "detectSentiment",
                    Parameters = new Dictionary<string, object>(2)
                    {
                        {"LanguageCode", "en"},
                        {
                            "Text", JsonPath.StringAt("$.detail.reviewContents")
                        }
                    },
                    IamResources = new string[1] {"*"},
                    ResultPath = "$.SentimentResult"
                })
                .Next(new Choice(this, "SentimentChoice")
                    .When(Condition.NumberGreaterThan("$.SentimentResult.SentimentScore.Positive", 0.95), new EventBridgePutEvents(this, "PublishPositiveEvent", new EventBridgePutEventsProps()
                    {
                        Entries = new EventBridgePutEventsEntry[1]
                        {
                            new EventBridgePutEventsEntry
                            {
                                Detail = TaskInput.FromJsonPathAt("$.detail"),
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
                                Detail = TaskInput.FromJsonPathAt("$.detail"),
                                DetailType = "negative-review",
                                Source = "event-driven-cdk.sentiment-analysis",
                                EventBus = props.CentralEventBus
                            }
                        }
                    }))
                    .Otherwise(new Pass(this, "UnknownSentiment")));
            
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