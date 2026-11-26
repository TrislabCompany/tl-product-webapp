import { useEffect, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ApiError, getSummary, signIn, type StaffSummary } from "./api";
import { readToken, saveToken } from "./session";
import InvoiceDocumentsSection from "../invoice-documents/InvoiceDocumentsSection"; // module:invoice-documents

// The staff area at /admin: a sign-in form, then what only staff may see. The API refuses every staff request
// without a signed-in user who holds the staff permission, so this page only shows what the API allows
// (docs/staff-area.md). Replace the summary with the product's own staff pages.
export default function AdminPage() {
  const { t } = useTranslation("admin");
  const [token, setToken] = useState(readToken);

  const update = (next: string | undefined) => {
    saveToken(next);
    setToken(next);
  };

  return (
    <>
      <h1>{t("title")}</h1>
      {token ? <Summary token={token} onSignOut={() => update(undefined)} /> : <SignIn onSignedIn={update} />}
    </>
  );
}

function SignIn({ onSignedIn }: { onSignedIn: (token: string) => void }) {
  const { t } = useTranslation("admin");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(undefined);
    try {
      onSignedIn(await signIn(email.trim(), password));
    } catch (e) {
      setError(e instanceof ApiError && e.status < 500 ? t("signInFailed") : t("unavailable"));
    }
  };

  return (
    <form onSubmit={submit} aria-labelledby="staff-sign-in">
      <h2 id="staff-sign-in">{t("signIn")}</h2>
      {error && <p role="alert">{error}</p>}
      <label htmlFor="staff-email">{t("email")}</label>
      <input id="staff-email" type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} required />
      <label htmlFor="staff-password">{t("password")}</label>
      <input
        id="staff-password"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        required
      />
      <button type="submit">{t("signInButton")}</button>
    </form>
  );
}

function Summary({ token, onSignOut }: { token: string; onSignOut: () => void }) {
  const { t } = useTranslation("admin");
  const [summary, setSummary] = useState<StaffSummary>();
  const [error, setError] = useState<ApiError>();

  useEffect(() => {
    getSummary(token).then(setSummary, (e: unknown) => {
      if (e instanceof ApiError && e.status === 401) {
        onSignOut(); // the token expired: sign in again
      } else {
        setError(e instanceof ApiError ? e : new ApiError(0, undefined, String(e)));
      }
    });
  }, [token, onSignOut]);

  return (
    <>
      <p>
        <button type="button" onClick={onSignOut}>
          {t("signOut")}
        </button>
      </p>
      {error && (
        <p role="alert">
          {error.status === 403 ? t("forbidden") : error.status === 404 ? t("notEnabled") : t("unavailable")}
          {error.traceId && ` ${t("traceId", { id: error.traceId })}`}
        </p>
      )}
      {summary === undefined ? (
        !error && <p>{t("loading")}</p>
      ) : (
        <dl>
          <dt>{t("items")}</dt>
          <dd>{summary.items}</dd>
          <dt>{t("doneItems")}</dt>
          <dd>{summary.doneItems}</dd>
        </dl>
      )}
      {summary !== undefined && <InvoiceDocumentsSection token={token} />} {/* module:invoice-documents */}
    </>
  );
}
