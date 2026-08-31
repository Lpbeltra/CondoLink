import { useEffect, useMemo } from "react";
import { Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import DeleteOutlineRoundedIcon from "@mui/icons-material/DeleteOutlineRounded";
import { formatAttachmentSize } from "../requests/attachments";

export function LocalAttachmentsPreview({ files, onRemove }: { files: File[]; onRemove: (index: number) => void }) {
  const items = useMemo(() => files.map(file => ({ file, url: URL.createObjectURL(file) })), [files]);
  useEffect(() => () => items.forEach(item => URL.revokeObjectURL(item.url)), [items]);
  if (!items.length) return <Typography color="text.secondary" variant="body2">Nenhum anexo selecionado.</Typography>;
  return <Stack spacing={1}>{items.map(({ file, url }, index) => <Card variant="outlined" key={`${file.name}-${file.lastModified}`}>
    <CardContent sx={{ display: "flex", gap: 2, alignItems: "center", py: 1.25, "&:last-child": { pb: 1.25 } }}>
      <Box sx={{ width: 96, height: 72, flexShrink: 0, display: "grid", placeItems: "center", overflow: "hidden", bgcolor: "action.hover", borderRadius: 1 }}>
        {file.type.startsWith("image/") ? <Box component="img" src={url} alt={`Prévia de ${file.name}`} sx={{ width: "100%", height: "100%", objectFit: "cover" }} />
          : file.type.startsWith("video/") ? <Box component="video" src={url} controls sx={{ width: "100%", maxHeight: 72 }} />
          : file.type.startsWith("audio/") ? <Box component="audio" src={url} controls sx={{ width: 92 }} />
          : file.type === "application/pdf" ? <Box component="iframe" src={url} title={`Prévia de ${file.name}`} sx={{ width: "100%", height: "100%", border: 0 }} />
          : <Typography variant="caption">ARQUIVO</Typography>}
      </Box>
      <Box flex={1} minWidth={0}><Typography noWrap>{file.name}</Typography><Typography variant="caption" color="text.secondary">{file.type || "Tipo desconhecido"} · {formatAttachmentSize(file.size)}</Typography></Box>
      <Button type="button" size="small" color="error" startIcon={<DeleteOutlineRoundedIcon />} onClick={() => onRemove(index)}>Remover</Button>
    </CardContent>
  </Card>)}</Stack>;
}
