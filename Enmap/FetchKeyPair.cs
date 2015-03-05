namespace Enmap
{
    public class FetchKeyPair<TParent, TChild> : IFetchKeyPair
    {
        public TParent ParentId { get; set; }
        public TChild ChildId { get; set; }

        object IFetchKeyPair.ParentId
        {
            get { return ParentId; }
        }

        object IFetchKeyPair.ChildId
        {
            get { return ChildId; }
        }
    }

    public interface IFetchKeyPair
    {
        object ParentId { get; }
        object ChildId { get; }
    }
}