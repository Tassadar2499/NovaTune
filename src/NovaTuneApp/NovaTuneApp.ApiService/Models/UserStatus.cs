namespace NovaTuneApp.ApiService.Models;

public enum UserStatus : byte
{
    Unknown = 0,
    Active = 1,
    Disabled = 2,
    PendingDeletion = 3
}
