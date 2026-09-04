import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient, ApiError, setUnauthorizedHandler } from "./api";

function res(status: number, body?: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    text: async () => (body === undefined ? "" : JSON.stringify(body)),
  } as Response;
}

const realFetch = globalThis.fetch;

beforeEach(() => {
  setUnauthorizedHandler(() => {});
});

afterEach(() => {
  globalThis.fetch = realFetch;
  vi.restoreAllMocks();
});

describe("apiClient", () => {
  it("ApiClient_UnwrapsPagedEnvelope", async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      res(200, {
        items: [{ id: 1 }, { id: 2 }],
        page: 1,
        pageSize: 50,
        totalCount: 2,
        totalPages: 1,
      }),
    );

    const result = await apiClient.list<{ id: number }>("/things");

    expect(result.items).toHaveLength(2);
    expect(result.items[0].id).toBe(1);
    expect(result.page).toBe(1);
    expect(result.totalPages).toBe(1);
  });

  it("maps problem+json to a typed ApiError with field errors", async () => {
    globalThis.fetch = vi
      .fn()
      .mockResolvedValue(
        res(400, { title: "Validation failed", errors: { Email: ["Email is required"] } }),
      );

    const error = (await apiClient.post("/things", {}).catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.fieldErrors.Email).toEqual(["Email is required"]);
  });

  it("ApiClient_On401_RefreshesOnceThenRedirects", async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);

    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(res(401, { title: "Unauthorized" })) // GET /widgets
      .mockResolvedValueOnce(res(401)); // POST /auth/refresh -> still failing
    globalThis.fetch = fetchMock;

    const error = (await apiClient.get("/widgets").catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(401);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/auth/refresh"))).toBe(true);
  });

  it("refreshes at most once for concurrent 401s, then retries successfully", async () => {
    setUnauthorizedHandler(vi.fn());

    let protectedCalls = 0;
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (String(url).endsWith("/auth/refresh")) return Promise.resolve(res(200));
      protectedCalls += 1;
      return Promise.resolve(protectedCalls <= 2 ? res(401) : res(200, { ok: true }));
    });
    globalThis.fetch = fetchMock;

    const [a, b] = await Promise.all([apiClient.get("/w"), apiClient.get("/w")]);

    expect(a).toEqual({ ok: true });
    expect(b).toEqual({ ok: true });
    const refreshCalls = fetchMock.mock.calls.filter(([url]) =>
      String(url).endsWith("/auth/refresh"),
    );
    expect(refreshCalls).toHaveLength(1);
  });
});
