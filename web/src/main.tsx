import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router";
import App from "./App";
import { frameworkI18n } from "./i18n";
import { loadConfig } from "./lib/config";
import "./styles.css";

// Loads /config.json first, so every part of the app sees the same values, then starts i18n with the
// deployment's default language (PUBLIC_DEFAULT_LANGUAGE; empty means the browser's).
loadConfig().then(async (config) => {
  await frameworkI18n.init(config.PUBLIC_DEFAULT_LANGUAGE);
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <BrowserRouter>
        <App config={config} />
      </BrowserRouter>
    </StrictMode>,
  );
});
