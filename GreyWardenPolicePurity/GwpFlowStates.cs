namespace GreyWardenPolicePurity
{
    internal enum AtonementFlowState
    {
        Inactive = 0,
        Active = 1,
        WaitingForTurnIn = 2
    }

    internal enum PlayerBountyFlowState
    {
        Idle = 0,
        HuntingTarget = 1,
        WaitingForCollection = 2
    }

    internal enum PoliceTaskFlowState
    {
        None = 0,
        PreparingDispatch = 1,
        Pursuit = 2,
        WarPursuit = 3,
        EscortingPlayer = 4,
        PlayerBountyEscort = 5
    }
}
