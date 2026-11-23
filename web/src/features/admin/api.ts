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

export async function signIn(email: string, password: string): Promise<string> {
  const path = "/api/v1/identity/token/issue";
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    throw await failure("POST", path, response);
  }
  return ((await response.json()) as { accessToken: string }).accessToken;
}

export async function getSummary(token: string): Promise<StaffSummary> {
  const path = "/api/admin/summary";
  const response = await fetch(path, { headers: { Authorization: `Bearer ${token}` } });
  if (!response.ok) {
    throw await failure("GET", path, response);
  }
  return (await response.json()) as StaffSummary;
}
