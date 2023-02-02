namespace EventDrivenCdk.Frontend;

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;

using Constructs;

public record DataStorageProps(string TableName);

public class DataStorage : Construct
{
    public Table Table { get; private set; }
    
    public DataStorage(
        Construct scope,
        string id,
        DataStorageProps props) : base(
        scope,
        id)
    {
        var apiTable = new Table(scope, "StorageFirstInput", new TableProps()
        {
            TableName = props.TableName,
            PartitionKey = new Attribute()
            {
                Name = "PK",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        Table = apiTable;
    }
}