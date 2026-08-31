import { useEffect, useRef, useState } from "react";
import { Alert, Box, Card, CardContent, CircularProgress, Dialog, DialogContent, DialogTitle, IconButton, Stack, Typography } from "@mui/material";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import DownloadRoundedIcon from "@mui/icons-material/DownloadRounded";
import { attachmentBlob } from "./api";
import type { Attachment } from "./types";
import { formatAttachmentSize } from "../requests/attachments";

const previewable = (type: string) => type.startsWith("image/") || type.startsWith("audio/") || type.startsWith("video/") || type === "application/pdf";

export function AttachmentsPreview({ items }: { items: Attachment[] }) {
  const [urls, setUrls] = useState<Record<string, string>>({});
  const [selected, setSelected] = useState<Attachment>();
  const [loading, setLoading] = useState<Set<string>>(new Set());
  const [error, setError] = useState("");
  const urlsRef = useRef(urls);
  urlsRef.current = urls;
  async function ensureUrl(item: Attachment) {
    if (urlsRef.current[item.id]) return urlsRef.current[item.id];
    setLoading(current => new Set(current).add(item.id));
    try { const url = URL.createObjectURL(await attachmentBlob(item.id)); setUrls(current => ({ ...current, [item.id]: url })); return url; }
    catch { setError("Não foi possível carregar o anexo."); }
    finally { setLoading(current => { const next = new Set(current); next.delete(item.id); return next; }); }
  }
  const ids = items.map(item => item.id).join("|");
  useEffect(() => { items.forEach(item => { void ensureUrl(item); }); }, [ids]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => () => Object.values(urlsRef.current).forEach(url => URL.revokeObjectURL(url)), []);
  if (!items.length) return null;
  const selectedUrl = selected ? urls[selected.id] : undefined;
  const open = async (item: Attachment) => { await ensureUrl(item); setSelected(item); };
  return <Stack spacing={1.25}>{error && <Alert severity="error">{error}</Alert>}<Box display="grid" gridTemplateColumns={{ xs: "1fr", sm: "repeat(2, minmax(0, 1fr))" }} gap={1}>
    {items.map(item => { const url = urls[item.id]; return <Card key={item.id} variant="outlined" sx={{ minWidth: 0 }}><CardContent sx={{ display: "flex", alignItems: "center", gap: 1.25, p: 1, "&:last-child": { pb: 1 } }}>
      <Box role={previewable(item.contentType) ? "button" : undefined} tabIndex={previewable(item.contentType) ? 0 : undefined} aria-label={previewable(item.contentType) ? `Visualizar ${item.originalFileName}` : undefined} onClick={() => previewable(item.contentType) && void open(item)} onKeyDown={event => { if (previewable(item.contentType) && (event.key === "Enter" || event.key === " ")) void open(item); }} sx={{ width: 88, height: 64, flexShrink: 0, display: "grid", placeItems: "center", overflow: "hidden", bgcolor: "action.hover", borderRadius: 1, cursor: previewable(item.contentType) ? "pointer" : "default" }}>
        {loading.has(item.id) ? <CircularProgress size={20} /> : item.contentType.startsWith("image/") && url ? <Box component="img" src={url} alt={`Prévia de ${item.originalFileName}`} sx={{ width: "100%", height: "100%", objectFit: "cover" }} /> : item.contentType.startsWith("audio/") && url ? <Box component="audio" controls src={url} sx={{ width: 84 }} onClick={event => event.stopPropagation()} /> : item.contentType.startsWith("video/") && url ? <Box component="video" controls src={url} sx={{ width: "100%", maxHeight: 64 }} onClick={event => event.stopPropagation()} /> : item.contentType === "application/pdf" && url ? <Box component="iframe" title={`Prévia de ${item.originalFileName}`} src={url} sx={{ width: "100%", height: "100%", border: 0, pointerEvents: "none" }} /> : <Typography variant="caption">ARQUIVO</Typography>}
      </Box><Box flex={1} minWidth={0}><Typography noWrap title={item.originalFileName}>{item.originalFileName}</Typography><Typography variant="caption" color="text.secondary">{item.contentType} · {formatAttachmentSize(item.fileSize)}</Typography></Box>
      {!previewable(item.contentType) && url && <IconButton component="a" href={url} download={item.originalFileName} aria-label={`Baixar ${item.originalFileName}`}><DownloadRoundedIcon /></IconButton>}
    </CardContent></Card>; })}
  </Box><Dialog open={Boolean(selected)} onClose={() => setSelected(undefined)} fullWidth maxWidth="md"><DialogTitle>{selected?.originalFileName}<IconButton aria-label="Fechar visualização" onClick={() => setSelected(undefined)} sx={{ float: "right" }}><CloseRoundedIcon /></IconButton></DialogTitle><DialogContent>{selected && selectedUrl && (selected.contentType.startsWith("image/") ? <Box component="img" src={selectedUrl} alt={selected.originalFileName} sx={{ maxWidth: "100%", maxHeight: "75vh", display: "block", mx: "auto" }} /> : selected.contentType.startsWith("audio/") ? <Box component="audio" controls autoPlay src={selectedUrl} sx={{ width: "100%" }} /> : selected.contentType.startsWith("video/") ? <Box component="video" controls autoPlay src={selectedUrl} sx={{ width: "100%", maxHeight: "70vh" }} /> : <Box component="iframe" title={selected.originalFileName} src={selectedUrl} sx={{ width: "100%", height: "70vh", border: 0 }} />)}</DialogContent></Dialog></Stack>;
}
