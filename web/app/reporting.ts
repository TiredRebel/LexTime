import { developmentTokenHeaders } from "./token-session";

export interface WeeklyBillableRollupRow {
  readonly isoYear: number;
  readonly isoWeek: number;
  readonly weekStartDate: string;
  readonly clientId: number;
  readonly clientCode: string;
  readonly clientName: string;
  readonly billableHours: number;
  readonly nonBillableHours: number;
  readonly billableAmount: number;
  readonly cumulativeBillableHours: number;
  readonly hoursDeltaVsPriorWeek: number | null;
  readonly clientRankInWeek: number;
}

export interface WeeklyBillableRollupResponse {
  readonly from: string;
  readonly to: string;
  readonly rows: readonly WeeklyBillableRollupRow[];
}

export type RollupFailureKind = "unauthorized" | "unavailable";

export class RollupRequestError extends Error {
  public constructor(
    public readonly kind: RollupFailureKind,
    message: string,
  ) {
    super(message);
    this.name = "RollupRequestError";
  }
}

const currencyFormatter = new Intl.NumberFormat("en-US", {
  currency: "USD",
  maximumFractionDigits: 0,
  style: "currency",
});

const hoursFormatter = new Intl.NumberFormat("en-US", {
  maximumFractionDigits: 1,
  minimumFractionDigits: 1,
});

const dateFormatter = new Intl.DateTimeFormat("en-US", {
  day: "numeric",
  month: "short",
  timeZone: "UTC",
});

export async function fetchWeeklyRollup(
  from: string,
  to: string,
  token: string,
  signal?: AbortSignal,
): Promise<WeeklyBillableRollupResponse> {
  let response: Response;

  try {
    const query = new URLSearchParams({ from, to });
    response = await fetch(
      `/api/v1/reports/weekly-billable-rollup?${query.toString()}`,
      {
        headers: developmentTokenHeaders(token),
        signal,
      },
    );
  } catch {
    throw new RollupRequestError(
      "unavailable",
      "The report service could not be reached.",
    );
  }

  if (response.status === 401) {
    throw new RollupRequestError(
      "unauthorized",
      "Your development session is missing or has expired.",
    );
  }

  if (!response.ok) {
    throw new RollupRequestError(
      "unavailable",
      "The report is temporarily unavailable.",
    );
  }

  return (await response.json()) as WeeklyBillableRollupResponse;
}

export function formatCurrency(value: number): string {
  return currencyFormatter.format(value);
}

export function formatHours(value: number): string {
  return hoursFormatter.format(value);
}

export function formatWeek(row: WeeklyBillableRollupRow): string {
  const weekStart = new Date(`${row.weekStartDate}T00:00:00Z`);
  return `W${row.isoWeek} · ${dateFormatter.format(weekStart)}`;
}
