import { developmentTokenHeaders } from "./token-session";

export interface ClientDto {
  readonly clientId: number;
  readonly clientCode: string;
  readonly name: string;
  readonly isActive: boolean;
  readonly createdAtUtc: string;
}

export interface MatterDto {
  readonly matterId: number;
  readonly clientId: number;
  readonly matterNumber: string;
  readonly name: string;
  readonly isBillableByDefault: boolean;
  readonly isActive: boolean;
  readonly createdAtUtc: string;
}

export interface TimekeeperDto {
  readonly userId: number;
  readonly email: string;
  readonly fullName: string;
  readonly defaultHourlyRate: number;
  readonly isActive: boolean;
}

export interface PartyPage<T> {
  readonly skip: number;
  readonly take: number;
  readonly total: number;
  readonly items: readonly T[];
}

export type ClientPage = PartyPage<ClientDto>;
export type MatterPage = PartyPage<MatterDto>;
export type TimekeeperPage = PartyPage<TimekeeperDto>;

export type PartyPageSize = 20 | 50 | 100;

export const partyPageSizes: readonly PartyPageSize[] = [20, 50, 100];

export type ClientStatusFilter = "all" | "active" | "inactive";

export type PartyFailureKind =
  | "unauthorized"
  | "unavailable"
  | "missing"
  | "missing-parent"
  | "conflict"
  | "malformed";

export class PartyRequestError extends Error {
  public constructor(
    public readonly kind: PartyFailureKind,
    message: string,
    public readonly conflictingField: string | null = null,
    public readonly conflictingValue: string | null = null,
  ) {
    super(message);
    this.name = "PartyRequestError";
  }
}

export interface ListClientsQuery {
  readonly status: ClientStatusFilter;
  readonly skip: number;
  readonly take: PartyPageSize;
}

export interface ListMattersQuery {
  readonly clientId: number;
  readonly skip: number;
  readonly take: PartyPageSize;
}

export interface ListTimekeepersQuery {
  readonly skip: number;
  readonly take: PartyPageSize;
}

export interface RegisterClientRequest {
  readonly clientCode: string;
  readonly name: string;
}

export interface CorrectClientRequest {
  readonly name: string;
  readonly isActive: boolean;
}

export interface OpenMatterRequest {
  readonly matterNumber: string;
  readonly name: string;
  readonly isBillableByDefault: boolean;
}

export interface CorrectMatterRequest {
  readonly name: string;
  readonly isBillableByDefault: boolean;
  readonly isActive: boolean;
}

const clientsRoute = "/api/v1/clients";
const mattersRoute = "/api/v1/matters";
const usersRoute = "/api/v1/users";

export function formatActiveFlag(isActive: boolean): string {
  return isActive ? "Active" : "Inactive";
}

export function formatUtcDate(iso: string): string {
  return iso.slice(0, 10);
}

export async function listClients(
  query: ListClientsQuery,
  token: string,
  signal?: AbortSignal,
): Promise<ClientPage> {
  const params = new URLSearchParams({
    skip: String(query.skip),
    take: String(query.take),
  });

  if (query.status === "active") {
    params.set("isActive", "true");
  } else if (query.status === "inactive") {
    params.set("isActive", "false");
  }

  const response = await send(`${clientsRoute}?${params.toString()}`, token, signal);
  return (await response.json()) as ClientPage;
}

export async function getClient(
  clientId: number,
  token: string,
  signal?: AbortSignal,
): Promise<ClientDto> {
  const response = await send(`${clientsRoute}/${clientId}`, token, signal);
  return (await response.json()) as ClientDto;
}

export async function listMattersForClient(
  query: ListMattersQuery,
  token: string,
  signal?: AbortSignal,
): Promise<MatterPage> {
  const params = new URLSearchParams({
    skip: String(query.skip),
    take: String(query.take),
  });
  const response = await send(
    `${clientsRoute}/${query.clientId}/matters?${params.toString()}`,
    token,
    signal,
  );
  return (await response.json()) as MatterPage;
}

export async function listTimekeepers(
  query: ListTimekeepersQuery,
  token: string,
  signal?: AbortSignal,
): Promise<TimekeeperPage> {
  const params = new URLSearchParams({
    skip: String(query.skip),
    take: String(query.take),
  });
  const response = await send(`${usersRoute}?${params.toString()}`, token, signal);
  return (await response.json()) as TimekeeperPage;
}

export async function getTimekeeper(
  userId: number,
  token: string,
  signal?: AbortSignal,
): Promise<TimekeeperDto> {
  const response = await send(`${usersRoute}/${userId}`, token, signal);
  return (await response.json()) as TimekeeperDto;
}

