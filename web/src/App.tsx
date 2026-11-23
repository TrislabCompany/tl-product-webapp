import { LanguageSwitch } from "@trislabcompany/framework-ui";
import { useTranslation } from "react-i18next";
import { Link, Route, Routes } from "react-router";
import ItemsPage from "./features/items/ItemsPage";
import { changeLanguage } from "./i18n";
import type { AppConfig } from "./lib/config";

export default function App({ config }: { config: AppConfig }) {
  const { t } = useTranslation();

  return (
    <>
      <header>
        <Link to="/" className="brand">
          {t("brand")}
        </Link>
        <span className="header-end">
          <LanguageSwitch changeLanguage={changeLanguage} />
          <span className="environment">{config.PLATFORM_ENVIRONMENT ?? "local"}</span>
        </span>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<ItemsPage />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>
    </>
  );
}

function NotFound() {
  const { t } = useTranslation();

  return (
    <>
      <h1>{t("notFound")}</h1>
      <p>
        <Link to="/">{t("backToItems")}</Link>
      </p>
    </>
  );
}
