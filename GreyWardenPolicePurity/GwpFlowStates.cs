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
        Pursuit = 1,
        WarPursuit = 2,
        EscortingPlayer = 3,
        PlayerBountyEscort = 4
    }
}
