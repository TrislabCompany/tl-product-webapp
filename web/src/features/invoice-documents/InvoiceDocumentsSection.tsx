import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "../admin/api";
import {
  createDraft,
  DocumentStatus,
  DocumentType,
  getCustomers,
  getDocuments,
  getItems,
  getPdf,
  issue,
  markPaid,
  type Customer,
  type DocumentSummary,
  type Item,
} from "./api";

const TYPE_KEYS = {
  [DocumentType.Quote]: "invoiceTypeQuote",
  [DocumentType.ProForma]: "invoiceTypeProForma",
  [DocumentType.Invoice]: "invoiceTypeInvoice",
  [DocumentType.CreditNote]: "invoiceTypeCreditNote",
  [DocumentType.AdvanceInvoice]: "invoiceTypeAdvanceInvoice",
} as const satisfies Record<DocumentType, string>;

const STATUS_KEYS = {
  [DocumentStatus.Draft]: "invoiceStatusDraft",
  [DocumentStatus.Issued]: "invoiceStatusIssued",
  [DocumentStatus.Paid]: "invoiceStatusPaid",
  [DocumentStatus.Converted]: "invoiceStatusConverted",
} as const satisfies Record<DocumentStatus, string>;

// The documents the organisation issues to its customers, from the framework's InvoiceDocuments module: the list,
// a new draft from one item, issuing, marking an invoice paid, and the PDF. The issuer's details, customers and
// items come from the module's API; this starter shows them, and a product adds the forms it needs.
export default function InvoiceDocumentsSection({ token }: { token: string }) {
  const { t, i18n } = useTranslation("admin");
  const [documents, setDocuments] = useState<DocumentSummary[]>();
  const [error, setError] = useState<ApiError>();

  const load = useCallback(() => {
    getDocuments(token).then(setDocuments, (e: unknown) => setError(asApiError(e)));
  }, [token]);

  useEffect(load, [load]);

  const act = async (action: () => Promise<void>) => {
    setError(undefined);
    try {
      await action();
      load();
    } catch (e) {
      setError(asApiError(e));
    }
  };

  const openPdf = async (id: string) => {
    setError(undefined);
    try {
      const url = URL.createObjectURL(await getPdf(token, id));
      window.open(url, "_blank", "noopener");
    } catch (e) {
      setError(asApiError(e));
    }
  };

  const money = (amount: number, currency: string) =>
    new Intl.NumberFormat(i18n.language, { style: "currency", currency }).format(amount);

  return (
    <section aria-labelledby="invoice-documents">
      <h2 id="invoice-documents">{t("invoicesTitle")}</h2>
      {error && (
        <p role="alert">
          {error.status === 403 ? t("invoicesForbidden") : error.status === 422 || error.status === 400 ? t("invoicesRefused") : t("unavailable")}
          {error.traceId && ` ${t("traceId", { id: error.traceId })}`}
        </p>
      )}
      <NewDraft token={token} onCreated={load} onError={setError} />
      {documents === undefined ? (
        !error && <p>{t("loading")}</p>
      ) : documents.length === 0 ? (
        <p>{t("invoicesNone")}</p>
      ) : (
        <div className="table-frame">
          <table>
            <thead>
              <tr>
                <th scope="col">{t("invoicesNumber")}</th>
                <th scope="col">{t("invoicesType")}</th>
                <th scope="col">{t("invoicesCustomer")}</th>
                <th scope="col">{t("invoicesStatus")}</th>
                <th scope="col">{t("invoicesDueDate")}</th>
                <th scope="col" className="number">{t("invoicesTotal")}</th>
                <th scope="col">{t("invoicesActions")}</th>
              </tr>
            </thead>
            <tbody>
              {documents.map((d) => (
                <tr key={d.id}>
                  <td>{d.number ?? t("invoicesNoNumber")}</td>
                  <td>{t(TYPE_KEYS[d.type])}</td>
                  <td>{d.customerName}</td>
                  <td>
                    {t(STATUS_KEYS[d.status])}
                    {d.isOverdue && ` (${t("invoicesOverdue")})`}
                  </td>
                  <td>{d.dueDate ?? ""}</td>
                  <td className="number">{money(d.totalGross, d.currency)}</td>
                  <td>
                    {d.status === DocumentStatus.Draft && (
                      <button type="button" onClick={() => act(() => issue(token, d.id))}>
                        {t("invoicesIssue", { customer: d.customerName })}
                      </button>
                    )}
                    {d.status === DocumentStatus.Issued &&
                      (d.type === DocumentType.Invoice || d.type === DocumentType.AdvanceInvoice) && (
                        <button type="button" onClick={() => act(() => markPaid(token, d.id))}>
                          {t("invoicesMarkPaid", { number: d.number })}
                        </button>
                      )}
                    {d.number && (
                      <button type="button" onClick={() => openPdf(d.id)}>
                        {t("invoicesPdf", { number: d.number })}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function NewDraft({ token, onCreated, onError }: { token: string; onCreated: () => void; onError: (e: ApiError) => void }) {
  const { t } = useTranslation("admin");
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [type, setType] = useState<DocumentType>(DocumentType.Invoice);
  const [customerId, setCustomerId] = useState("");
  const [itemId, setItemId] = useState("");
  const [quantity, setQuantity] = useState("1");

  useEffect(() => {
    Promise.all([getCustomers(token), getItems(token)]).then(
      ([c, i]) => {
        setCustomers(c);
        setItems(i);
        setCustomerId((current) => current || (c[0]?.id ?? ""));
        setItemId((current) => current || (i[0]?.id ?? ""));
      },
      (e: unknown) => onError(asApiError(e)),
    );
  }, [token, onError]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    try {
      await createDraft(token, { type, customerId, itemId, quantity: Number(quantity) });
      onCreated();
    } catch (e) {
      onError(asApiError(e));
    }
  };

  return (
    <form onSubmit={submit} aria-labelledby="new-draft">
      <h3 id="new-draft">{t("invoicesNewDraft")}</h3>
      <label htmlFor="draft-type">{t("invoicesType")}</label>
      <select id="draft-type" value={type} onChange={(e) => setType(Number(e.target.value) as DocumentType)}>
        {[DocumentType.Invoice, DocumentType.ProForma, DocumentType.Quote].map((value) => (
          <option key={value} value={value}>
            {t(TYPE_KEYS[value])}
          </option>
        ))}
      </select>
      <label htmlFor="draft-customer">{t("invoicesCustomer")}</label>
      <select id="draft-customer" value={customerId} onChange={(e) => setCustomerId(e.target.value)} required>
        {customers.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>
      <label htmlFor="draft-item">{t("invoicesItem")}</label>
      <select id="draft-item" value={itemId} onChange={(e) => setItemId(e.target.value)} required>
        {items.map((i) => (
          <option key={i.id} value={i.id}>
            {i.name}
          </option>
        ))}
      </select>
      <label htmlFor="draft-quantity">{t("invoicesQuantity")}</label>
      <input
        id="draft-quantity"
        type="number"
        min="0.01"
        step="0.01"
        value={quantity}
        onChange={(e) => setQuantity(e.target.value)}
        required
      />
      <button type="submit">{t("invoicesCreateDraft")}</button>
    </form>
  );
}

function asApiError(e: unknown): ApiError {
  return e instanceof ApiError ? e : new ApiError(0, undefined, String(e));
}
