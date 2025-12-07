# Task 1.2: Core Domain Entities

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending

## Description

Define the foundational domain entities in the `Models/` folder.

---

## Subtasks

### 1.2.1 Create User Entity

- [ ] Create `Models/User.cs`:

```csharp
namespace NovaTuneApp.ApiService.Models;

public sealed class User
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
}
```

---

### 1.2.2 Create UserStatus Enum

- [ ] Create `Models/UserStatus.cs`:

```csharp
namespace NovaTuneApp.ApiService.Models;

public enum UserStatus
{
    Active,
    Disabled,
    PendingDeletion
}
```

---

### 1.2.3 Create Track Entity

- [ ] Create `Models/Track.cs`:

```csharp
namespace NovaTuneApp.ApiService.Models;

public sealed class Track
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public TimeSpan Duration { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public AudioMetadata? Metadata { get; set; }
    public TrackStatus Status { get; set; } = TrackStatus.Processing;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

---

### 1.2.4 Create TrackStatus Enum

- [ ] Create `Models/TrackStatus.cs`:

```csharp
namespace NovaTuneApp.ApiService.Models;

public enum TrackStatus
{
    Processing,
    Ready,
    Failed,
    Deleted
}
```

---

### 1.2.5 Create AudioMetadata Value Object

- [ ] Create `Models/AudioMetadata.cs`:

```csharp
namespace NovaTuneApp.ApiService.Models;

public sealed record AudioMetadata
{
    public string Format { get; init; } = string.Empty;
    public int Bitrate { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public long FileSizeBytes { get; init; }
    public string? MimeType { get; init; }
}
```

---

### 1.2.6 Add Validation Attributes

- [ ] Add validation attributes and data annotations to entities

**User validation:**
```csharp
using System.ComponentModel.DataAnnotations;

public sealed class User
{
    public string Id { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
}
```

**Track validation:**
```csharp
using System.ComponentModel.DataAnnotations;

public sealed class Track
{
    public string Id { get; init; } = string.Empty;

    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Artist { get; set; }

    public TimeSpan Duration { get; set; }

    [Required]
    public string ObjectKey { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Checksum { get; set; }

    public AudioMetadata? Metadata { get; set; }
    public TrackStatus Status { get; set; } = TrackStatus.Processing;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

---

### 1.2.7 Create Unit Tests

- [ ] Create unit tests for entity validation rules

**Test file:** `NovaTuneApp.Tests/Unit/Models/UserTests.cs`
```csharp
namespace NovaTuneApp.Tests.Unit.Models;

public class UserTests
{
    [Fact]
    public void User_WithValidData_PassesValidation()
    {
        var user = new User
        {
            Id = "users/1",
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var results = ValidateModel(user);
        Assert.Empty(results);
    }

    [Fact]
    public void User_WithInvalidEmail_FailsValidation()
    {
        var user = new User
        {
            Id = "users/1",
            Email = "invalid-email",
            DisplayName = "Test User",
            PasswordHash = "hashed"
        };

        var results = ValidateModel(user);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(User.Email)));
    }

    [Fact]
    public void User_WithEmptyDisplayName_FailsValidation()
    {
        var user = new User
        {
            Id = "users/1",
            Email = "test@example.com",
            DisplayName = "",
            PasswordHash = "hashed"
        };

        var results = ValidateModel(user);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(User.DisplayName)));
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
```

**Test file:** `NovaTuneApp.Tests/Unit/Models/TrackTests.cs`
```csharp
namespace NovaTuneApp.Tests.Unit.Models;

public class TrackTests
{
    [Fact]
    public void Track_WithValidData_PassesValidation()
    {
        var track = new Track
        {
            Id = "tracks/1",
            UserId = "users/1",
            Title = "Test Track",
            ObjectKey = "audio/test.mp3",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var results = ValidateModel(track);
        Assert.Empty(results);
    }

    [Fact]
    public void Track_WithEmptyTitle_FailsValidation()
    {
        var track = new Track
        {
            Id = "tracks/1",
            UserId = "users/1",
            Title = "",
            ObjectKey = "audio/test.mp3"
        };

        var results = ValidateModel(track);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Track.Title)));
    }

    [Fact]
    public void Track_DefaultStatus_IsProcessing()
    {
        var track = new Track();
        Assert.Equal(TrackStatus.Processing, track.Status);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
```

---

## Acceptance Criteria

- [ ] All entities are defined with proper nullability annotations
- [ ] Validation rules are implemented and tested
- [ ] Entities follow C# 12 conventions

---

## File Checklist

- [ ] `Models/User.cs`
- [ ] `Models/UserStatus.cs`
- [ ] `Models/Track.cs`
- [ ] `Models/TrackStatus.cs`
- [ ] `Models/AudioMetadata.cs`
- [ ] `NovaTuneApp.Tests/Unit/Models/UserTests.cs`
- [ ] `NovaTuneApp.Tests/Unit/Models/TrackTests.cs`

---

## Navigation

[Task 1.1: Project Structure](task-1.1-project-structure.md) | [Phase 1 Overview](overview.md) | [Task 1.3: Docker Compose](task-1.3-docker-compose.md)
