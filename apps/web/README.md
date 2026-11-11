# React API client scaffold

## Prerequisites

- Node.js 22.18 or newer
- npm

## Generate and compile the API client

From this directory:

```bash
npm ci
npm run generate:api
npm run typecheck
npm run verify:query-compat
```

`openapi-typescript` generates types from the served contract. Orval generates a Fetch client from the served contract and a separate compatibility fixture from the deferred contract. The fixture is not part of the shipped client; it checks that `QUERY /messages` keeps its HTTP method and JSON body when the operation is promoted.

The current toolchain check found that `openapi-typescript` 7.13.0 omits the deferred `query` operation from `paths["/messages"]`, while Orval 8.47.0 preserves it in generated Fetch code. Decision [#37](https://github.com/tengtium-tech/marites/issues/37) tracks the remaining choice for React Query caching semantics. The OpenAPI contract has not been changed.
