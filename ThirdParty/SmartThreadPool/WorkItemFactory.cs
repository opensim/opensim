using System;
using System.Threading;
using System.Runtime.CompilerServices;


namespace Amib.Threading.Internal
{
    #region WorkItemFactory class 

    public class WorkItemFactory
    {

        public static WorkItem CreateWorkItem(IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo, WorkItemInfo workItemInfo,
            WaitCallback callback, object state)
        {
            ValidateCallback(callback);
            ValidateCallback(workItemInfo.PostExecuteWorkItemCallback);
            return new WorkItem(workItemsGroup, new WorkItemInfo(workItemInfo), callback, state);

        }

        public static WorkItem CreateWorkItem(IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo,
            WaitCallback callback, object state)
        {
            ValidateCallback(callback);

            WorkItemInfo workItemInfo = new()
            {
                UseCallerCallContext = wigStartInfo.UseCallerCallContext,
                PostExecuteWorkItemCallback = wigStartInfo.PostExecuteWorkItemCallback,
                CallToPostExecute = wigStartInfo.CallToPostExecute,
                DisposeOfStateObjects = wigStartInfo.DisposeOfStateObjects,
            };

            return new WorkItem(workItemsGroup, workItemInfo, callback, state);
        }

        /// <summary>
        /// Create a new work item
        /// </summary>
        /// <param name="workItemsGroup">The WorkItemsGroup of this workitem</param>
        /// <param name="wigStartInfo">Work item group start information</param>
        /// <param name="callback">A callback to execute</param>
        /// <returns>Returns a work item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorkItem CreateWorkItem( IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo, WorkItemCallback callback)
        {
            return CreateWorkItem(workItemsGroup, wigStartInfo, callback, null);
        }

        /// <summary>
        /// Create a new work item
        /// </summary>
        /// <param name="workItemsGroup">The WorkItemsGroup of this workitem</param>
        /// <param name="wigStartInfo">Work item group start information</param>
        /// <param name="workItemInfo">Work item info</param>
        /// <param name="callback">A callback to execute</param>
        /// <returns>Returns a work item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorkItem CreateWorkItem( IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo,
            WorkItemInfo workItemInfo, WorkItemCallback callback)
        {
            return CreateWorkItem(workItemsGroup, wigStartInfo, workItemInfo, callback, null);
        }

        /// <summary>
        /// Create a new work item
        /// </summary>
        /// <param name="workItemsGroup">The WorkItemsGroup of this workitem</param>
        /// <param name="wigStartInfo">Work item group start information</param>
        /// <param name="callback">A callback to execute</param>
        /// <param name="state">
        /// The context object of the work item. Used for passing arguments to the work item. 
        /// </param>
        /// <returns>Returns a work item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorkItem CreateWorkItem( IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo,
            WorkItemCallback callback, object state)
        {
            ValidateCallback(callback);
            
            WorkItemInfo workItemInfo = new()
            {
                UseCallerCallContext = wigStartInfo.UseCallerCallContext,
                PostExecuteWorkItemCallback = wigStartInfo.PostExecuteWorkItemCallback,
                CallToPostExecute = wigStartInfo.CallToPostExecute,
                DisposeOfStateObjects = wigStartInfo.DisposeOfStateObjects,
            };

            return new WorkItem( workItemsGroup, workItemInfo, callback, state);
        }

        /// <summary>
        /// Create a new work item
        /// </summary>
        /// <param name="workItemsGroup">The work items group</param>
        /// <param name="wigStartInfo">Work item group start information</param>
        /// <param name="workItemInfo">Work item information</param>
        /// <param name="callback">A callback to execute</param>
        /// <param name="state">
        /// The context object of the work item. Used for passing arguments to the work item. 
        /// </param>
        /// <returns>Returns a work item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorkItem CreateWorkItem( IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo, WorkItemInfo workItemInfo,
            WorkItemCallback callback, object state)
        {
            ValidateCallback(callback);
            ValidateCallback(workItemInfo.PostExecuteWorkItemCallback);

            WorkItem workItem = new(
                workItemsGroup,
                new WorkItemInfo(workItemInfo),
                callback,
                state);

            return workItem;
        }

        /// <summary>
        /// Create a new work item
        /// </summary>
        /// <param name="workItemsGroup">The work items group</param>
        /// <param name="wigStartInfo">Work item group start information</param>
        /// <param name="callback">A callback to execute</param>
        /// <param name="state">
        /// The context object of the work item. Used for passing arguments to the work item. 
        /// </param>
        /// <param name="postExecuteWorkItemCallback">
        /// A delegate to call after the callback completion
        /// </param>
        /// <returns>Returns a work item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorkItem CreateWorkItem(IWorkItemsGroup workItemsGroup, WIGStartInfo wigStartInfo,
            WorkItemCallback callback, object state,PostExecuteWorkItemCallback postExecuteWorkItemCallback)
        {
            ValidateCallback(callback);
            ValidateCallback(postExecuteWorkItemCallback);

            WorkItemInfo workItemInfo = new()
            {
                UseCallerCallContext = wigStartInfo.UseCallerCallContext,
                PostExecuteWorkItemCallback = postExecuteWorkItemCallback,
                CallToPostExecute = wigStartInfo.CallToPostExecute,
                DisposeOfStateObjects = wigStartInfo.DisposeOfStateObjects
            };

            return new WorkItem( workItemsGroup, workItemInfo, callback, state);
        }

        /// <summary>
        /// Create a new work item
        /// </summary>
        /// <param name="workItemsGroup">The work items group</param>
        /// <param name="wigStartInfo">Work item group start information</param>
        /// <param name="callback">A callback to execute</param>
        /// <param name="state">
        /// The context object of the work item. Used for passing arguments to the work item. 
        /// </param>
        /// <param name="postExecuteWorkItemCallback">
        /// A delegate to call after the callback completion
        /// </param>
        /// <param name="callToPostExecute">Indicates on which cases to call to the post execute callback</param>
        /// <returns>Returns a work item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorkItem CreateWorkItem(IWorkItemsGroup workItemsGroup,WIGStartInfo wigStartInfo,
            WorkItemCallback callback, object state,
            PostExecuteWorkItemCallback postExecuteWorkItemCallback, CallToPostExecute callToPostExecute)
        {
            ValidateCallback(callback);
            ValidateCallback(postExecuteWorkItemCallback);

            WorkItemInfo workItemInfo = new()
            {
                UseCallerCallContext = wigStartInfo.UseCallerCallContext,
                PostExecuteWorkItemCallback = postExecuteWorkItemCallback,
                CallToPostExecute = callToPostExecute,
                DisposeOfStateObjects = wigStartInfo.DisposeOfStateObjects
            };

            return new WorkItem(workItemsGroup, workItemInfo, callback, state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateCallback(Delegate callback)
        {
            if (callback is not null && callback.GetInvocationList().Length > 1)
            {
                throw new NotSupportedException("SmartThreadPool doesn't support delegates chains");
            }
        }
    }

    #endregion
}
