// Calls the framework's InvoiceDocuments module at /api/v1/invoice-documents with the staff member's token. Its
// permissions are the module's own (the Admin role holds them all); a staff member without them gets 403.
// Enums travel as numbers, as the framework's JSON writes them.
import { ApiError } from "../admin/api";

export const DocumentType = { Quote: 0, ProForma: 1, Invoice: 2, CreditNote: 3, AdvanceInvoice: 4 } as const;
export type DocumentType = (typeof DocumentType)[keyof typeof DocumentType];

export const DocumentStatus = { Draft: 0, Issued: 1, Paid: 2, Converted: 3 } as const;
export type DocumentStatus = (typeof DocumentStatus)[keyof typeof DocumentStatus];

export interface DocumentSummary {
  id: string;
  type: DocumentType;
  status: DocumentStatus;
  number: string | null;
  customerName: string;
  currency: string;
  issueDate: string | null;
  dueDate: string | null;
  totalGross: number;
  isOverdue: boolean;
}

export interface Customer {
  id: string;
  name: string;
}

export interface Item {
  id: string;
  name: string;
  unitPrice: number;
}

interface Page<T> {
  items: T[];
}

const BASE = "/api/v1/invoice-documents";

async function call(token: string, method: string, path: string, body?: unknown): Promise<Response> {
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    let traceId: string | undefined;
    try {
      traceId = ((await response.json()) as { traceId?: string }).traceId;
    } catch {
      // An answer without a JSON body has no trace id.
    }
    throw new ApiError(response.status, traceId, `${method} ${BASE}${path}: ${response.status}`);
  }
  return response;
}

export async function getDocuments(token: string): Promise<DocumentSummary[]> {
  const response = await call(token, "GET", "/documents?pageSize=50");
  return ((await response.json()) as Page<DocumentSummary>).items;
}

export async function getCustomers(token: string): Promise<Customer[]> {
  const response = await call(token, "GET", "/customers?pageSize=100");
  return ((await response.json()) as Page<Customer>).items;
}

export async function getItems(token: string): Promise<Item[]> {
  const response = await call(token, "GET", "/items?pageSize=100");
  return ((await response.json()) as Page<Item>).items;
}

/** A draft with one line from an item; the module fills the line's text, price and tax code from the item. */
export async function createDraft(
  token: string,
  draft: { type: DocumentType; customerId: string; itemId: string; quantity: number },
): Promise<void> {
  await call(token, "POST", "/documents", {
    type: draft.type,
    customerId: draft.customerId,
    lines: [{ itemId: draft.itemId, quantity: draft.quantity }],
  });
}

export async function issue(token: string, id: string): Promise<void> {
  await call(token, "POST", `/documents/${id}/issue`);
}

export async function markPaid(token: string, id: string): Promise<void> {
  await call(token, "POST", `/documents/${id}/mark-paid`);
}

/** The PDF as a blob, opened by the page in a new tab. */
export async function getPdf(token: string, id: string): Promise<Blob> {
  const response = await call(token, "GET", `/documents/${id}/pdf`);
  return response.blob();
}
