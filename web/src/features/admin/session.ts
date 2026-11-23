// The staff member's access token, kept for this browser tab only: sessionStorage is gone when the tab closes,
// and nothing else reads it. The page works without storage (a private window): the member signs in again.
const KEY = "tl-product-webapp.staff-token";

export function readToken(): string | undefined {
  try {
    return sessionStorage.getItem(KEY) ?? undefined;
  } catch {
    return undefined;
  }
}

export function saveToken(token: string | undefined): void {
  try {
    if (token) {
      sessionStorage.setItem(KEY, token);
    } else {
      sessionStorage.removeItem(KEY);
    }
  } catch {
    // Storage is unavailable: the token lives in the page only.
  }
}
