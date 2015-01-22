namespace EventStore.TestClientAPI
{
    public interface ICmdProcessor
    {
        bool Execute(CommandProcessorContext context, string[] args);
        string Usage { get; }
        string Keyword { get; }
    }
}