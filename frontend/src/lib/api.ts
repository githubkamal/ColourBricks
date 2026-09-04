import { apiBaseUrl } from "./config";
import { startApiCall, stopApiCall } from "./loading-bar";

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  /** RFC 9457 extension members are serialised at the top level. */
  [key: string]: unknown;
}

/** Every non-2xx response from the API maps to one of these. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  /** Field-keyed validation messages, if any (RFC 9457 `errors`). */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }

  static fromResponse(status: number, body: unknown): ApiError {
    const problem = (body ?? {}) as ProblemDetails;
    const message = problem.detail ?? problem.title ?? `Request failed with status ${status}`;
    return new ApiError(status, message, problem);
  }
}

/** The envelope every list endpoint returns (plan.md §7). */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

type UnauthorizedHandler = () => void;
let onUnauthorized: UnauthorizedHandler = () => {};

/** Wired once by the client providers to `router.replace('/login')`. */
export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler;
}

let refreshInFlight: Promise<boolean> | null = null;

function refreshOnce(): Promise<boolean> {
  refreshInFlight ??= fetch(`${apiBaseUrl}/auth/refresh`, {
    method: "POST",
    credentials: "include",
  })
    .then((res) => res.ok)
    .catch(() => false)
    .finally(() => {
      refreshInFlight = null;
    });

  return refreshInFlight;
}

async function request<T>(path: string, init: RequestInit, allowRefresh = true): Promise<T> {
  startApiCall();
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      credentials: "include",
      headers: {
        "content-type": "application/json",
        ...(init.headers ?? {}),
      },
    });

    if (response.status === 401 && allowRefresh && !path.startsWith("/auth/")) {
      const refreshed = await refreshOnce();
      if (refreshed) {
        return await request<T>(path, init, false);
      }
      onUnauthorized();
      throw new ApiError(401, "Your session has expired. Please sign in again.", {
        title: "Unauthorized",
        status: 401,
      });
    }

    if (response.status === 204) {
      return undefined as T;
    }

    const text = await response.text();
    const body: unknown = text.length > 0 ? JSON.parse(text) : undefined;

    if (!response.ok) {
      throw ApiError.fromResponse(response.status, body);
    }

    return body as T;
  } finally {
    stopApiCall();
  }
}

function assertPaged<T>(body: unknown): PagedResult<T> {
  const candidate = body as Partial<PagedResult<T>> | undefined;
  if (
    !candidate ||
    !Array.isArray(candidate.items) ||
    typeof candidate.page !== "number" ||
    typeof candidate.totalPages !== "number"
  ) {
    throw new ApiError(500, "Expected a paged list envelope from the API.");
  }
  return candidate as PagedResult<T>;
}

export const apiClient = {
  get: <T>(path: string): Promise<T> => request<T>(path, { method: "GET" }),

  post: <T>(path: string, body?: unknown): Promise<T> =>
    request<T>(path, {
      method: "POST",
      body: body === undefined ? undefined : JSON.stringify(body),
    }),

  put: <T>(path: string, body?: unknown): Promise<T> =>
    request<T>(path, {
      method: "PUT",
      body: body === undefined ? undefined : JSON.stringify(body),
    }),

  del: <T>(path: string): Promise<T> => request<T>(path, { method: "DELETE" }),

  /** GET a list endpoint and return the unwrapped, typed paged envelope. */
  list: async <T>(path: string): Promise<PagedResult<T>> =>
    assertPaged<T>(await request<unknown>(path, { method: "GET" })),
};
