import { developmentTokenHeaders } from "./token-session";
import { TimeEntryRequestError } from "./time-entries-api";

export interface ClientDto {
  readonly clientId: number;
  readonly clientCode: string;
  readonly name: string;
  readonly isActive: boolean;
}

export interface MatterDto {
  readonly matterId: number;
  readonly clientId: number;
  readonly matterNumber: string;
  readonly name: string;
  readonly isBillableByDefault: boolean;
  readonly isActive: boolean;
}

export interface TimekeeperDto {
  readonly userId: number;
  readonly email: string;
  readonly fullName: string;
  readonly defaultHourlyRate: number;
  readonly isActive: boolean;
}

interface PartyPage<T> {
  readonly items: readonly T[];
}

const matterCache = new Map<number, MatterDto>();

export function partyLabel(
  name: string,
  isActive: boolean,
  extra?: string,
): string {
  const base = extra === undefined ? name : `${extra} · ${name}`;
  return isActive ? base : `${base} (inactive)`;
}

export async function listTimekeepers(
  token: string,
): Promise<readonly TimekeeperDto[]> {
  const page = await getJson<PartyPage<TimekeeperDto>>(
    "/api/v1/users?take=200",
    token,
  );
  return page.items;
}

export async function listClients(
  token: string,
): Promise<readonly ClientDto[]> {
  const page = await getJson<PartyPage<ClientDto>>(
    "/api/v1/clients?take=200",
    token,
  );
  return page.items;
}

export async function listMattersForClient(
  clientId: number,
  token: string,
): Promise<readonly MatterDto[]> {
  const page = await getJson<PartyPage<MatterDto>>(
    `/api/v1/clients/${clientId}/matters?take=200`,
    token,
  );

  for (const matter of page.items) {
    matterCache.set(matter.matterId, matter);
  }

  return page.items;
}

export async function getMatter(
  matterId: number,
  token: string,
): Promise<MatterDto | null> {
  const cached = matterCache.get(matterId);
  if (cached !== undefined) {
    return cached;
  }

  try {
    const matter = await getJson<MatterDto>(
      `/api/v1/matters/${matterId}`,
      token,
    );
    matterCache.set(matter.matterId, matter);
    return matter;
  } catch (error) {
    if (error instanceof TimeEntryRequestError && error.kind === "missing") {
      return null;
    }

    throw error;
  }
}

async function getJson<T>(path: string, token: string): Promise<T> {
  let response: Response;

  try {
    response = await fetch(path, {
      headers: developmentTokenHeaders(token),
    });
  } catch {
    throw new TimeEntryRequestError(
      "unavailable",
      "The directory service could not be reached.",
    );
  }

  if (response.status === 401) {
    throw new TimeEntryRequestError(
      "unauthorized",
      "Your development session is missing or has expired.",
    );
  }

  if (response.status === 404) {
    throw new TimeEntryRequestError(
      "missing",
      "That record is no longer there.",
    );
  }

  if (!response.ok) {
    throw new TimeEntryRequestError(
      "unavailable",
      "The directory is temporarily unavailable.",
    );
  }

  return (await response.json()) as T;
}
