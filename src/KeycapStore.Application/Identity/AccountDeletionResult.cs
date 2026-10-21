namespace KeycapStore.Application.Identity;

public enum AccountDeletionResult
{
    Deleted,
    WrongPassword,
    LockedOut,
    IsAdmin,
    HasOpenOrders,
}
