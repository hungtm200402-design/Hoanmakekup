export const adminAuthStorageKey = "hoanmakeup-admin-auth-v1";

export type AdminAuthSession = {
  token: string;
  expiresAt: string;
  role: string;
};

export function saveAdminSession(session: AdminAuthSession, storage: Pick<Storage, "setItem"> = window.localStorage) {
  storage.setItem(adminAuthStorageKey, JSON.stringify(session));
}

export function readAdminSession(storage: Pick<Storage, "getItem"> = window.localStorage, now = new Date()) {
  const raw = storage.getItem(adminAuthStorageKey);
  if (!raw) {
    return null;
  }

  try {
    const session = JSON.parse(raw) as Partial<AdminAuthSession>;
    if (!session.token || !session.expiresAt || !session.role) {
      return null;
    }

    if (new Date(session.expiresAt).getTime() <= now.getTime()) {
      return null;
    }

    return session as AdminAuthSession;
  } catch {
    return null;
  }
}

export function clearAdminSession(storage: Pick<Storage, "removeItem"> = window.localStorage) {
  storage.removeItem(adminAuthStorageKey);
}

export function getAdminAuthHeaders(session = readAdminSession()): Record<string, string> {
  return session ? { Authorization: `Bearer ${session.token}` } : {};
}
