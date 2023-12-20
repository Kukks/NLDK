namespace NLDK;

public class TransactionScript
{
    public string TransactionHash { get; set; }
    public string ScriptId { get; set; }
    public bool Spent { get; set; }

    public Transaction Transaction { get; set; }
    public Script Script { get; set; }
}