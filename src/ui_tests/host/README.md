# NovaTune UI Tests

Playwright + TypeScript UI tests live here and run against the Aspire-hosted Development stack.

## Commands

```bash
cd src/NovaTuneClient
pnpm test:ui
pnpm test:ui:headed
pnpm test:ui:install
```

## Notes

- The suite expects Docker and the .NET Aspire stack to be available locally.
- The Playwright runner starts `NovaTuneApp.AppHost` in `Development`.
- Player tests use unique listener accounts to stay isolated from persisted backend data.
- Admin tests use the seeded Development admin account unless `UI_TESTS_ADMIN_EMAIL` and `UI_TESTS_ADMIN_PASSWORD` are provided.
