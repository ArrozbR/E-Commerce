namespace KeycapStore.Application.Identity;

public enum AccountDeletionResult
{
    Deleted,
    WrongPassword,
    WrongEmail,
    LockedOut,
    IsAdmin,
    HasOpenOrders,
}
