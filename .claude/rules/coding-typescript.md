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

## Texts

- Every text on screen goes in `web/src/locales/en-US/<namespace>.json` and the same key in `sl-SI/`, and is read with `useTranslation(<namespace>)` and `t()`. No literal text in JSX or in an attribute such as `aria-label`. `web/src/i18n.test.ts` fails on a key one language lacks or on different `{{placeholders}}`; the keys are typed against the English catalogs (`i18next.d.ts`), so `tsc` fails on a key that does not exist.
- A new namespace is added to `eagerCatalogs` in `web/src/i18n.ts` and to both language folders. Dates and numbers go through the formatters of `@trislabcompany/framework-ui` (`formatDate`, `formatNumber`, `formatMoney`), in the language on screen; the API sends values, never formatted text.
- `@trislabcompany/framework-ui` is the framework's package: a missing feature or a bug in it is a change in `tl-framework`, not a copy here.

## Naming

| Thing | Shape | Example |
|---|---|---|
| Page / component | PascalCase | `ItemsPage` |
| API function | `<verb><Thing>` | `listItems`, `createItem` |
| Hook | `use<Thing>` | `useItems` |
| Test | next to the file, `<File>.test.tsx` | `ItemsPage.test.tsx` |
