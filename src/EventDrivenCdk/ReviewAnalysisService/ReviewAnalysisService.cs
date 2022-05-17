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

namespace EventDrivenCdk.ReviewAnalysisService
{
    public class ReviewAnalysisServiceProps
    {
        public EventBus CentralEventBus { get; set; }
    }

    public class ReviewAnalysisService : Construct
    {
        public ReviewAnalysisService(Construct scope, string id, ReviewAnalysisServiceProps props) :
            base(scope, id)
        {
            var sentimentAnlysisWorkflow = WorkflowStep.AnalyzeSentiment(scope)
                .Next(new Choice(this, "SentimentChoice")
                    .When(Condition.NumberGreaterThan("$.SentimentResult.SentimentScore.Positive", 0.95), WorkflowStep.PublishEvent(this, "PublishPositiveEvent", "positiveReview", props.CentralEventBus))
                    .When(Condition.NumberGreaterThan("$.SentimentResult.SentimentScore.Negative", 0.95), WorkflowStep.PublishEvent(this, "PublishNegativeEvent", "negativeReview", props.CentralEventBus))
                    .Otherwise(new Pass(this, "UnknownSentiment")));

            var analyseSentiment = WorkflowStep.DetectLanguage(this)
                .Next(WorkflowStep.FormatLanguageResults(this))
                .Next(new Choice(this, "TranslateNonEnLanguage")
                    .When(Condition.Not(Condition.StringEquals(JsonPath.StringAt("$.dominantLanguage"), "en")), WorkflowStep.TranslateNonEnglishLanguage(this) 
                    .Next(WorkflowStep.AddTranslationToState(this))
                    .Next(sentimentAnlysisWorkflow))
                    .Otherwise(sentimentAnlysisWorkflow));
            
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