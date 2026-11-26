// Calls the staff area of the API: the sign-in of the framework's Identity module, and the staff endpoints
// under /api/admin, at the relative path /api like every call of the app. Single-tenant mode takes no
// `tenant` header at sign-in.

export interface StaffSummary {
  items: number;
  doneItems: number;
}

/** A failed call: the status, and the trace id of the ProblemDetails answer, which support quotes. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly traceId: string | undefined,
    message: string,
  ) {
    super(message);
  }
}

async function failure(method: string, path: string, response: Response): Promise<ApiError> {
  let traceId: string | undefined;
  try {
    traceId = ((await response.json()) as { traceId?: string }).traceId;
  } catch {
    // An answer without a JSON body has no trace id.
  }
  return new ApiError(response.status, traceId, `${method} ${path}: ${response.status}`);
}

/**
 * The answer of the sign-in: the tokens, or, for an account that is a member of several tenants, a selection
 * token and the tenants to choose from. Accounts are global and a membership gives one access to a tenant.
 */
interface SignInResponse {
  accessToken?: string | null;
  tenantSelectionRequired: boolean;
  selectionToken?: string | null;
  tenants?: { id: string; name: string; isDefault: boolean }[] | null;
}

async function post(path: string, body: unknown): Promise<SignInResponse> {
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw await failure("POST", path, response);
  }
  return (await response.json()) as SignInResponse;
}

export async function signIn(email: string, password: string): Promise<string> {
  let answer = await post("/api/v1/identity/token/issue", { email, password });
  // Single-tenant mode has one tenant, so the server answers with the tokens. If it ever asks for a choice,
  // the only membership the account can have here is this app's tenant: choose it.
  if (!answer.accessToken && answer.tenantSelectionRequired && answer.selectionToken && answer.tenants?.length === 1) {
    answer = await post("/api/v1/identity/token/select", {
      selectionToken: answer.selectionToken,
      tenant: answer.tenants[0].id,
    });
  }
  if (!answer.accessToken) {
    throw new ApiError(401, undefined, "POST /api/v1/identity/token/issue: no access token");
  }
  return answer.accessToken;
}

export async function getSummary(token: string): Promise<StaffSummary> {
  const path = "/api/admin/summary";
  const response = await fetch(path, { headers: { Authorization: `Bearer ${token}` } });
  if (!response.ok) {
    throw await failure("GET", path, response);
  }
  return (await response.json()) as StaffSummary;
}
