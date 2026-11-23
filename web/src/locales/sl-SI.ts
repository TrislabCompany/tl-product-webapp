// Every sl-SI catalog in one module, so the language ships as one lazy chunk that i18n.ts loads on demand.
// Keyed by namespace, the file name. The test in i18n.test.ts fails on a key a catalog lacks.
const files = import.meta.glob<Record<string, string>>("./sl-SI/*.json", { eager: true, import: "default" });

export default Object.fromEntries(
  Object.entries(files).map(([file, catalog]) => [file.slice("./sl-SI/".length, -".json".length), catalog]),
);
