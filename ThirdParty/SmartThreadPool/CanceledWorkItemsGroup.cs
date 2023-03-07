namespace Amib.Threading.Internal
{
    internal class CanceledWorkItemsGroup
    {
        public readonly static CanceledWorkItemsGroup NotCanceledWorkItemsGroup = new();

        public CanceledWorkItemsGroup()
        {
            IsCanceled = false;
        }

        public bool IsCanceled { get; set; }
    }
}