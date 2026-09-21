# PostgreSQL integration tests

The PostgreSQL-only tests use a dedicated disposable database; they never fall back to the local development database.

```powershell
docker compose -f docker-compose.test.yml --env-file .env.test.example up -d
$env:COMVY_TEST_POSTGRES = 'Host=localhost;Port=5434;Database=condolink_test;Username=comvy_test;Password=comvy_test_local_only'
dotnet test backend/CondoLink.Tests/CondoLink.Tests.csproj --filter "FullyQualifiedName~PendingAssistantActionPostgres"
```

The compose service stores its data in `tmpfs`, so stopping it removes all test data. The test guard accepts only database names ending in `_test` and creates per-test temporary databases from that explicitly configured target.
