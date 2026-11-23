import { expect, test } from "vitest";

// en-US is the source, and every text ships in sl-SI too: a key one language lacks, or a translation whose
// {{placeholders}} differ, fails here, although at run time the text would fall back to English.
const catalogs = (glob: Record<string, Record<string, string>>) =>
  Object.fromEntries(Object.entries(glob).map(([file, catalog]) => [file.split("/").pop()!, catalog]));

const en = catalogs(import.meta.glob<Record<string, string>>("./locales/en-US/*.json", { eager: true, import: "default" }));
const sl = catalogs(import.meta.glob<Record<string, string>>("./locales/sl-SI/*.json", { eager: true, import: "default" }));

const placeholders = (text: string) => [...text.matchAll(/\{\{\s*(\w+)\s*\}\}/g)].map((m) => m[1]).sort();

test("sl-SI has a catalog for every en-US catalog, and no other", () => {
  expect(Object.keys(sl).sort()).toEqual(Object.keys(en).sort());
});

for (const [file, source] of Object.entries(en)) {
  test(`${file}: sl-SI translates every key, with the same placeholders`, () => {
    const translation = sl[file] ?? {};
    expect(Object.keys(translation).sort()).toEqual(Object.keys(source).sort());
    for (const [key, text] of Object.entries(source)) {
      expect(placeholders(translation[key] ?? ""), key).toEqual(placeholders(text));
    }
  });
}
