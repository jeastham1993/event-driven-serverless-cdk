namespace EventDrivenCdk.ReviewAnalysisService;

using Amazon.CDK.AWS.StepFunctions;

using Constructs;

using EventDrivenCdk.SharedConstruct;

public record ReviewAnalysisOrchestratorProps;

public class ReviewAnalysisOrchestrator : Construct
{
    public StateMachine Orchestrator { get; }

    public ReviewAnalysisOrchestrator(
        Construct scope,
        string id,
        ReviewAnalysisOrchestratorProps props) :
        base(
            scope,
            id)
    {
        // Define workflow module to run sentiment analysis.
        var analyzeSentiment = WorkflowStep.AnalyzeSentiment(scope)
            // Publish a different event type depending on the sentiment results
            .Next(
                new Choice(
                        this,
                        "SentimentChoice")
                    .When(
                        Condition.NumberGreaterThan(
                            "$.SentimentResult.SentimentScore.Positive",
                            0.95),
                        WorkflowStep.PublishEvent(
                            this,
                            "PublishPositiveEvent",
                            "positiveReview",
                            MessageBus.EventBus))
                    .When(
                        Condition.NumberGreaterThan(
                            "$.SentimentResult.SentimentScore.Negative",
                            0.95),
                        WorkflowStep.PublishEvent(
                            this,
                            "PublishNegativeEvent",
                            "negativeReview",
                            MessageBus.EventBus))
                    .Otherwise(
                        new Pass(
                            this,
                            "UnknownSentiment")));

        // Define workflow to run translation and call sentiment analysis module.
        var analyseSentiment = WorkflowStep.DetectLanguage(this)
            .Next(WorkflowStep.FormatLanguageResults(this))
            .Next(
                new Choice(
                        this,
                        "TranslateNonEnLanguage")
                    .When(
                        Condition.Not(
                            Condition.StringEquals(
                                JsonPath.StringAt("$.dominantLanguage"),
                                "en")),
                        WorkflowStep.TranslateNonEnglishLanguage(this)
                            .Next(WorkflowStep.AddTranslationToState(this))
                            .Next(analyzeSentiment))
                    .Otherwise(analyzeSentiment));

        var stateMachine = new DefaultStateMachine(
            this,
            "SentimentAnalysisStateMachine",
            analyseSentiment,
            StateMachineType.STANDARD);

        this.Orchestrator = stateMachine;
    }
}