import { useMemo, useState } from "react";
import { Button, Chip, Stack, Tooltip, Typography } from "@mui/material";
import type { AssistantSource } from "./api";

interface SourceGroup {
  documentId: string;
  documentName: string;
  pages: number[];
  sources: AssistantSource[];
  documentExists: boolean;
  documentCurrentlyActive: boolean;
}

const technicalName = /^(?:s3[-_/]|[0-9a-f]{16,}(?:\.[a-z0-9]+)?$|[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?:\.[a-z0-9]+)?$)/i;

function friendlySourceName(source: AssistantSource) {
  return technicalName.test(source.documentName) && source.originalFileName
    ? source.originalFileName
    : source.documentName;
}

function compactPages(pages: number[]) {
  const sorted = [...new Set(pages)].sort((left, right) => left - right);
  const ranges: string[] = [];
  for (let index = 0; index < sorted.length;) {
    let end = index;
    while (end + 1 < sorted.length && sorted[end + 1] === sorted[end] + 1) end++;
    ranges.push(index === end ? `${sorted[index]}` : `${sorted[index]}–${sorted[end]}`);
    index = end + 1;
  }
  return ranges.join(", ");
}

function groupAssistantSources(sources: AssistantSource[]): SourceGroup[] {
  const groups = new Map<string, SourceGroup>();
  for (const source of sources) {
    const current = groups.get(source.documentId);
    if (current) {
      current.sources.push(source);
      if (source.pageNumber != null && !current.pages.includes(source.pageNumber)) current.pages.push(source.pageNumber);
      current.documentExists ||= source.documentExists !== false;
      current.documentCurrentlyActive ||= source.documentCurrentlyActive !== false;
      continue;
    }
    groups.set(source.documentId, {
      documentId: source.documentId,
      documentName: friendlySourceName(source),
      pages: source.pageNumber == null ? [] : [source.pageNumber],
      sources: [source],
      documentExists: source.documentExists !== false,
      documentCurrentlyActive: source.documentCurrentlyActive !== false,
    });
  }
  return [...groups.values()];
}

export function AssistantSources({ sources, onDownload }: {
  sources: AssistantSource[];
  onDownload: (documentId: string, documentName: string) => Promise<void>;
}) {
  const [expanded, setExpanded] = useState(false);
  const groups = useMemo(() => groupAssistantSources(sources), [sources]);
  const visible = expanded ? groups : groups.slice(0, 3);
  return (
    <Stack mt={1} gap={0.5} maxWidth="85%">
      <Typography variant="caption" fontWeight={800}>Fontes</Typography>
      <Stack direction="row" gap={0.5} flexWrap="wrap" minWidth={0}>
        {visible.map((group) => {
          const pageText = compactPages(group.pages);
          const pages = pageText ? ` · ${group.pages.length === 1 ? "pág." : "págs."} ${pageText}` : "";
          const status = !group.documentExists ? " · documento removido"
            : !group.documentCurrentlyActive ? " · documento atualmente inativo" : "";
          const label = `${group.documentName}${pages}${status}`;
          return (
            <Tooltip key={group.documentId} title={label}>
              <Chip
                clickable={group.documentExists}
                onClick={group.documentExists ? () => void onDownload(group.documentId, group.documentName) : undefined}
                label={label}
                sx={{ maxWidth: { xs: "100%", sm: 440 }, "& .MuiChip-label": { overflow: "hidden", textOverflow: "ellipsis" } }}
              />
            </Tooltip>
          );
        })}
        {!expanded && groups.length > 3 && (
          <Button size="small" onClick={() => setExpanded(true)}>+ {groups.length - 3} fontes</Button>
        )}
        {expanded && groups.length > 3 && (
          <Button size="small" onClick={() => setExpanded(false)}>Mostrar menos</Button>
        )}
      </Stack>
    </Stack>
  );
}
