using System.Threading.Tasks;

namespace Enmap
{
    public interface IFetcherItem
    {
        Task ApplyFetchedValue(object value);
        object EntityId { get; }         
    }
}