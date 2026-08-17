import type { ClientPage, MatterPage, TimekeeperPage } from "./parties-api";

export type DirectoryState<TPage> =
  | { readonly kind: "idle" }
  | { readonly kind: "loading" }
  | {
      readonly kind: "ready";
      readonly page: TPage;
    }
  | {
      readonly kind: "empty";
      readonly page: TPage;
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
    }
  | {
      readonly kind: "missing-parent";
      readonly message: string;
    };

export type ClientsState = DirectoryState<ClientPage>;
export type MattersState = DirectoryState<MatterPage>;
export type TimekeepersState = DirectoryState<TimekeeperPage>;

export function stateFromClientPage(page: ClientPage): ClientsState {
  return page.items.length === 0
    ? { kind: "empty", page }
    : { kind: "ready", page };
}

export function stateFromMatterPage(page: MatterPage): MattersState {
  return page.items.length === 0
    ? { kind: "empty", page }
    : { kind: "ready", page };
}

export function stateFromTimekeeperPage(
  page: TimekeeperPage,
): TimekeepersState {
  return page.items.length === 0
    ? { kind: "empty", page }
    : { kind: "ready", page };
}