export async function registerClient(
  request: RegisterClientRequest,
  token: string,
): Promise<ClientDto> {
  const response = await sendWrite("POST", clientsRoute, request, token, "client");
  return (await response.json()) as ClientDto;
}

export async function correctClient(
  clientId: number,
  request: CorrectClientRequest,
  token: string,
): Promise<ClientDto> {
  const response = await sendWrite(
    "PUT",
    `${clientsRoute}/${clientId}`,
    request,
    token,
    "client",
  );
  return (await response.json()) as ClientDto;
}

export async function openMatter(
  clientId: number,
  request: OpenMatterRequest,
  token: string,
): Promise<MatterDto> {
  const response = await sendWrite(
    "POST",
    `${clientsRoute}/${clientId}/matters`,
    request,
    token,
    "open-matter",
  );
  return (await response.json()) as MatterDto;
}

export async function correctMatter(
  matterId: number,
  request: CorrectMatterRequest,
  token: string,
): Promise<MatterDto> {
  const response = await sendWrite(
    "PUT",
    `${mattersRoute}/${matterId}`,
    request,
    token,
    "matter",
  );
  return (await response.json()) as MatterDto;
}

async function send(
  url: string,
  token: string,
  signal?: AbortSignal,
): Promise<Response> {
  let response: Response;

  try {
    response = await fetch(url, {
      headers: developmentTokenHeaders(token),
      signal,
    });
  } catch {
    throw new PartyRequestError(
      "unavailable",
      "The directory service could not be reached.",
    );
  }

  return interpretRead(response);
}

async function sendWrite(
  method: "POST" | "PUT",
  path: string,
  body:
    | RegisterClientRequest
    | CorrectClientRequest
    | OpenMatterRequest
    | CorrectMatterRequest,
  token: string,
  surface: "client" | "matter" | "open-matter",
): Promise<Response> {
  let response: Response;

  try {
    response = await fetch(path, {
      body: JSON.stringify(body),
      headers: {
        ...developmentTokenHeaders(token),
        "Content-Type": "application/json",
      },
      method,
    });
  } catch {
    throw new PartyRequestError(
      "unavailable",
      "The directory service could not be reached.",
    );
  }

  return interpretWrite(response, surface);
}

function interpretRead(response: Response): Response {
  if (response.status === 401) {
    throw new PartyRequestError(
      "unauthorized",
      "Your development session is missing or has expired.",
    );
  }

  if (response.status === 404) {
    throw new PartyRequestError("missing", "That record is no longer there.");
  }

  if (!response.ok) {
    throw new PartyRequestError(
      "unavailable",
      "The directory is temporarily unavailable.",
    );
  }

  return response;
}

async function interpretWrite(
  response: Response,
  surface: "client" | "matter" | "open-matter",
): Promise<Response> {
  if (response.status === 401) {
    throw new PartyRequestError(
      "unauthorized",
      "Your development session is missing or has expired.",
    );
  }

  if (response.status === 404) {
    if (surface === "open-matter") {
      throw new PartyRequestError(
        "missing-parent",
        "That client is no longer there, so a matter cannot be opened under it.",
      );
    }

    throw new PartyRequestError("missing", "That record is no longer there.");
  }

  if (response.status === 409) {
    const conflict = await readConflict(response);
    throw new PartyRequestError(
      "conflict",
      conflict.detail,
      conflict.conflictingField,
      conflict.conflictingValue,
    );
  }

  if (response.status === 400) {
    const detail = await readProblemDetail(response);
    throw new PartyRequestError(
      "malformed",
      detail ?? "This form has a value the service cannot accept.",
    );
  }

  if (response.status === 201 || response.status === 200) {
    return response;
  }

  throw new PartyRequestError(
    "unavailable",
    "The directory is temporarily unavailable.",
  );
}

async function readConflict(response: Response): Promise<{
  readonly detail: string;
  readonly conflictingField: string | null;
  readonly conflictingValue: string | null;
}> {
  try {
    const payload = (await response.json()) as {
      readonly detail?: string;
      readonly conflictingField?: string;
      readonly conflictingValue?: string;
    };
    return {
      conflictingField: payload.conflictingField ?? null,
      conflictingValue: payload.conflictingValue ?? null,
      detail:
        payload.detail ??
        "This value is already in use.",
    };
  } catch {
    return {
      conflictingField: null,
      conflictingValue: null,
      detail: "This value is already in use.",
    };
  }
}

async function readProblemDetail(response: Response): Promise<string | null> {
  try {
    const payload = (await response.json()) as { readonly detail?: string };
    return payload.detail ?? null;
  } catch {
    return null;
  }
}
