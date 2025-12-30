# Phase 8: Migration Verification & Cleanup

## 8.1 Full Integration Test
- [ ] Start full stack: `docker compose up -d`
- [ ] Run Aspire host: `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost`
- [ ] Verify all services healthy in Aspire dashboard
- [ ] Test cache operations manually
- [ ] Test message publishing/consuming manually
- [ ] Run full test suite

## 8.2 Code Cleanup
- [ ] `grep -r "NCache" --include="*.cs"` returns no results
- [ ] `grep -r "RabbitMQ" --include="*.cs"` returns no results
- [ ] `grep -r "Alachisoft" --include="*.csproj"` returns no results
- [ ] Remove any unused configuration sections

## 8.3 Final Verification
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] `dotnet format --verify-no-changes` passes

**Exit Criteria:** Clean codebase with no legacy references, all tests passing.
