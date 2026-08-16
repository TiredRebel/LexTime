const tokenStorageKey = "lextime.development-token";

export function readDevelopmentToken(): string | null {
  try {
    return sessionStorage.getItem(tokenStorageKey);
  } catch {
    return null;
  }
}

export function saveDevelopmentToken(token: string): void {
  const normalizedToken = token.trim();

  if (normalizedToken.length === 0) {
    clearDevelopmentToken();
    return;
  }

  try {
    sessionStorage.setItem(tokenStorageKey, normalizedToken);
  } catch {
    // The in-memory React state still provides a session until this tab reloads.
  }
}

export function clearDevelopmentToken(): void {
  try {
    sessionStorage.removeItem(tokenStorageKey);
  } catch {
    // A blocked storage API already behaves like a signed-out session.
  }
}

export function developmentTokenHeaders(token: string): HeadersInit {
  return {
    Authorization: `Bearer ${token}`,
  };
}
