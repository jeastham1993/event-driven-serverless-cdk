namespace EventDrivenCdk.Frontend;

using System.Collections.Generic;

using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.StepFunctions;

using Constructs;

using EventDrivenCdk.SharedConstruct;

public record InboundRequestOrchestratorProps(Table Table, EventBus EventBus);

public class InboundRequestOrchestrator : Construct
{
    public DefaultStateMachine Orchestrator { get; private set; }
    public InboundRequestOrchestrator(
        Construct scope,
        string id,
        InboundRequestOrchestratorProps props) : base(
        scope,
        id)
    {
        // Define the business workflow to integrate with the HTTP request, generate the case id
        // store and publish.
        // Abstract the complexities of each Workflow Step behind a method call of legibility
        var stepFunction = new DefaultStateMachine(this, "ApiStateMachine",
            new Map(this, "LoopInputRecords", new MapProps()
            {
                InputPath = JsonPath.EntirePayload
            }).Iterator(
                new Pass(this, "ParseSQSInput", new PassProps()
                    {
                        Parameters = new Dictionary<string, object>(1)
                        {
                            { "parsed.$", "States.StringToJson($.body)" }
                        },
                        OutputPath = JsonPath.StringAt("$.parsed")
                    }).Next(
                        // Generate a case id that can be returned to the frontend
                        WorkflowStep.GenerateCaseId(this))
                    // Store the API data
                    .Next(WorkflowStep.StoreApiData(this, props.Table)
                        // Publish the new request event
                        .Next(WorkflowStep.PublishNewApiRequestEvent(this, props.EventBus))
                        // Format the HTTP response to return to the front end
                        .Next(WorkflowStep.FormatStateForHttpResponse(this)))), StateMachineType.EXPRESS);

        props.Table.GrantReadWriteData(stepFunction);

        this.Orchestrator = stepFunction;
    }
}