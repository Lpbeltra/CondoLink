import { describe, expect, it } from "vitest";
import {
  appendUploadedAttachments,
  calculateUploadProgress,
  getAttachmentErrorMessage,
  removeSelectedAttachment,
  removeUploadedAttachment,
  selectAttachmentFiles,
} from "./attachments";
import type { RequestAttachment } from "./types";

function file(name: string, type: string, size = 1024) {
  return { name, type, size } as File;
}

function attachment(id: string): RequestAttachment {
  return {
    id,
    requestId: "request-id",
    originalFileName: `${id}.pdf`,
    contentType: "application/pdf",
    fileSize: 1024,
    uploadedBy: { id: "user-id", fullName: "Usuário" },
    createdAt: "2026-07-27T10:00:00Z",
    contentUrl: `/request-attachments/${id}/content`,
  };
}

describe("request attachment selection", () => {
  it("accepts multiple supported images and PDFs", () => {
    const result = selectAttachmentFiles(
      [],
      [
        file("foto.jpg", "image/jpeg"),
        file("planta.png", "image/png"),
        file("documento.pdf", "application/pdf"),
      ],
    );

    expect(result.error).toBeNull();
    expect(result.files).toHaveLength(3);
  });

  it("preserves the previous selection when more than ten files are selected", () => {
    const current = [file("existente.pdf", "application/pdf")];
    const incoming = Array.from({ length: 10 }, (_, index) =>
      file(`${index}.pdf`, "application/pdf"),
    );

    expect(selectAttachmentFiles(current, incoming)).toEqual({
      files: current,
      error: "É permitido enviar no máximo 10 arquivos.",
    });
  });

  it("rejects a file larger than 15 MB without partially selecting the batch", () => {
    const result = selectAttachmentFiles(
      [],
      [
        file("válido.pdf", "application/pdf"),
        file("grande.pdf", "application/pdf", 15 * 1024 * 1024 + 1),
      ],
    );

    expect(result.files).toEqual([]);
    expect(result.error).toBe("Cada arquivo pode possuir no máximo 15 MB.");
  });

  it("rejects unsupported formats and mismatched MIME types", () => {
    expect(
      selectAttachmentFiles(
        [],
        [file("arquivo.exe", "application/octet-stream")],
      ).error,
    ).toBe("Formato não suportado. Envie somente JPG, PNG, WebP ou PDF.");

    expect(
      selectAttachmentFiles([], [file("falso.pdf", "image/jpeg")]).error,
    ).toBe("Formato não suportado. Envie somente JPG, PNG, WebP ou PDF.");
  });

  it("removes a selected file immediately", () => {
    const files = [
      file("primeiro.pdf", "application/pdf"),
      file("segundo.pdf", "application/pdf"),
    ];

    expect(removeSelectedAttachment(files, 0)).toEqual([files[1]]);
  });
});

describe("request attachment upload presentation", () => {
  it("calculates bounded upload percentages", () => {
    expect(calculateUploadProgress(0, 100)).toBe(0);
    expect(calculateUploadProgress(42, 100)).toBe(42);
    expect(calculateUploadProgress(150, 100)).toBe(100);
    expect(calculateUploadProgress(1, undefined)).toBe(0);
  });

  it("updates the local list after upload and deletion", () => {
    const first = attachment("first");
    const second = attachment("second");
    const uploaded = appendUploadedAttachments([first], [second]);

    expect(uploaded).toEqual([first, second]);
    expect(removeUploadedAttachment(uploaded, first.id)).toEqual([second]);
  });

  it("shows the useful server message without exposing ProblemDetails", () => {
    expect(
      getAttachmentErrorMessage({
        isAxiosError: true,
        response: {
          status: 400,
          data: { error: "Formato não suportado." },
        },
      }),
    ).toBe("Formato não suportado.");

    expect(
      getAttachmentErrorMessage({
        isAxiosError: true,
        response: {
          status: 403,
          data: { title: "Forbidden", detail: "internal detail" },
        },
      }),
    ).toBe(
      "Você não possui permissão para acessar os anexos desta solicitação.",
    );
  });
});
