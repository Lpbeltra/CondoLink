import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import { attachmentBlob } from "./api";
import type { Attachment } from "./types";
import { formatAttachmentSize } from "../requests/attachments";
export function AttachmentsPreview({ items }: { items: Attachment[] }) {
  const [urls, setUrls] = useState<Record<string, string>>({}),
    [selected, setSelected] = useState<Attachment>(),
    [loading, setLoading] = useState<string>(),
    [error, setError] = useState("");
  const urlsRef = useRef(urls);
  urlsRef.current = urls;
  useEffect(
    () => () => {
    Object.values(urlsRef.current).forEach((url) => URL.revokeObjectURL(url));
    },
    [],
  );
  async function load(item: Attachment) {
    setLoading(item.id);
    setError("");
    try {
      let url = urls[item.id];
      if (!url) {
        url = URL.createObjectURL(await attachmentBlob(item.id));
        setUrls((x) => ({ ...x, [item.id]: url }));
      }
      setSelected(item);
    } catch {
      setError("Não foi possível carregar o anexo.");
    } finally {
      setLoading(undefined);
    }
  }
  if (!items.length) return null;
  const url = selected ? urls[selected.id] : undefined;
  return (
    <Stack spacing={1}>
      {error && <Alert severity="error">{error}</Alert>}
      <Box display="flex" gap={1} flexWrap="wrap">
        {items.map((item) => (
          <Button
            key={item.id}
            variant="outlined"
            onClick={() => void load(item)}
            disabled={loading === item.id}
          >
            {loading === item.id ? (
              <CircularProgress size={18} />
            ) : (
              item.originalFileName
            )}{" "}
            · {formatAttachmentSize(item.fileSize)}
          </Button>
        ))}
      </Box>
      <Dialog
        open={Boolean(selected)}
        onClose={() => setSelected(undefined)}
        fullWidth
        maxWidth="md"
      >
        <DialogTitle>
          {selected?.originalFileName}
          <IconButton
            aria-label="Fechar visualização"
            onClick={() => setSelected(undefined)}
            sx={{ float: "right" }}
          >
            <CloseRoundedIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent>
          {selected &&
            url &&
            (selected.contentType.startsWith("image/") ? (
              <Box
                component="img"
                src={url}
                alt={selected.originalFileName}
                sx={{ maxWidth: "100%", display: "block", mx: "auto" }}
              />
            ) : selected.contentType.startsWith("audio/") ? (
              <Box
                component="audio"
                controls
                src={url}
                sx={{ width: "100%" }}
              />
            ) : selected.contentType.startsWith("video/") ? (
              <Box
                component="video"
                controls
                src={url}
                sx={{ width: "100%", maxHeight: "70vh" }}
              />
            ) : selected.contentType === "application/pdf" ? (
              <Box
                component="iframe"
                title={selected.originalFileName}
                src={url}
                sx={{ width: "100%", height: "70vh", border: 0 }}
              />
            ) : (
              <Stack>
                <Typography>
                  Pré-visualização indisponível para este formato.
                </Typography>
                <Button
                  component="a"
                  href={url}
                  download={selected.originalFileName}
                >
                  Baixar arquivo
                </Button>
              </Stack>
            ))}
        </DialogContent>
      </Dialog>
    </Stack>
  );
}
