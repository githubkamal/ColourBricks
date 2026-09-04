import { describe, expect, it } from "vitest";
import { HttpResponse, http } from "msw";
import { server } from "@/test/msw/server";
import { apiBaseUrl } from "@/lib/config";
import { ApiError } from "./api";
import { fetchCurrentUser, login } from "./auth";

describe("auth (via MSW shared handlers)", () => {
  it("fetchCurrentUser returns the current user with permissions", async () => {
    const user = await fetchCurrentUser();

    expect(user.email).toBe("admin@colourbricks.local");
    expect(user.permissions).toEqual(["*"]);
  });

  it("login surfaces a 401 as a typed ApiError", async () => {
    server.use(
      http.post(`${apiBaseUrl}/auth/login`, () =>
        HttpResponse.json({ title: "Invalid credentials" }, { status: 401 }),
      ),
    );

    await expect(login("admin@colourbricks.local", "wrong")).rejects.toMatchObject({
      name: "ApiError",
      status: 401,
    });
    await expect(login("admin@colourbricks.local", "wrong")).rejects.toBeInstanceOf(ApiError);
  });
});
