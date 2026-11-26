# The look of the app

The app starts in the Trislab theme of `@trislabcompany/framework-ui`: dark slate, the orange accent, Inter and a radius of `0.75rem`, light or dark as the operating system says. The theme is a set of CSS variables with the shadcn names (`--background`, `--foreground`, `--primary`, `--border`, `--ring`, `--radius`, …), listed in the package's README under "Theme".

## The files

| File | What it holds | Ownership |
| --- | --- | --- |
| `web/src/styles.css` | The layout. It imports the framework's `theme.css` and `fonts.css` first and the product's `theme.css` last, and uses only `var(--…)` | `shared`: a template update merges its changes |
| `web/src/theme.css` | The product's look: the variables it overrides, under `:root` (light) and `.dark` (dark). The only file with colours | `product`: a template update never changes it |
| `web/src/main.tsx` | `applyColorScheme("system")` puts the class `dark` on `<html>` when the operating system is dark | `product` |

## Restyling a product

Change `web/src/theme.css`, and nothing else:

```css
:root { --primary: #2563eb; --primary-foreground: #ffffff; --primary-hover: #1d4ed8; --primary-text: #1d4ed8; --ring: #1d4ed8; }
.dark { --primary: #3b82f6; --primary-foreground: #0f172a; --primary-hover: #60a5fa; --primary-text: #60a5fa; --ring: #60a5fa; }
```

- Keep the contrast of WCAG 2 AA: every `--*-foreground` needs 4.5:1 on its colour, and `--primary-text` and `--muted-foreground` need it on `--background`, `--card` and `--muted`.
- For another font, set `--font-sans` in `theme.css` and remove the `fonts.css` import from `styles.css`.
- A fixed light or dark look: call `applyColorScheme("light")` or `("dark")` in `main.tsx`.

## The checks

`pnpm --dir web test` runs both:

- `framework-ui-check-colors src` fails on a colour written into code: a hex value, a colour function (`rgb(`, `oklch(`, …) or Tailwind's palette, in CSS or a script. A `theme.css` isn't checked. A colour that must stay fixed, such as a logo's, gets the comment `raw-color-allowed: <why>` on its line.
- `src/theme.test.ts` fails when `styles.css` doesn't load the framework's theme first and `./theme.css` last, or sets a variable itself.
