namespace Enmap
{
    public interface IBatchFetcherItem : IFetcherItem
    {
        IBatchProcessor BatchProcessor { get; }
    }
}