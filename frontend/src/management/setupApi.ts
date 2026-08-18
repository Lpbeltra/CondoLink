import { api } from "../services/api";
import type {
  SetupConfirmation,
  SetupDraft,
  GeneratorTower,
  SetupResidentRow,
  SetupPreview,
} from "./setupTypes";

export const previewSetup = async (condominiumId: string, draft: SetupDraft) =>
  (
    await api.post<SetupPreview>(
      `/condominiums/${condominiumId}/setup/preview`,
      draft,
    )
  ).data;

export const previewSetupImport = async (
  condominiumId: string,
  structureFile: File | null,
  residentsFile: File | null,
  noRegistrableUnits: boolean,
) => {
  const form = new FormData();
  if (structureFile) form.append("structureFile", structureFile);
  if (residentsFile) form.append("residentsFile", residentsFile);
  form.append("noRegistrableUnits", String(noRegistrableUnits));
  return (
    await api.post<SetupPreview>(
      `/condominiums/${condominiumId}/setup/import/preview`,
      form,
    )
  ).data;
};

export const previewGeneratedSetup = async (
  condominiumId: string,
  towers: GeneratorTower[],
  residents: SetupResidentRow[],
) =>
  (
    await api.post<SetupPreview>(
      `/condominiums/${condominiumId}/setup/generate/preview`,
      {
        towers: towers.map((tower) => ({
          name: tower.name,
          segments: tower.segments.map((segment) => ({
            startFloor: segment.startFloor,
            endFloor: segment.endFloor,
            unitsPerFloor: segment.unitsPerFloor,
            firstUnit: segment.firstUnit,
            digits: segment.digits,
            includeFloorNumber: segment.includeFloorNumber,
            prefix: segment.prefix,
            suffix: segment.suffix,
          })),
        })),
        residents,
      },
    )
  ).data;

export const confirmSetup = async (condominiumId: string, draft: SetupDraft) =>
  (
    await api.post<SetupConfirmation>(
      `/condominiums/${condominiumId}/setup/confirm`,
      draft,
    )
  ).data;

export const downloadSetupTemplate = async (
  condominiumId: string,
  template: "structure" | "residents",
) => {
  const response = await api.get<Blob>(
    `/condominiums/${condominiumId}/setup/templates/${template}`,
    { responseType: "blob" },
  );
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download =
    template === "structure" ? "comvy-estrutura.csv" : "comvy-moradores.xlsx";
  anchor.click();
  URL.revokeObjectURL(url);
};
