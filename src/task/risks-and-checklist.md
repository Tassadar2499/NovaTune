# Risks, Rollback & Checklist

## Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Redpanda Kafka API incompatibility | High | Low | Redpanda maintains high compatibility; test critical paths |
| Garnet Redis command gaps | Medium | Low | Garnet supports all standard commands; test cache patterns |
| SASL/TLS misconfiguration | Medium | Medium | Validate in stage dual-run before prod |
| Compaction/retention mismatch | Medium | Low | Verify topic policies against FR data needs in QA |
| Garnet memory pressure | Medium | Medium | Enable AOF persistence; monitor memory usage |
| Missing NCache features | Low | Low | NovaTune uses basic cache patterns (GET/SET/TTL) |

---

## Rollback Procedure

If critical issues are discovered:

1. **Immediate:** Revert to tagged commit `pre-redpanda-migration`
2. **Docker:** `docker compose down -v && git checkout docker-compose.yml && docker compose up -d`
3. **Packages:** `git checkout *.csproj && dotnet restore`

For stage dual-run issues:
- Kafka and Redpanda can coexist temporarily
- Route traffic back to Kafka if Redpanda issues arise
- Extend dual-run period if needed

---

## Checklist Summary

### Phase Completion Checklist
- [ ] Phase 1: Docker infrastructure updated
- [ ] Phase 2: NuGet packages updated
- [ ] Phase 3: Aspire AppHost configured
- [ ] Phase 4: Caching layer migrated
- [ ] Phase 5: Messaging layer migrated
- [ ] Phase 6: Tests updated
- [ ] Phase 7: Documentation updated
- [ ] Phase 8: Verification complete

### Final Sign-off
- [ ] All tests passing
- [ ] No legacy technology references
- [ ] Documentation current
- [ ] Team notified of changes
