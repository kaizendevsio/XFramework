# Remote query membership and boolean semantics

Remote LINQ is parsed in `QueryExpressionVisitor`, serialized as a MemoryPack
`QueryDescriptor`, and executed by `QueryDescriptorExecutor`. Test that entire path;
an in-memory `IDataContext` fake does not exercise query serialization.

Generated query and query-stream wrappers use `CreateDataContextQueryAsync` to
request only `datacontext.query` for ordinary actor-bound reads. Actorless reads
still require `tenant.target`; filter bypass still requests both `datacontext.query.all-tenants`
and `tenant.target`. Actor tokens are resolved once and propagated unchanged, and
the owning service continues to enforce tenant/actor authorization. Mutation scope
selection is unchanged. `GeneratedQueryTokenScopeTests` captures token acquisition
from the actual compiled IdentityServer wrapper for each query/stream combination.

- Local arrays and `List<T>.Contains(entity.Property)` emit bounded `In` filters.
  C# 14's array-to-`ReadOnlySpan<T>` binding is supported without evaluating or
  boxing the span. Keep each lookup at 64 values or fewer; 50-item batches leave
  room for other filters. Arbitrary enumerables, custom comparers, null elements,
  and unsupported scalar types fail explicitly.
- Empty membership is an empty set, not an absent filter. Consecutive membership
  checks on the same property remain intersections.
- `And`/`Or` postfix markers count preceding wire tokens. The executor preserves
  nested groups and keeps its trusted tenant and soft-delete boundaries outside
  the user predicate. Invalid group boundaries fail rather than omit conditions.
- Ordinary `string.Contains` stays case-sensitive. Explicit
  `Contains(term, StringComparison.OrdinalIgnoreCase)` selects PostgreSQL `ILIKE`
  (database collation semantics); `%`, `_`, and backslash remain literal characters.
  Client and server must both have this appended operation before using it.

The generated feature gate receives HTTP verbs (`POST`, not `MapPost`). Unknown
verbs otherwise resolve to `manage`, which caused legitimate non-admin chat calls
to fail. New chat bootstrap/read endpoints also declare their capability explicitly.

`XFramework.Yap` is a known service caller, but every query still requires the
configured service scope, actor capability, and tenant boundary. Logout requires
an authenticated actor and checks session credential/tenant ownership; it does
not require granting the client administrative service permissions.

Regression coverage: `RemoteQueryPostgreSqlTests`, `QueryExpressionVisitorTests`,
`QueryExecutionServiceTests`, `BoltHandlerGeneratorTests`, and the IdentityServer
logout authorization/ownership tests. These run through the normal PR CI and
develop deployment workflow.
