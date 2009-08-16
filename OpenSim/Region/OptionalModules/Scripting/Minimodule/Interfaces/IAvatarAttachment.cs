namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IAvatarAttachment
    {
        //// <value>
        /// Describes where on the avatar the attachment is located
        /// </value>
        int Location { get ; }
        
        //// <value>
        /// Accessor to the rez'ed asset, representing the attachment
        /// </value>
        IObject Asset { get; }
    }
}