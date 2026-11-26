import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import InvoiceDocumentsSection from "./InvoiceDocumentsSection";

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const documents = [
  { id: "d1", type: 2, status: 0, number: null, customerName: "Example Household", currency: "EUR", issueDate: null, dueDate: null, totalGross: 146.4, isOverdue: false },
  { id: "d2", type: 2, status: 1, number: "RAC-2026-00002", customerName: "Example Company", currency: "EUR", issueDate: "2026-11-26", dueDate: "2026-12-11", totalGross: 323.3, isOverdue: false },
];

// Answers by path, so the order of the section's calls doesn't matter.
function stubApi(overrides: Record<string, Response> = {}) {
  const fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const key = `${init?.method ?? "GET"} ${url}`;
    if (overrides[key]) return overrides[key].clone();
    if (url.includes("/documents?")) return json({ items: documents });
    if (url.includes("/customers?")) return json({ items: [{ id: "c1", name: "Example Company" }] });
    if (url.includes("/items?")) return json({ items: [{ id: "i1", name: "Consulting", unitPrice: 60 }] });
    return json({});
  });
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

test("lists the documents with the staff member's token", async () => {
  const fetch = stubApi();

  render(<InvoiceDocumentsSection token="token-1" />);

  expect(await screen.findByText("RAC-2026-00002")).toBeTruthy();
  expect(screen.getAllByText("Draft", { selector: "td" })).toHaveLength(2); // no number yet, and the status
  const call = fetch.mock.calls.find(([url]) => String(url).includes("/documents?"))!;
  expect((call[1] as RequestInit).headers).toMatchObject({ Authorization: "Bearer token-1" });
});

test("issues a draft, then reloads the list", async () => {
  const fetch = stubApi();

  render(<InvoiceDocumentsSection token="token-1" />);
  fireEvent.click(await screen.findByRole("button", { name: "Issue the draft for Example Household" }));

  await waitFor(() =>
    expect(fetch).toHaveBeenCalledWith("/api/v1/invoice-documents/documents/d1/issue", expect.objectContaining({ method: "POST" })),
  );
  await waitFor(() => expect(fetch.mock.calls.filter(([url]) => String(url).includes("/documents?")).length).toBe(2));
});

test("offers to mark only an issued invoice paid", async () => {
  stubApi();

  render(<InvoiceDocumentsSection token="token-1" />);

  expect(await screen.findByRole("button", { name: "Mark RAC-2026-00002 paid" })).toBeTruthy();
  expect(screen.getAllByRole("button", { name: /^Mark .* paid$/ })).toHaveLength(1);
});

test("creates a draft from the chosen customer and item", async () => {
  const fetch = stubApi();

  render(<InvoiceDocumentsSection token="token-1" />);
  await screen.findByRole("option", { name: "Consulting" });
  fireEvent.change(screen.getByLabelText("Quantity"), { target: { value: "3" } });
  fireEvent.click(screen.getByRole("button", { name: "Create the draft" }));

  await waitFor(() => {
    const post = fetch.mock.calls.find(([url, init]) => String(url).endsWith("/documents") && (init as RequestInit)?.method === "POST");
    expect(JSON.parse((post![1] as RequestInit).body as string)).toEqual({
      type: 2,
      customerId: "c1",
      lines: [{ itemId: "i1", quantity: 3 }],
    });
  });
});

test("tells a staff member without the module's permissions", async () => {
  stubApi({ "GET /api/v1/invoice-documents/documents?pageSize=50": json({ traceId: "abc" }, 403) });

  render(<InvoiceDocumentsSection token="token-1" />);

  expect((await screen.findByRole("alert")).textContent).toContain("may not see the documents");
});
