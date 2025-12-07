namespace NovaTuneApp.ApiService.Models;

public enum TrackStatus : byte
{
    Unknown = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3,
    Deleted = 4
}
