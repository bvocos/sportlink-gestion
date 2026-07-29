import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock("@/shared/api/httpClient", () => ({
  http: {
    get: mocks.get,
    post: mocks.post,
  },
}));

import { auth, type Session } from "./auth";

const session: Session = {
  id: "1",
  nombre: "Administrador",
  usuario: "admin",
  rol: "Administrador",
  permisos: ["dashboard"],
  debeCambiarPassword: false,
};

describe("auth.login", () => {
  beforeEach(() => {
    mocks.get.mockReset();
    mocks.post.mockReset();
    auth.state.user = null;
    auth.state.checked = false;
  });

  it("confirma la cookie con /me antes de informar la sesión autenticada", async () => {
    mocks.post.mockResolvedValue({ data: session });
    mocks.get.mockResolvedValue({ data: session });

    const result = await auth.login("admin", "secreto");

    expect(mocks.post).toHaveBeenCalledWith("/auth/login", {
      usuario: "admin",
      password: "secreto",
    });
    expect(mocks.get).toHaveBeenCalledWith("/auth/me", {
      headers: { "Cache-Control": "no-cache" },
    });
    expect(mocks.post.mock.invocationCallOrder[0]!).toBeLessThan(
      mocks.get.mock.invocationCallOrder[0]!,
    );
    expect(result).toEqual(session);
    expect(auth.state.user).toEqual(session);
    expect(auth.state.checked).toBe(true);
  });
});
