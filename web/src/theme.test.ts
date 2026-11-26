import { existsSync, readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// The one-file restyle (docs/theme.md): styles.css loads the Trislab theme first and the product's
// theme.css last, so a variable set in theme.css wins over the framework's.
// Read from disk: Vitest turns an imported CSS file into an empty string. Vitest runs in web/.
const styles = readFileSync("src/styles.css", "utf8");
const imports = [...styles.matchAll(/^@import\s+"([^"]+)";/gm)].map((m) => m[1]);

describe("theme", () => {
  it("loads the framework's theme first and the product's theme.css last", () => {
    expect(imports[0]).toBe("@trislabcompany/framework-ui/theme.css");
    expect(imports.at(-1)).toBe("./theme.css");
  });

  it("sets no variable of the theme in styles.css", () => {
    // A `--name: value` outside theme.css would be a second place to change the look.
    expect(styles).not.toMatch(/^\s*--[\w-]+\s*:/m);
  });

  it("keeps the product's theme.css", () => {
    expect(existsSync("src/theme.css")).toBe(true);
  });
});
