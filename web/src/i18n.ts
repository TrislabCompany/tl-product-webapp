import { createFrameworkI18n } from "@trislabcompany/framework-ui";
import admin from "./locales/en-US/admin.json"; // feature:staff-area
import common from "./locales/en-US/common.json";
import items from "./locales/en-US/items.json";

// The app's texts, in English (en-US) and Slovenian (sl-SI), as the framework requires: every text is in both
// (docs/architecture.md). en-US is eager, the language of the shell; sl-SI is one lazy chunk, loaded when
// it is chosen. A feature adds a catalog per namespace here and in both language folders.
export const eagerCatalogs = {
  common,
  items,
  admin, // feature:staff-area
};

export const frameworkI18n = createFrameworkI18n({
  // Two apps on one origin need different keys, or one would switch the other's language.
  storageKey: "tl-product-webapp.web",
  eager: eagerCatalogs,
  languages: { "sl-SI": () => import("./locales/sl-SI") },
});

export const i18n = frameworkI18n.i18n;
export const changeLanguage = frameworkI18n.changeLanguage;
