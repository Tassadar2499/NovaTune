# Task 1.1: Project Structure Setup

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending

## Description

Verify and organize the existing Aspire project structure.

---

## Subtasks

### 1.1.1 Verify Existing Project Structure

- [ ] Verify existing project structure matches expected layout:
  - `NovaTuneApp.ApiService` - API endpoints, entities, services
  - `NovaTuneApp.Web` - Blazor frontend
  - `NovaTuneApp.AppHost` - Aspire orchestration
  - `NovaTuneApp.ServiceDefaults` - Shared configuration
  - `NovaTuneApp.Tests` - All tests

**Expected structure:**
```
src/NovaTuneApp/
├── NovaTuneApp.sln
├── NovaTuneApp.ApiService/
├── NovaTuneApp.Web/
├── NovaTuneApp.AppHost/
├── NovaTuneApp.ServiceDefaults/
└── NovaTuneApp.Tests/
```

---

### 1.1.2 Create ApiService Folder Structure

- [ ] Create folder structure within `NovaTuneApp.ApiService`:

```
NovaTuneApp.ApiService/
├── Models/           # Entities (User, Track, AudioMetadata)
├── Services/         # Business logic (AuthService, TrackService, etc.)
├── Endpoints/        # Minimal API route definitions
└── Infrastructure/   # External adapters (MinIO, RavenDB, Kafka, NCache)
```

**Commands:**
```bash
cd src/NovaTuneApp/NovaTuneApp.ApiService
mkdir -p Models Services Endpoints Infrastructure
```

---

### 1.1.3 Update Namespace Conventions

- [ ] Update namespace conventions to match folder structure

**Naming pattern:**
- `NovaTuneApp.ApiService.Models` for entities
- `NovaTuneApp.ApiService.Services` for business logic
- `NovaTuneApp.ApiService.Endpoints` for API routes
- `NovaTuneApp.ApiService.Infrastructure` for external adapters

---

### 1.1.4 Add EditorConfig Rules

- [ ] Add `.editorconfig` rules for code style enforcement

**Sample `.editorconfig`:**
```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# Naming conventions
dotnet_naming_rule.private_fields_should_be_camel_case.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_style

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_style.capitalization = camel_case
dotnet_naming_style.camel_case_style.required_prefix = _

# Code style
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# Brace style (Allman)
csharp_new_line_before_open_brace = all

# Nullable reference types
dotnet_diagnostic.CS8600.severity = error
dotnet_diagnostic.CS8601.severity = error
dotnet_diagnostic.CS8602.severity = error
dotnet_diagnostic.CS8603.severity = error
dotnet_diagnostic.CS8604.severity = error
```

---

## Acceptance Criteria

- [ ] All folders exist and are properly organized
- [ ] Namespaces follow folder structure
- [ ] Build succeeds with no warnings

---

## Verification Commands

```bash
# Verify build succeeds
dotnet build --no-restore /p:TreatWarningsAsErrors=true

# Verify format compliance
dotnet format --verify-no-changes
```

---

## Navigation

[Phase 1 Overview](overview.md) | [Task 1.2: Domain Entities](task-1.2-domain-entities.md)
