import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { AssistantSource } from "./api";
import { AssistantSources } from "./AssistantSources";

function source(documentId: string, documentName: string, pageNumber: number,
  marker: string, extra: Partial<AssistantSource> = {}): AssistantSource {
  return { documentId, documentName, pageNumber, marker, sectionTitle: null, excerpt: "Trecho", ...extra };
}

describe("AssistantSources", () => {
  it("groups pages by document and compacts consecutive ranges", () => {
    render(<AssistantSources onDownload={vi.fn()} sources={[
      source("a", "Ata AGO.pdf", 1, "S1"),
      source("a", "Ata AGO.pdf", 3, "S2"),
      source("a", "Ata AGO.pdf", 4, "S3"),
      source("a", "Ata AGO.pdf", 5, "S4"),
      source("b", "Regimento.pdf", 2, "S5"),
    ]} />);

    expect(screen.getByText("Ata AGO.pdf · págs. 1, 3–5")).toBeInTheDocument();
    expect(screen.getByText("Regimento.pdf · pág. 2")).toBeInTheDocument();
  });

  it("shows one page when several chunks cite it", () => {
    render(<AssistantSources onDownload={vi.fn()} sources={[
      source("a", "Ata.pdf", 4, "S1"), source("a", "Ata.pdf", 4, "S2"),
      source("a", "Ata.pdf", 4, "S3"),
    ]} />);

    expect(screen.getByText("Ata.pdf · pág. 4")).toBeInTheDocument();
    expect(screen.getAllByText(/Ata\.pdf/)).toHaveLength(1);
  });

  it("expands after three documents and collapses inline", async () => {
    const user = userEvent.setup();
    render(<AssistantSources onDownload={vi.fn()} sources={
      Array.from({ length: 5 }, (_, index) => source(`${index}`, `Documento ${index + 1}`, 1, `S${index + 1}`))
    } />);

    expect(screen.queryByText(/Documento 4 ·/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "+ 2 fontes" }));
    expect(screen.getByText("Documento 5 · pág. 1")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Mostrar menos" }));
    expect(screen.queryByText(/Documento 4 ·/)).not.toBeInTheDocument();
  });

  it("uses original file name when display name is technical", () => {
    render(<AssistantSources onDownload={vi.fn()} sources={[
      source("a", "s3-68f8e30b94f3b.pdf", 1, "S1",
        { originalFileName: "Ata Assembleia 09-04-2026.pdf" }),
    ]} />);

    expect(screen.getByText("Ata Assembleia 09-04-2026.pdf · pág. 1")).toBeInTheDocument();
    expect(screen.queryByText(/s3-68f8e30b94f3b/)).not.toBeInTheDocument();
  });
});
