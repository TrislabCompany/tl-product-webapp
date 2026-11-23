import "i18next";
import type { eagerCatalogs } from "./i18n";

// Types every t() key against the en-US catalogs, so a key a namespace lacks fails `tsc`.
declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "common";
    resources: typeof eagerCatalogs;
  }
}
