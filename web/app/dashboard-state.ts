import type { WeeklyBillableRollupResponse } from "./reporting";

export type DashboardState =
  | { readonly kind: "idle" }
  | { readonly kind: "loading" }
  | {
      readonly kind: "ready";
      readonly response: WeeklyBillableRollupResponse;
    }
  | {
      readonly kind: "empty";
      readonly response: WeeklyBillableRollupResponse;
    }
  | {
      readonly kind: "blocked-range";
      readonly message: string;
    }
  | {
      readonly kind: "unauthenticated";
      readonly message: string;
    }
  | {
      readonly kind: "unavailable";
      readonly message: string;
    };

export function stateFromResponse(
  response: WeeklyBillableRollupResponse,
): DashboardState {
  return response.rows.length === 0
    ? { kind: "empty", response }
    : { kind: "ready", response };
}
