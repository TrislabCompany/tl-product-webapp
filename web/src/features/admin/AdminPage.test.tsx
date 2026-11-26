import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import AdminPage from "./AdminPage";

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

beforeEach(() => sessionStorage.clear());

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const signIn = async () => {
  fireEvent.change(await screen.findByLabelText("E-mail"), { target: { value: "staff@example.com" } });
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: "secret" } });
  fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
};

test("asks for a sign-in before it shows anything", () => {
  vi.stubGlobal("fetch", vi.fn());

  render(<AdminPage />);

  expect(screen.getByLabelText("E-mail")).toBeTruthy();
  expect(fetch).not.toHaveBeenCalled();
});

test("signs in without a tenant header, then shows the summary with the bearer token", async () => {
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(json({ accessToken: "token-1" }))
    .mockResolvedValueOnce(json({ items: 3, doneItems: 1 }));
  vi.stubGlobal("fetch", fetch);

  render(<AdminPage />);
  await signIn();

  expect(await screen.findByText("Items done")).toBeTruthy();
  expect(fetch).toHaveBeenNthCalledWith(1, "/api/v1/identity/token/issue", expect.objectContaining({ method: "POST" }));
  const headers = (fetch.mock.calls[0][1] as RequestInit).headers as Record<string, string>;
  expect(Object.keys(headers).map((h) => h.toLowerCase())).not.toContain("tenant");
  expect(fetch).toHaveBeenNthCalledWith(2, "/api/admin/summary", { headers: { Authorization: "Bearer token-1" } });
});

test("picks the app's one tenant when the server asks for a tenant choice", async () => {
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(
      json({
        tenantSelectionRequired: true,
        selectionToken: "sel-1",
        tenants: [{ id: "app", name: "App", isDefault: true }],
      }),
    )
    .mockResolvedValueOnce(json({ accessToken: "token-3", tenantSelectionRequired: false }))
    .mockResolvedValueOnce(json({ items: 3, doneItems: 1 }));
  vi.stubGlobal("fetch", fetch);

  render(<AdminPage />);
  await signIn();

  expect(await screen.findByText("Items done")).toBeTruthy();
  expect(fetch).toHaveBeenNthCalledWith(2, "/api/v1/identity/token/select", expect.objectContaining({ method: "POST" }));
  expect(JSON.parse((fetch.mock.calls[1][1] as RequestInit).body as string)).toEqual({ selectionToken: "sel-1", tenant: "app" });
  expect(fetch).toHaveBeenNthCalledWith(3, "/api/admin/summary", { headers: { Authorization: "Bearer token-3" } });
});

test("says so when the e-mail or the password is wrong", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json({ title: "Unauthorized" }, 401)));

  render(<AdminPage />);
  await signIn();

  expect((await screen.findByRole("alert")).textContent).toContain("wrong");
});

test("tells a signed-in user who is not staff, with the support reference", async () => {
  sessionStorage.setItem("tl-product-webapp.staff-token", "token-2");
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json({ title: "Forbidden", traceId: "abc123" }, 403)));

  render(<AdminPage />);

  const alert = (await screen.findByRole("alert")).textContent;
  expect(alert).toContain("not staff");
  expect(alert).toContain("abc123");
});

test("goes back to the sign-in when the token has expired", async () => {
  sessionStorage.setItem("tl-product-webapp.staff-token", "old");
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json({ title: "Unauthorized" }, 401)));

  render(<AdminPage />);

  expect(await screen.findByLabelText("E-mail")).toBeTruthy();
  expect(sessionStorage.getItem("tl-product-webapp.staff-token")).toBeNull();
});
