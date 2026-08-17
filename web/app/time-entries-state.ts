import type { TimeEntryPage } from "./time-entries-api";

export type TimeEntriesState =
  | { readonly kind: "idle" }
  | { readonly kind: "loading" }
  | {
      readonly kind: "ready";
      readonly page: TimeEntryPage;
    }
  | {
      readonly kind: "empty";
      readonly page: TimeEntryPage;
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
    }
  | {
      readonly kind: "missing";
      readonly message: string;
    };

export function validateListingRange(from: string, to: string): string | null {
  if (from.length === 0 || to.length === 0 || from > to) {
    return "Choose a complete range whose start is not after its end.";
  }

  return null;
}

export function stateFromPage(page: TimeEntryPage): TimeEntriesState {
  return page.items.length === 0
    ? { kind: "empty", page }
    : { kind: "ready", page };
}
