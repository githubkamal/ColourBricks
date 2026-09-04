import { setupServer } from "msw/node";
import { handlers } from "./handlers";

/** Shared MSW server for component/unit tests (P0-T09). */
export const server = setupServer(...handlers);
