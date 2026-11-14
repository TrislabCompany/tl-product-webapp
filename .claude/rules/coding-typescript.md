---
paths:
  - "web/src/**/*.ts"
  - "web/src/**/*.tsx"
---

# TypeScript coding standards

Checked by: `pnpm --dir web test`, which runs `tsc --noEmit` (strict), ESLint (`web/eslint.config.js`: recommended, typescript-eslint, react-hooks, react-refresh) and Vitest. `ci / ci` runs it.

## Types

- No `any`. Use `unknown` and narrow it, or write the type. An `as` cast only at a boundary where the shape is known (a parsed response), as in `api.ts`.
- The API's types are hand-written in the feature's `api.ts`, with the API's field names (camelCase). Change them in the same PR as the endpoint.

## Components

- Function components; a page is the default export of `<Feature>Page.tsx`, smaller components are named exports. The file is named after the component, in PascalCase.
- Props typed with a `type <Name>Props`. State stays in the page until two pages need it.
- Hooks follow the rules of hooks (lint-checked).

## Errors and logging

- `api.ts` throws an `Error` with the method, path and status; the page shows the message in its error state. Never swallow a rejected promise.
- No `console.log` in committed code. A `console.warn` or `console.error` only where the user can't be told, with a short `<area>: <what>` message and no personal data.
- When the API returns ProblemDetails, show its `traceId` with the error, so the user can quote it (`logging.md`).

## Naming

| Thing | Shape | Example |
|---|---|---|
| Page / component | PascalCase | `ItemsPage` |
| API function | `<verb><Thing>` | `listItems`, `createItem` |
| Hook | `use<Thing>` | `useItems` |
| Test | next to the file, `<File>.test.tsx` | `ItemsPage.test.tsx` |
