import { beforeAll } from "vitest";
import { frameworkI18n } from "./i18n";

// The components render with the app's i18n, in English.
beforeAll(async () => {
  await frameworkI18n.init("en-US");
});
