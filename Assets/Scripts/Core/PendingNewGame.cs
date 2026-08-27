namespace DemocracyWay.Core
{
    /// <summary>
    /// Hand-off between the main menu and character creation: the menu stores
    /// which empty slot the player picked, the creation screen reads it when
    /// starting the run. Cleared implicitly by the next new-game flow.
    /// </summary>
    public static class PendingNewGame
    {
        /// <summary>Slot the new run will save into. -1 = nothing pending.</summary>
        public static int TargetSlot = -1;
    }
}
