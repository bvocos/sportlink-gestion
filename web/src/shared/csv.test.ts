import { afterEach, describe, expect, it, vi } from "vitest";
import { downloadCsv } from "./csv";

describe("downloadCsv", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("genera un archivo CSV real con MIME, extensión y contenido correctos", async () => {
    let generatedBlob: Blob | undefined;
    const link = { href: "", download: "", click: vi.fn() };
    vi.stubGlobal("document", { createElement: vi.fn(() => link) });
    vi.stubGlobal("URL", {
      createObjectURL: vi.fn((blob: Blob) => {
        generatedBlob = blob;
        return "blob:csv-test";
      }),
      revokeObjectURL: vi.fn(),
    });

    downloadCsv("reporte.csv", ["Cliente", "Importe"], [["Club, Norte", 1500.5]]);

    expect(link.download).toBe("reporte.csv");
    expect(link.click).toHaveBeenCalledOnce();
    expect(generatedBlob?.type).toBe("text/csv;charset=utf-8");
    const content = await generatedBlob!.text();
    expect(content).toContain('"Cliente";"Importe"');
    expect(content).toContain('"Club, Norte";"1500.5"');
    expect(content).not.toContain("<html");
  });
});
