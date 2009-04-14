using System;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IScheduler
    {
        /// <summary>
        /// Schedule an event callback to occur
        /// when 'time' is elapsed.
        /// </summary>
        /// <param name="time">The period to wait before executing</param>
        void RunIn(TimeSpan time);

        /// <summary>
        /// Schedule an event callback to fire
        /// every "time". Equivilent to a repeating
        /// timer.
        /// </summary>
        /// <param name="time">The period to wait between executions</param>
        void RunAndRepeat(TimeSpan time);

        /// <summary>
        /// Fire this scheduler only when the region has
        /// a user in it.
        /// </summary>
        bool WhenRegionOccupied { get; set; }

        /// <summary>
        /// Fire this event only when the region is visible
        /// to a child agent, or there is a full agent
        /// in this region.
        /// </summary>
        bool WhenRegionVisible { get; set; }

        /// <summary>
        /// Determines whether this runs in the master scheduler thread, or a new thread
        /// is spawned to handle your request. Running in scheduler may mean that your
        /// code does not execute perfectly on time, however will result in a lower
        /// processor cost to running your code.
        /// </summary>
        bool Schedule { get; set; }
    }
}
