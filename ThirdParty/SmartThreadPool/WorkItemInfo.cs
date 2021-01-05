namespace Amib.Threading
{
    #region WorkItemInfo class

    /// <summary>
    /// Summary description for WorkItemInfo.
    /// </summary>
    public class WorkItemInfo
    {
        public WorkItemInfo()
        {
            UseCallerCallContext = SmartThreadPool.DefaultUseCallerCallContext;
            DisposeOfStateObjects = SmartThreadPool.DefaultDisposeOfStateObjects;
            CallToPostExecute = SmartThreadPool.DefaultCallToPostExecute;
            PostExecuteWorkItemCallback = SmartThreadPool.DefaultPostExecuteWorkItemCallback;
        }

        public WorkItemInfo(WorkItemInfo workItemInfo)
        {
            UseCallerCallContext = workItemInfo.UseCallerCallContext;
            DisposeOfStateObjects = workItemInfo.DisposeOfStateObjects;
            CallToPostExecute = workItemInfo.CallToPostExecute;
            PostExecuteWorkItemCallback = workItemInfo.PostExecuteWorkItemCallback;
            Timeout = workItemInfo.Timeout;
        }

        /// <summary>
        /// Get/Set if to use the caller's security context
        /// </summary>
        public bool UseCallerCallContext { get; set; }

        /// <summary>
        /// Get/Set if to dispose of the state object of a work item
        /// </summary>
        public bool DisposeOfStateObjects { get; set; }

        /// <summary>
        /// Get/Set the run the post execute options
        /// </summary>
        public CallToPostExecute CallToPostExecute { get; set; }

        /// <summary>
        /// Get/Set the post execute callback
        /// </summary>
        public PostExecuteWorkItemCallback PostExecuteWorkItemCallback { get; set; }

        /// <summary>
        /// Get/Set the work item's timout in milliseconds.
        /// This is a passive timout. When the timout expires the work item won't be actively aborted!
        /// </summary>
        public long Timeout { get; set; }
    }

    #endregion
}
