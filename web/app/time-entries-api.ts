import { developmentTokenHeaders } from "./token-session";

export interface TimeEntryDto {
  readonly timeEntryId: number;
  readonly userId: number;
  readonly matterId: number;
  readonly workDate: string;
  readonly durationMinutes: number;
  readonly isBillable: boolean;
  readonly hourlyRateSnapshot: number;
  readonly narrative: string;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
}

export interface TimeEntryPage {
  readonly skip: number;
  readonly take: number;
  readonly total: number;
  readonly items: readonly TimeEntryDto[];
}

export interface DomainRuleViolation {
  readonly rule: string;
  readonly offendingValue: string;
  readonly detail: string;
}

export type TimeEntryPageSize = 20 | 50 | 100;

export type TimeEntryFailureKind =
  | "unauthorized"
  | "unavailable"
  | "missing"
  | "refused";

export class TimeEntryRequestError extends Error {
  public constructor(
    public readonly kind: TimeEntryFailureKind,
    message: string,
    public readonly violations: readonly DomainRuleViolation[] = [],
  ) {
    super(message);
    this.name = "TimeEntryRequestError";
  }
}

export interface ListTimeEntriesQuery {
  readonly from: string;
  readonly to: string;
  readonly userId?: number;
  readonly matterId?: number;
  readonly skip: number;
  readonly take: TimeEntryPageSize;
}

export interface RecordTimeEntryRequest {
  readonly userId: number;
  readonly matterId: number;
  readonly workDate: string;
  readonly durationMinutes: number;
  readonly isBillable: boolean;
  readonly narrative: string;
}

export interface ReviseTimeEntryRequest {
  readonly matterId: number;
  readonly workDate: string;
  readonly durationMinutes: number;
  readonly isBillable: boolean;
  readonly narrative: string;
}

const hoursFormatter = new Intl.NumberFormat("en-US", {
  maximumFractionDigits: 1,
  minimumFractionDigits: 1,
});

const timeEntriesRoute = "/api/v1/time-entries";

export function formatDurationHours(durationMinutes: number): string {
  return hoursFormatter.format(durationMinutes / 60);
}

export async function listTimeEntries(
  query: ListTimeEntriesQuery,
  token: string,
  signal?: AbortSignal,
): Promise<TimeEntryPage> {
  const params = new URLSearchParams({
    from: query.from,
    to: query.to,
    skip: String(query.skip),
    take: String(query.take),
  });

  if (query.userId !== undefined) {
    params.set("userId", String(query.userId));
  }

  if (query.matterId !== undefined) {
    params.set("matterId", String(query.matterId));
  }

  const response = await send(timeEntriesRoute, params, token, signal);
  return (await response.json()) as TimeEntryPage;
}

export async function getTimeEntry(
  timeEntryId: number,
  token: string,
  signal?: AbortSignal,
): Promise<TimeEntryDto> {
  const response = await send(
    `${timeEntriesRoute}/${timeEntryId}`,
    null,
    token,
    signal,
  );
  return (await response.json()) as TimeEntryDto;
}

export async function recordTimeEntry(
  request: RecordTimeEntryRequest,
  token: string,
): Promise<TimeEntryDto> {
  const response = await sendWrite("POST", timeEntriesRoute, request, token);
  return (await response.json()) as TimeEntryDto;
}

export async function reviseTimeEntry(
  timeEntryId: number,
  request: ReviseTimeEntryRequest,
  token: string,
): Promise<TimeEntryDto> {
  const response = await sendWrite(
    "PUT",
    `${timeEntriesRoute}/${timeEntryId}`,
    request,
    token,
  );
  return (await response.json()) as TimeEntryDto;
}

export async function deleteTimeEntry(
  timeEntryId: number,
  token: string,
): Promise<void> {
  await sendWrite("DELETE", `${timeEntriesRoute}/${timeEntryId}`, null, token);
}

async function send(
  path: string,
  params: URLSearchParams | null,
  token: string,
  signal?: AbortSignal,
): Promise<Response> {
  const url = params === null ? path : `${path}?${params.toString()}`;
  let response: Response;

  try {
    response = await fetch(url, {
      headers: developmentTokenHeaders(token),
      signal,
    });
  } catch {
    throw new TimeEntryRequestError(
      "unavailable",
      "The time-entry service could not be reached.",
    );
  }

  return interpretRead(response);
}

async function sendWrite(
  method: "POST" | "PUT" | "DELETE",
  path: string,
  body: RecordTimeEntryRequest | ReviseTimeEntryRequest | null,
  token: string,
): Promise<Response> {
  let response: Response;

  try {
    response = await fetch(path, {
      body: body === null ? undefined : JSON.stringify(body),
      headers: {
        ...developmentTokenHeaders(token),
        ...(body === null ? {} : { "Content-Type": "application/json" }),
      },
      method,
    });
  } catch {
    throw new TimeEntryRequestError(
      "unavailable",
      "The time-entry service could not be reached.",
    );
  }

  return interpretWrite(response);
}

function interpretRead(response: Response): Response {
  if (response.status === 401) {
    throw new TimeEntryRequestError(
      "unauthorized",
      "Your development session is missing or has expired.",
    );
  }

  if (response.status === 404) {
    throw new TimeEntryRequestError(
      "missing",
      "That time entry is no longer there.",
    );
  }

  if (!response.ok) {
    throw new TimeEntryRequestError(
      "unavailable",
      "Time entries are temporarily unavailable.",
    );
  }

  return response;
}

async function interpretWrite(response: Response): Promise<Response> {
  if (response.status === 401) {
    throw new TimeEntryRequestError(
      "unauthorized",
      "Your development session is missing or has expired.",
    );
  }

  if (response.status === 404) {
    throw new TimeEntryRequestError(
      "missing",
      "That time entry is no longer there.",
    );
  }

  if (response.status === 400) {
    const violations = await readViolations(response);
    const detail =
      violations.map((violation) => violation.detail).join(" ") ||
      "The service refused this submission.";
    throw new TimeEntryRequestError("refused", detail, violations);
  }

  if (response.status === 201 || response.status === 200) {
    return response;
  }

  if (response.status === 204) {
    return response;
  }

  throw new TimeEntryRequestError(
    "unavailable",
    "Time entries are temporarily unavailable.",
  );
}

async function readViolations(
  response: Response,
): Promise<readonly DomainRuleViolation[]> {
  try {
    const payload = (await response.json()) as {
      readonly violations?: readonly DomainRuleViolation[];
    };
    return payload.violations ?? [];
  } catch {
    return [];
  }
}
