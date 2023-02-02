namespace EventDrivenCdk.EventAuditService;

using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.StepFunctions;

using Constructs;

using EventDrivenCdk.SharedConstruct;

public record AuditQueryProcessorProps(Table Table);

public class AuditQueryProcessor : Construct
{
    public string Endpoint { get; private set; }

    public AuditQueryProcessor(
        Construct scope,
        string id,
        AuditQueryProcessorProps props) : base(
        scope,
        id)
    {
        // Simple workflow to take event bridge input and store in dynamodb.
        var queryWorkflow = WorkflowStep.QueryDynamo(this)
            .Next(
                new Map(
                    this,
                    "LoopItems",
                    new MapProps
                    {
                        ItemsPath = "$.QueryResult.Items"
                    }).Iterator(WorkflowStep.FormatResponse(this)));

        var apiStateMachine = new DefaultStateMachine(
            this,
            "EventAuditApiStateMachine",
            queryWorkflow,
            StateMachineType.EXPRESS);

        apiStateMachine.AddToRolePolicy(
            new PolicyStatement(
                new PolicyStatementProps
                {
                    Actions = new string[1] { "dynamodb:Query" },
                    Resources = new string[1] { props.Table.TableArn }
                }));

        // Create the API
        var queryApi = new StepFunctionsRestApi(
            this,
            "EventAuditQueryApi",
            new StepFunctionsRestApiProps
            {
                StateMachine = apiStateMachine,
            });

        Endpoint = queryApi.Url;
    }
}